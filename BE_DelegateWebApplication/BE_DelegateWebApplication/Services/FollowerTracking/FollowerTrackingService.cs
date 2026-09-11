using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services.FollowerIdentity;
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
        Task<FollowerShiftDto> StartAsync(FollowerUserIdentity follower, CancellationToken ct);
        Task<FollowerShiftDto> EndAsync(FollowerUserIdentity follower, CancellationToken ct);
        Task<FollowerShiftDto?> CurrentAsync(int followerUserId, CancellationToken ct);
        Task<FollowerLocationBatchResultDto> IngestBatchAsync(FollowerUserIdentity follower, FollowerLocationBatchRequestDto request, CancellationToken ct);
        Task<FollowerLiveLocationDto> IngestLiveAsync(FollowerUserIdentity follower, FollowerLiveLocationRequestDto request, CancellationToken ct);
        Task RecordEventAsync(FollowerUserIdentity follower, int? shiftId, string eventType, CancellationToken ct);
        Task<IReadOnlyList<FollowerLiveLocationDto>> ListLiveAsync(CancellationToken ct);
        Task<(FollowerShiftDto? Shift, IReadOnlyList<FollowerRoutePointDto> Points)> GetRouteAsync(int followerUserId, DateTime dateIraq, CancellationToken ct);
        Task<IReadOnlyList<object>> ListFollowersAsync(CancellationToken ct);
    }

    public sealed class FollowerTrackingService : IFollowerTrackingService
    {
        private const int MaxBatch = 500;
        private readonly IFollowerTrackingRepository _repo;
        private readonly IFollowerIdentityService _identity;

        public FollowerTrackingService(IFollowerTrackingRepository repo, IFollowerIdentityService identity)
        {
            _repo = repo;
            _identity = identity;
        }

        public async Task<FollowerShiftDto> StartAsync(FollowerUserIdentity follower, CancellationToken ct)
        {
            if (!follower.IsActive || !FollowerUserType.IsFollowerType(follower.UserType))
            {
                throw new FollowerTrackingException(403, "غير مصرح — المستخدم ليس متابعًا.");
            }

            await _repo.EnsureSchemaAsync(ct);
            var utc = FollowerIraqTime.UtcNow;
            await _repo.CloseExpiredAsync(utc, ct);

            var userId = follower.UserId;
            var active = await _repo.GetActiveByFollowerAsync(userId, ct);
            if (active != null && !FollowerIraqTime.IsExpired(active.CutoffAtUtc, utc))
            {
                active.IsNew = false;
                active.HasActiveShift = true;
                return active;
            }

            if (active != null)
            {
                await _repo.CloseAsync(active.ShiftId, utc, "AutomaticCutoff", ct);
                await _repo.EndLiveAsync(userId, ct);
            }

            var iraq = FollowerIraqTime.ToIraq(utc);
            var cityName = follower.CityName;
            var cityValue = follower.CityId?.ToString();
            try
            {
                var created = await _repo.InsertActiveAsync(
                    userId,
                    follower.UserName ?? "",
                    cityValue,
                    cityName,
                    utc,
                    iraq,
                    FollowerIraqTime.CutoffUtc(utc),
                    ct);
                await _repo.InsertEventAsync(userId, created.ShiftId, "FOLLOWER_SHIFT_STARTED", utc, null, ct);
                created.IsNew = true;
                created.HasActiveShift = true;
                return created;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                var existing = await _repo.GetActiveByFollowerAsync(userId, ct)
                               ?? throw new FollowerTrackingException(409, "تعذر بدء الدوام.");
                existing.IsNew = false;
                existing.HasActiveShift = true;
                return existing;
            }
        }

        public async Task<FollowerShiftDto> EndAsync(FollowerUserIdentity follower, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = FollowerIraqTime.UtcNow;
            var active = await _repo.GetActiveByFollowerAsync(follower.UserId, ct);
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
            await _repo.EndLiveAsync(follower.UserId, ct);
            await _repo.InsertEventAsync(follower.UserId, active.ShiftId, "FOLLOWER_SHIFT_ENDED", utc, null, ct);
            active.Status = "Closed";
            active.ClosedAtUtc = utc;
            active.CloseReason = "ManualEnd";
            active.HasActiveShift = false;
            return active;
        }

        public async Task<FollowerShiftDto?> CurrentAsync(int followerUserId, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var utc = FollowerIraqTime.UtcNow;
            await _repo.CloseExpiredAsync(utc, ct);
            var active = await _repo.GetActiveByFollowerAsync(followerUserId, ct);
            if (active == null) return null;
            if (FollowerIraqTime.IsExpired(active.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(active.ShiftId, utc, "AutomaticCutoff", ct);
                await _repo.EndLiveAsync(followerUserId, ct);
                return null;
            }

            active.HasActiveShift = true;
            return active;
        }

        public async Task<FollowerLocationBatchResultDto> IngestBatchAsync(
            FollowerUserIdentity follower, FollowerLocationBatchRequestDto request, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            if (request.Points.Count > MaxBatch)
            {
                throw new FollowerTrackingException(400, "عدد النقاط أكبر من الحد المسموح.");
            }

            var shift = await _repo.GetByIdAsync(request.ShiftId, ct)
                        ?? throw new FollowerTrackingException(404, "الدوام غير موجود.");
            if (shift.FollowerId != follower.UserId)
            {
                throw new FollowerTrackingException(403, "لا يمكنك إرسال موقع لدوام متابع آخر.");
            }

            var utc = FollowerIraqTime.UtcNow;
            if (shift.Status == "Active" && FollowerIraqTime.IsExpired(shift.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(shift.ShiftId, utc, "AutomaticCutoff", ct);
                await _repo.EndLiveAsync(follower.UserId, ct);
                shift.Status = "Closed";
            }

            if (shift.Status != "Active")
            {
                throw new FollowerTrackingException(409, "الدوام مغلق.");
            }

            var accepted = 0;
            var duplicates = 0;
            var rejected = 0;
            foreach (var point in request.Points)
            {
                if (!IsValidPoint(point, shift))
                {
                    rejected++;
                    continue;
                }

                var inserted = await _repo.TryInsertPointAsync(follower.UserId, shift.ShiftId, point, utc, ct);
                if (inserted > 0) accepted++;
                else duplicates++;
            }

            return new FollowerLocationBatchResultDto
            {
                ShiftId = shift.ShiftId,
                ShiftStatus = shift.Status,
                Accepted = accepted,
                Duplicates = duplicates,
                Rejected = rejected
            };
        }

        public async Task<FollowerLiveLocationDto> IngestLiveAsync(
            FollowerUserIdentity follower, FollowerLiveLocationRequestDto request, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var shift = await _repo.GetByIdAsync(request.ShiftId, ct)
                        ?? throw new FollowerTrackingException(404, "الدوام غير موجود.");
            if (shift.FollowerId != follower.UserId)
            {
                throw new FollowerTrackingException(403, "لا يمكنك إرسال موقع لدوام متابع آخر.");
            }

            var utc = FollowerIraqTime.UtcNow;
            if (shift.Status == "Active" && FollowerIraqTime.IsExpired(shift.CutoffAtUtc, utc))
            {
                await _repo.CloseAsync(shift.ShiftId, utc, "AutomaticCutoff", ct);
                await _repo.EndLiveAsync(follower.UserId, ct);
                throw new FollowerTrackingException(409, "الدوام مغلق.");
            }

            if (shift.Status != "Active")
            {
                throw new FollowerTrackingException(409, "الدوام مغلق.");
            }

            if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            {
                throw new FollowerTrackingException(400, "إحداثيات الموقع غير صالحة.");
            }

            await _repo.UpsertLiveAsync(follower.UserId, follower.UserName ?? "", shift.ShiftId, request, utc, ct);
            return new FollowerLiveLocationDto
            {
                FollowerId = follower.UserId,
                FollowerName = follower.UserName ?? "",
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

        public async Task RecordEventAsync(FollowerUserIdentity follower, int? shiftId, string eventType, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            if (shiftId is int sid)
            {
                var shift = await _repo.GetByIdAsync(sid, ct);
                if (shift == null || shift.FollowerId != follower.UserId)
                {
                    throw new FollowerTrackingException(403, "لا يمكنك تسجيل حدث على دوام متابع آخر.");
                }
            }

            await _repo.InsertEventAsync(follower.UserId, shiftId, eventType, FollowerIraqTime.UtcNow, null, ct);
        }

        public async Task<IReadOnlyList<FollowerLiveLocationDto>> ListLiveAsync(CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            return await _repo.ListLiveAsync(ct);
        }

        public async Task<(FollowerShiftDto? Shift, IReadOnlyList<FollowerRoutePointDto> Points)> GetRouteAsync(
            int followerUserId, DateTime dateIraq, CancellationToken ct)
        {
            await _repo.EnsureSchemaAsync(ct);
            var day = dateIraq.Date;
            var fromUtc = FollowerIraqTime.ToUtcFromIraq(day);
            var toUtc = FollowerIraqTime.ToUtcFromIraq(day.AddDays(1));
            var shift = await _repo.GetShiftForDayAsync(followerUserId, fromUtc, toUtc, ct);
            var points = await _repo.GetRouteAsync(followerUserId, fromUtc, toUtc, ct);
            return (shift, points);
        }

        public async Task<IReadOnlyList<object>> ListFollowersAsync(CancellationToken ct)
        {
            var users = await _identity.ListActiveAsync(ct);
            IReadOnlyList<FollowerLiveLocationDto> live = Array.Empty<FollowerLiveLocationDto>();
            try
            {
                await _repo.EnsureSchemaAsync(ct);
                live = await _repo.ListLiveAsync(ct);
            }
            catch
            {
                // Missing GPS tables / empty live must not break follower directory.
                live = Array.Empty<FollowerLiveLocationDto>();
            }

            var liveMap = live.ToDictionary(x => x.FollowerId, x => x);
            return users.Select(u =>
            {
                liveMap.TryGetValue(u.UserId, out var loc);
                return (object)new
                {
                    followerId = u.UserId,
                    userId = u.UserId,
                    followerName = u.UserName,
                    cityName = u.CityName,
                    cityId = u.CityId,
                    hasActiveShift = loc != null,
                    lastLatitude = loc?.Latitude,
                    lastLongitude = loc?.Longitude,
                    lastUpdatedAtUtc = loc?.UpdatedAtUtc,
                };
            }).ToList();
        }

        public static bool IsValidPoint(FollowerLocationPointDto point, FollowerShiftDto shift)
        {
            if (point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180) return false;
            var captured = point.CapturedAtUtc.Kind == DateTimeKind.Utc
                ? point.CapturedAtUtc
                : DateTime.SpecifyKind(point.CapturedAtUtc, DateTimeKind.Utc);
            if (captured < shift.StartedAtUtc.AddMinutes(-2)) return false;
            if (captured > FollowerIraqTime.UtcNow.AddMinutes(5)) return false;
            if (point.IsOfficial)
            {
                var slot = point.OfficialSlotUtc ?? captured;
                if (!FollowerOfficialSlot.IsExactOfficialSlot(shift.StartedAtUtc, slot)) return false;
            }

            return true;
        }
    }
}
