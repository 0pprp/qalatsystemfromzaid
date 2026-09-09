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

        public async Task<SalesShiftDTO> EndAsync(SalesIdentity identity, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = _clock.UtcNow;
            var active = await _repo.GetActiveByEmployeeAsync(identity.EmployeeId, ct);
            if (active == null)
            {
                return Map(new SalesShiftDTO
                {
                    Status = SalesShiftStatuses.Closed,
                    CloseReason = SalesShiftCloseReasons.ManualEnd,
                    StartedAtUtc = utc,
                    CutoffAtUtc = utc,
                    HasActiveShift = false
                }, isNew: false, hasActive: false);
            }

            await _repo.CloseAsync(active.ShiftId, utc, SalesShiftCloseReasons.ManualEnd, ct);
            var closed = await _repo.GetByIdAsync(active.ShiftId, ct) ?? active;
            closed.Status = SalesShiftStatuses.Closed;
            closed.ClosedAtUtc = utc;
            closed.CloseReason = SalesShiftCloseReasons.ManualEnd;
            return Map(closed, isNew: false, hasActive: false);
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
                shift.ClosedAtUtc = utc;
                await _repo.InsertEventAsync(identity.EmployeeId, shift.ShiftId, SalesTrackingEventTypes.ShiftAutoClosed, utc, null, ct);
            }

            // Any closed shift rejects the entire batch — including backdated CapturedAtUtc.
            if (shift.Status != SalesShiftStatuses.Active || shift.ClosedAtUtc != null)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "الدوام مغلق.");
            }

            var result = new SalesLocationBatchResultDTO
            {
                ShiftId = shift.ShiftId,
                ShiftStatus = shift.Status
            };

            var newestAccepted = (SalesLocationPointRequestDTO?)null;
            foreach (var point in request.Points)
            {
                var official = OfficialSlot.SnapOfficial(point);
                if (!IsValidPoint(official, shift))
                {
                    result.Rejected++;
                    continue;
                }

                var inserted = await _repo.TryInsertPointAsync(identity.EmployeeId, shift.ShiftId, official, utc, ct);
                if (inserted == 0)
                {
                    result.Duplicates++;
                }
                else
                {
                    result.Accepted++;
                    if (newestAccepted == null || official.CapturedAtUtc > newestAccepted.CapturedAtUtc)
                    {
                        newestAccepted = official;
                    }
                }
            }

            return result;
        }

        public async Task<SalesLiveLocationDTO> IngestLiveAsync(
            SalesIdentity identity,
            SalesLiveLocationRequestDTO request,
            CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
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
                shift.ClosedAtUtc = utc;
                await _repo.InsertEventAsync(identity.EmployeeId, shift.ShiftId, SalesTrackingEventTypes.ShiftAutoClosed, utc, null, ct);
            }

            if (shift.Status != SalesShiftStatuses.Active)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "الدوام مغلق.");
            }

            if (!IsValidLivePoint(request))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "إحداثيات الموقع غير صالحة.");
            }

            request.CapturedAtUtc = DateTime.SpecifyKind(request.CapturedAtUtc, DateTimeKind.Utc);
            if (request.CapturedAtUtc == default)
            {
                request.CapturedAtUtc = utc;
            }

            await _repo.UpsertLiveLocationAsync(identity.EmployeeId, shift.ShiftId, request, utc, ct);
            var live = new SalesLiveLocationDTO
            {
                EmployeeId = identity.EmployeeId,
                EmployeeName = identity.EmployeeName,
                CityValue = identity.BranchId,
                CityName = identity.BranchName,
                ShiftId = shift.ShiftId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Accuracy = request.Accuracy,
                Speed = request.Speed,
                Heading = request.Heading,
                CapturedAt = request.CapturedAtUtc,
                DeviceTimestampUtc = request.CapturedAtUtc,
                UpdatedAtUtc = utc,
                LocationStatus = SalesLocationStatuses.Live,
                ShiftStatus = SalesShiftStatuses.Active
            };
            await _broadcaster.PublishAsync(live, ct);
            return live;
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

            if (Math.Abs(point.Latitude) < 0.000001 && Math.Abs(point.Longitude) < 0.000001)
            {
                return false;
            }

            if (point.Accuracy is < 0 or > 5000)
            {
                return false;
            }

            var slot = DateTime.SpecifyKind(point.OfficialSlotUtc ?? point.CapturedAtUtc, DateTimeKind.Utc);
            // OfficialSlotUtc must be exactly start + n*10 (n >= 1). No early skew.
            if (!OfficialSlot.IsExactOfficialSlot(shift.StartedAtUtc, slot))
            {
                return false;
            }

            if (slot >= shift.CutoffAtUtc)
            {
                return false;
            }

            // Actual capture may be late vs the logical slot; it must not precede shift start.
            if (point.ActualCapturedAtUtc is DateTime actual)
            {
                var actualUtc = DateTime.SpecifyKind(actual, DateTimeKind.Utc);
                if (actualUtc < DateTime.SpecifyKind(shift.StartedAtUtc, DateTimeKind.Utc))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsValidLivePoint(SalesLiveLocationRequestDTO point)
        {
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
            {
                return false;
            }

            if (Math.Abs(point.Latitude) < 0.000001 && Math.Abs(point.Longitude) < 0.000001)
            {
                return false;
            }

            if (point.Accuracy is < 0 or > 5000)
            {
                return false;
            }

            return true;
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
