using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services.FollowerTracking
{
    public sealed class FollowerTrackingException : Exception
    {
        public int StatusCode { get; }
        public FollowerTrackingException(int statusCode, string message) : base(message) => StatusCode = statusCode;
    }

    public interface IFollowerTrackingService
    {
        Task<FollowerShiftDto> StartAsync(DelegateGetDTO follower, CancellationToken ct);
        Task<FollowerShiftDto> EndAsync(DelegateGetDTO follower, CancellationToken ct);
        Task<FollowerShiftDto?> CurrentAsync(int followerId, CancellationToken ct);
        Task<FollowerLocationBatchResultDto> IngestBatchAsync(DelegateGetDTO follower, FollowerLocationBatchRequestDto request, CancellationToken ct);
        Task<FollowerLiveLocationDto> IngestLiveAsync(DelegateGetDTO follower, FollowerLiveLocationRequestDto request, CancellationToken ct);
        Task RecordEventAsync(DelegateGetDTO follower, int? shiftId, string eventType, CancellationToken ct);
        Task<IReadOnlyList<FollowerLiveLocationDto>> ListLiveAsync(CancellationToken ct);
        Task<(FollowerShiftDto? Shift, IReadOnlyList<FollowerRoutePointDto> Points)> GetRouteAsync(int followerId, DateTime dateIraq, CancellationToken ct);
        Task<IReadOnlyList<(int FollowerId, string FollowerName)>> ListFollowersAsync(CancellationToken ct);
    }

    public sealed class FollowerTrackingService : IFollowerTrackingService
    {
        private const int MaxBatch = 500;
        private readonly IFollowerTrackingRepository _repo;
        private readonly IFollowerActionsRepository _followerActions;

        public FollowerTrackingService(IFollowerTrackingRepository repo, IFollowerActionsRepository followerActions)
        {
            _repo = repo;
            _followerActions = followerActions;
        }

        public async Task<FollowerShiftDto> StartAsync(DelegateGetDTO follower, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = FollowerIraqTime.UtcNow;
            await _repo.CloseExpiredAsync(utc, ct);

            var active = await _repo.GetActiveByFollowerAsync(follower.DelegateId, ct);
            if (active != null && !FollowerIraqTime.IsExpired(active.CutoffAtUtc, utc))
            {
                active.IsNew = false;
                active.HasActiveShift = true;
                return active;
            }

            if (active != null)
            {
                await _repo.CloseAsync(active.ShiftId, utc, "AutomaticCutoff", ct);
                await _repo.EndLiveAsync(follower.DelegateId, ct);
            }

            var iraq = FollowerIraqTime.ToIraq(utc);
            var cityName = await _followerActions.GetFollowerCityNameAsync(follower.DelegateId, ct);
            try
            {
                var created = await _repo.InsertActiveAsync(
                    follower.DelegateId,
                    follower.DelegateName ?? "",
                    cityName,
                    cityName,
                    utc,
                    iraq,
                    FollowerIraqTime.CutoffUtc(utc),
                    ct);
                await _repo.InsertEventAsync(follower.DelegateId, created.ShiftId, "FOLLOWER_SHIFT_STARTED", utc, null, ct);
                created.IsNew = true;
                created.HasActiveShift = true;
                return created;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                var existing = await _repo.GetActiveByFollowerAsync(follower.DelegateId, ct)
                               ?? throw new FollowerTrackingException(409, "تعذر بدء الدوام.");
                existing.IsNew = false;
                existing.HasActiveShift = true;
                return existing;
            }
        }

        public async Task<FollowerShiftDto> EndAsync(DelegateGetDTO follower, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = FollowerIraqTime.UtcNow;
            var active = await _repo.GetActiveByFollowerAsync(follower.DelegateId, ct);
            if (active == null)
            {
                return new FollowerShiftDto
                {
                    Status = "Closed",
                    CloseReason = "ManualEnd",
                    StartedAtUtc = utc,
                    CutoffAtUtc = utc,
                    HasActiveShift = false
                };
            }

            await _repo.CloseAsync(active.ShiftId, utc, "ManualEnd", ct);
            await _repo.EndLiveAsync(follower.DelegateId, ct);
            await _repo.InsertEventAsync(follower.DelegateId, active.ShiftId, "FOLLOWER_SHIFT_ENDED", utc, null, ct);
            var closed = await _repo.GetByIdAsync(active.ShiftId, ct) ?? active;
            closed.Status = "Closed";
            closed.ClosedAtUtc = utc;
            closed.CloseReason = "ManualEnd";
            closed.HasActiveShift = false;
            return closed;
        }

        public async Task<FollowerShiftDto?> CurrentAsync(int followerId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = FollowerIraqTime.UtcNow;
            await _repo.CloseExpiredAsync(utc, ct);
            var active = await _repo.GetActiveByFollowerAsync(followerId, ct);
            if (active == null) return null;
            if (FollowerIraqTime.IsExpired(active.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(active.ShiftId, utc, "AutomaticCutoff", ct);
                await _repo.EndLiveAsync(followerId, ct);
                return null;
            }

            active.HasActiveShift = true;
            return active;
        }

        public async Task<FollowerLocationBatchResultDto> IngestBatchAsync(
            DelegateGetDTO follower,
            FollowerLocationBatchRequestDto request,
            CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            request.Points ??= [];
            if (request.Points.Count > MaxBatch)
            {
                throw new FollowerTrackingException(400, "عدد النقاط أكبر من الحد المسموح.");
            }

            var utc = FollowerIraqTime.UtcNow;
            var shift = await _repo.GetByIdAsync(request.ShiftId, ct)
                        ?? throw new FollowerTrackingException(404, "الدوام غير موجود.");
            if (shift.FollowerId != follower.DelegateId)
            {
                throw new FollowerTrackingException(403, "لا يمكنك إرسال موقع لدوام متابع آخر.");
            }

            if (shift.Status == "Active" && FollowerIraqTime.IsExpired(shift.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(shift.ShiftId, utc, "AutomaticCutoff", ct);
                shift.Status = "Closed";
                shift.ClosedAtUtc = utc;
            }

            if (shift.Status != "Active" || shift.ClosedAtUtc != null)
            {
                throw new FollowerTrackingException(409, "الدوام مغلق.");
            }

            var result = new FollowerLocationBatchResultDto { ShiftId = shift.ShiftId, ShiftStatus = shift.Status };
            foreach (var point in request.Points)
            {
                NormalizePoint(point);
                if (!IsValidPoint(point, shift))
                {
                    result.Rejected++;
                    continue;
                }

                var inserted = await _repo.TryInsertPointAsync(follower.DelegateId, shift.ShiftId, point, utc, ct);
                if (inserted == 0) result.Duplicates++;
                else result.Accepted++;
            }

            return result;
        }

        public async Task<FollowerLiveLocationDto> IngestLiveAsync(
            DelegateGetDTO follower,
            FollowerLiveLocationRequestDto request,
            CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = FollowerIraqTime.UtcNow;
            var shift = await _repo.GetByIdAsync(request.ShiftId, ct)
                        ?? throw new FollowerTrackingException(404, "الدوام غير موجود.");
            if (shift.FollowerId != follower.DelegateId)
            {
                throw new FollowerTrackingException(403, "لا يمكنك إرسال موقع لدوام متابع آخر.");
            }

            if (shift.Status == "Active" && FollowerIraqTime.IsExpired(shift.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(shift.ShiftId, utc, "AutomaticCutoff", ct);
                shift.Status = "Closed";
                shift.ClosedAtUtc = utc;
            }

            if (shift.Status != "Active")
            {
                throw new FollowerTrackingException(409, "الدوام مغلق.");
            }

            if (!IsValidLive(request))
            {
                throw new FollowerTrackingException(400, "إحداثيات الموقع غير صالحة.");
            }

            request.CapturedAtUtc = DateTime.SpecifyKind(request.CapturedAtUtc == default ? utc : request.CapturedAtUtc, DateTimeKind.Utc);
            await _repo.UpsertLiveAsync(follower.DelegateId, follower.DelegateName ?? "", shift.ShiftId, request, utc, ct);
            return new FollowerLiveLocationDto
            {
                FollowerId = follower.DelegateId,
                FollowerName = follower.DelegateName ?? "",
                ShiftId = shift.ShiftId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Accuracy = request.Accuracy,
                CapturedAtUtc = request.CapturedAtUtc,
                UpdatedAtUtc = utc,
                ShiftStatus = "Active",
                LocationStatus = "Live"
            };
        }

        public async Task RecordEventAsync(DelegateGetDTO follower, int? shiftId, string eventType, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            if (shiftId is > 0)
            {
                var shift = await _repo.GetByIdAsync(shiftId.Value, ct);
                if (shift == null || shift.FollowerId != follower.DelegateId)
                {
                    throw new FollowerTrackingException(403, "لا يمكنك تسجيل حدث على دوام متابع آخر.");
                }
            }

            await _repo.InsertEventAsync(follower.DelegateId, shiftId, eventType, FollowerIraqTime.UtcNow, null, ct);
        }

        public async Task<IReadOnlyList<FollowerLiveLocationDto>> ListLiveAsync(CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            return await _repo.ListLiveAsync(ct);
        }

        public async Task<(FollowerShiftDto? Shift, IReadOnlyList<FollowerRoutePointDto> Points)> GetRouteAsync(
            int followerId, DateTime dateIraq, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var day = dateIraq.Date;
            var fromUtc = FollowerIraqTime.ToUtcFromIraq(day.Add(FollowerIraqTime.CutoffTime));
            var toUtc = FollowerIraqTime.ToUtcFromIraq(day.AddDays(1).Add(FollowerIraqTime.CutoffTime));
            var shift = await _repo.GetShiftForDayAsync(followerId, fromUtc, toUtc, ct);
            var points = await _repo.GetRouteAsync(followerId, fromUtc, toUtc, ct);
            return (shift, points);
        }

        public async Task<IReadOnlyList<(int FollowerId, string FollowerName)>> ListFollowersAsync(CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            return await _repo.ListFollowersWithShiftsAsync(ct);
        }

        private static void NormalizePoint(FollowerLocationPointDto point)
        {
            var slot = point.OfficialSlotUtc is DateTime o && o != default
                ? DateTime.SpecifyKind(o, DateTimeKind.Utc)
                : DateTime.SpecifyKind(point.CapturedAtUtc, DateTimeKind.Utc);
            var actual = point.ActualCapturedAtUtc is DateTime a && a != default
                ? DateTime.SpecifyKind(a, DateTimeKind.Utc)
                : slot;
            point.OfficialSlotUtc = slot;
            point.CapturedAtUtc = slot;
            point.ActualCapturedAtUtc = actual;
            point.IsOfficial = true;
            if (point.DeviceSequence <= 0)
            {
                point.DeviceSequence = Math.Max(1, slot.Subtract(DateTime.UnixEpoch).Ticks / FollowerOfficialSlot.Length.Ticks);
            }
        }

        public static bool IsValidPoint(FollowerLocationPointDto point, FollowerShiftDto shift)
        {
            if (point.DeviceSequence <= 0) return false;
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180) return false;
            if (Math.Abs(point.Latitude) < 0.000001 && Math.Abs(point.Longitude) < 0.000001) return false;
            if (point.Accuracy is < 0 or > 5000) return false;
            var slot = DateTime.SpecifyKind(point.OfficialSlotUtc ?? point.CapturedAtUtc, DateTimeKind.Utc);
            if (!FollowerOfficialSlot.IsExactOfficialSlot(shift.StartedAtUtc, slot)) return false;
            if (slot >= shift.CutoffAtUtc) return false;
            return true;
        }

        private static bool IsValidLive(FollowerLiveLocationRequestDto point)
        {
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180) return false;
            if (Math.Abs(point.Latitude) < 0.000001 && Math.Abs(point.Longitude) < 0.000001) return false;
            if (point.Accuracy is < 0 or > 5000) return false;
            return true;
        }
    }
}
