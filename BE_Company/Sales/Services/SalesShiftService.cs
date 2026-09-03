using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class SalesShiftService : ISalesShiftService
    {
        private readonly ISalesTrackingRepository _repo;
        private readonly IIraqClock _clock;

        public SalesShiftService(ISalesTrackingRepository repo, IIraqClock clock)
        {
            _repo = repo;
            _clock = clock;
        }

        public async Task<SalesShiftDTO> StartAsync(SalesIdentity identity, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = _clock.UtcNow;
            await CloseIfExpiredAsync(identity.EmployeeId, utc, ct);

            var active = await _repo.GetActiveByEmployeeAsync(identity.EmployeeId, ct);
            if (active != null && !IraqTimeService.IsExpired(active.CutoffAtUtc, utc))
            {
                return Map(active, isNew: false, hasActive: true);
            }

            var iraq = IraqTimeService.ToIraq(utc);
            var cutoffUtc = IraqTimeService.CutoffUtc(utc);
            try
            {
                var created = await _repo.InsertActiveAsync(
                    identity.EmployeeId,
                    identity.EmployeeName,
                    identity.BranchId,
                    identity.BranchName,
                    utc,
                    iraq,
                    cutoffUtc,
                    ct);
                await _repo.InsertEventAsync(identity.EmployeeId, created.ShiftId, SalesTrackingEventTypes.ShiftStarted, utc, null, ct);
                return Map(created, isNew: true, hasActive: true);
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                var existing = await _repo.GetActiveByEmployeeAsync(identity.EmployeeId, ct)
                               ?? throw new InvalidOperationException("تعذر بدء الدوام.");
                return Map(existing, isNew: false, hasActive: true);
            }
        }

        public async Task<SalesShiftDTO?> GetCurrentAsync(int employeeId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            await CloseIfExpiredAsync(employeeId, _clock.UtcNow, ct);
            var active = await _repo.GetActiveByEmployeeAsync(employeeId, ct);
            return active == null ? null : Map(active, isNew: false, hasActive: true);
        }

        public async Task<bool> IsShiftStartedAsync(int employeeId, CancellationToken ct)
        {
            var current = await GetCurrentAsync(employeeId, ct);
            return current?.HasActiveShift == true;
        }

        public async Task CloseExpiredAsync(CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            await _repo.CloseExpiredAsync(_clock.UtcNow, ct);
        }

        private async Task CloseIfExpiredAsync(int employeeId, DateTime utc, CancellationToken ct)
        {
            var active = await _repo.GetActiveByEmployeeAsync(employeeId, ct);
            if (active != null && IraqTimeService.IsExpired(active.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(active.ShiftId, utc, SalesShiftCloseReasons.AutomaticCutoff, ct);
                await _repo.InsertEventAsync(employeeId, active.ShiftId, SalesTrackingEventTypes.ShiftAutoClosed, utc, null, ct);
            }
        }

        internal static SalesShiftDTO Map(SalesShiftDTO row, bool isNew, bool hasActive)
        {
            row.IsNew = isNew;
            row.HasActiveShift = hasActive;
            if (row.CutoffAtUtc.Kind != DateTimeKind.Utc)
            {
                row.CutoffAtUtc = DateTime.SpecifyKind(row.CutoffAtUtc, DateTimeKind.Utc);
            }
            if (row.StartedAtUtc.Kind != DateTimeKind.Utc)
            {
                row.StartedAtUtc = DateTime.SpecifyKind(row.StartedAtUtc, DateTimeKind.Utc);
            }
            row.StartedAt = row.StartedAtUtc;
            row.CutoffAt = row.CutoffAtUtc;
            return row;
        }
    }

    public sealed class SalesLocationIngestService : ISalesLocationIngestService
    {
        public const int MaxBatch = 500;
        private readonly ISalesTrackingRepository _repo;
        private readonly IIraqClock _clock;
        private readonly ISalesLocationBroadcaster _broadcaster;

        public SalesLocationIngestService(
            ISalesTrackingRepository repo,
            IIraqClock clock,
            ISalesLocationBroadcaster? broadcaster = null)
        {
            _repo = repo;
            _clock = clock;
            _broadcaster = broadcaster ?? NullSalesLocationBroadcaster.Instance;
        }

        public async Task<SalesLocationBatchResultDTO> IngestBatchAsync(
            SalesIdentity identity,
            SalesLocationBatchRequestDTO request,
            CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            request.Points ??= [];
            if (request.Points.Count > MaxBatch)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "عدد النقاط أكبر من الحد المسموح.");
            }

            var utc = _clock.UtcNow;
            var shift = await _repo.GetByIdAsync(request.ShiftId, ct)
                        ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "الدوام غير موجود.");
            if (shift.EmployeeId != identity.EmployeeId)
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك إرسال موقع لدوام موظف آخر.");
            }

            if (shift.Status == SalesShiftStatuses.Active && IraqTimeService.IsExpired(shift.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(shift.ShiftId, utc, SalesShiftCloseReasons.AutomaticCutoff, ct);
                shift.Status = SalesShiftStatuses.Closed;
                await _repo.InsertEventAsync(identity.EmployeeId, shift.ShiftId, SalesTrackingEventTypes.ShiftAutoClosed, utc, null, ct);
            }

            var result = new SalesLocationBatchResultDTO
            {
                ShiftId = shift.ShiftId,
                ShiftStatus = shift.Status
            };

            var newestAccepted = (SalesLocationPointRequestDTO?)null;
            foreach (var point in request.Points)
            {
                if (!IsValidPoint(point, shift))
                {
                    result.Rejected++;
                    continue;
                }

                var inserted = await _repo.TryInsertPointAsync(identity.EmployeeId, shift.ShiftId, Normalize(point), utc, ct);
                if (inserted == 0)
                {
                    result.Duplicates++;
                }
                else
                {
                    result.Accepted++;
                    if (newestAccepted == null || point.CapturedAtUtc > newestAccepted.CapturedAtUtc)
                    {
                        newestAccepted = point;
                    }
                }
            }

            if (newestAccepted != null)
            {
                var live = new SalesLiveLocationDTO
                {
                    EmployeeId = identity.EmployeeId,
                    EmployeeName = identity.EmployeeName,
                    CityValue = identity.BranchId,
                    CityName = identity.BranchName,
                    ShiftId = shift.ShiftId,
                    Latitude = newestAccepted.Latitude,
                    Longitude = newestAccepted.Longitude,
                    Accuracy = newestAccepted.Accuracy,
                    CapturedAt = DateTime.SpecifyKind(newestAccepted.CapturedAtUtc, DateTimeKind.Utc)
                };
                result.LiveUpdate = live;
                await _broadcaster.PublishAsync(live, ct);
            }

            return result;
        }

        public async Task RecordEventAsync(SalesIdentity identity, SalesTrackingEventRequestDTO request, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var type = request.EventType ?? string.Empty;
            if (!SalesTrackingEventTypes.Allowed.Contains(type))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "نوع الحدث غير صالح.");
            }

            int? shiftId = request.ShiftId;
            if (shiftId is > 0)
            {
                var shift = await _repo.GetByIdAsync(shiftId.Value, ct);
                if (shift == null || shift.EmployeeId != identity.EmployeeId)
                {
                    throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك تسجيل حدث على دوام موظف آخر.");
                }
            }

            var occurred = request.OccurredAtUtc == default || request.OccurredAtUtc == null
                ? _clock.UtcNow
                : request.OccurredAtUtc.Value.ToUniversalTime();
            await _repo.InsertEventAsync(identity.EmployeeId, shiftId, type, occurred, request.Metadata, ct);
        }

        internal static bool IsValidPoint(SalesLocationPointRequestDTO point, SalesShiftDTO shift)
        {
            if (point.DeviceSequence <= 0)
            {
                return false;
            }

            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
            {
                return false;
            }

            if (point.Accuracy is < 0 or > 5000)
            {
                return false;
            }

            var captured = DateTime.SpecifyKind(point.CapturedAtUtc, DateTimeKind.Utc);
            if (captured < shift.StartedAtUtc.AddMinutes(-5))
            {
                return false;
            }

            if (captured > shift.CutoffAtUtc)
            {
                return false;
            }

            return true;
        }

        private static SalesLocationPointRequestDTO Normalize(SalesLocationPointRequestDTO point)
        {
            point.CapturedAtUtc = DateTime.SpecifyKind(point.CapturedAtUtc, DateTimeKind.Utc);
            return point;
        }
    }

    public sealed class SalesShiftCutoffHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<SalesShiftCutoffHostedService> _logger;

        public SalesShiftCutoffHostedService(IServiceScopeFactory scopes, ILogger<SalesShiftCutoffHostedService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = _scopes.CreateAsyncScope();
                    var guard = scope.ServiceProvider.GetRequiredService<SalesDevelopmentGuard>();
                    var check = await guard.CanRunSalesModuleAsync(stoppingToken);
                    if (check.Ok)
                    {
                        var shifts = scope.ServiceProvider.GetRequiredService<ISalesShiftService>();
                        await shifts.CloseExpiredAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sales shift cutoff sweep skipped.");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}
