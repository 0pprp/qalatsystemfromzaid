using BE_Company.Sales.Authorization;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;

namespace BE_Company.Sales.Filtering
{
    public interface ISalesFilterService
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task<IReadOnlyList<SalesFilterCityDTO>> MyCitiesAsync(SalesIdentity actor, CancellationToken ct = default);
        Task<SalesFilterPagedResultDTO> ListAsync(
            SalesIdentity actor,
            string? city,
            string? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default);
        Task<SalesFilterDetailDTO> GetAsync(SalesIdentity actor, int id, CancellationToken ct = default);
        Task<SalesFilterDetailDTO> HoldAsync(SalesIdentity actor, int id, string? note, CancellationToken ct = default);
        Task<SalesFilterDetailDTO> ReadyAsync(SalesIdentity actor, int id, string? note, CancellationToken ct = default);
        Task<SalesFilterDetailDTO> RejectAsync(SalesIdentity actor, int id, string? reason, string? note, CancellationToken ct = default);
        Task ReplaceUserCitiesAsync(int userId, IReadOnlyList<(string CityValue, string? CityName)> cities, CancellationToken ct = default);
        Task ClearUserCitiesAsync(int userId, CancellationToken ct = default);
        Task<IReadOnlyList<SalesFilterCityDTO>> ListUserCitiesAsync(int userId, CancellationToken ct = default);
    }

    public sealed class SalesFilterService : ISalesFilterService
    {
        private readonly ISalesFilterRepository _repo;

        public SalesFilterService(ISalesFilterRepository repo)
        {
            _repo = repo;
        }

        public Task EnsureSchemaAsync(CancellationToken ct = default) => _repo.EnsureSchemaAsync(ct);

        public async Task<IReadOnlyList<SalesFilterCityDTO>> MyCitiesAsync(SalesIdentity actor, CancellationToken ct = default)
        {
            EnsureFilterEmployee(actor);
            if (actor.IsGateway)
            {
                // Gateway JWT already carries allowed cities; branch DB user id is not portable.
                return Array.Empty<SalesFilterCityDTO>();
            }

            return await _repo.ListUserCitiesAsync(actor.EmployeeId, ct);
        }

        public async Task<SalesFilterPagedResultDTO> ListAsync(
            SalesIdentity actor,
            string? city,
            string? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            EnsureFilterEmployee(actor);
            var filterStatus = string.IsNullOrWhiteSpace(status)
                ? SalesFilterStatuses.PendingFilter
                : status.Trim();
            if (!SalesFilterStatuses.IsKnown(filterStatus))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "حالة الفلترة غير معروفة.");
            }

            IReadOnlyList<string> allowedValues;
            if (actor.IsGateway)
            {
                // Gateway already ACL'd the cityValue; require it so we never dump the whole branch.
                if (string.IsNullOrWhiteSpace(city))
                {
                    throw new SalesCompleteException(StatusCodes.Status400BadRequest, "المحافظة مطلوبة.");
                }

                allowedValues = [city.Trim()];
            }
            else
            {
                var allowed = await _repo.ListUserCitiesAsync(actor.EmployeeId, ct);
                allowedValues = allowed.Select(c => c.CityValue).ToList();
                if (!string.IsNullOrWhiteSpace(city)
                    && !allowedValues.Any(v => string.Equals(v, city.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    throw new SalesCompleteException(StatusCodes.Status403Forbidden, "المحافظة غير مسموحة لحسابك.");
                }
            }

            var (items, total) = await _repo.ListRequestsAsync(
                allowedValues, city, filterStatus, search, page, pageSize, ct);
            return new SalesFilterPagedResultDTO
            {
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize <= 0 ? 30 : pageSize, 1, 100),
                Total = total,
                Items = items.ToList(),
            };
        }

        public async Task<SalesFilterDetailDTO> GetAsync(SalesIdentity actor, int id, CancellationToken ct = default)
        {
            EnsureFilterEmployee(actor);
            return await RequireInScope(actor, id, ct);
        }

        public Task<SalesFilterDetailDTO> HoldAsync(SalesIdentity actor, int id, string? note, CancellationToken ct = default) =>
            TransitionAsync(actor, id, SalesFilterStatuses.OnHold, SalesFilterValidation.NormalizeOptionalNote(note), null, ct);

        public Task<SalesFilterDetailDTO> ReadyAsync(SalesIdentity actor, int id, string? note, CancellationToken ct = default) =>
            TransitionAsync(actor, id, SalesFilterStatuses.ReadyForSale, SalesFilterValidation.NormalizeOptionalNote(note), null, ct);

        public Task<SalesFilterDetailDTO> RejectAsync(SalesIdentity actor, int id, string? reason, string? note, CancellationToken ct = default) =>
            TransitionAsync(
                actor,
                id,
                SalesFilterStatuses.Rejected,
                SalesFilterValidation.NormalizeOptionalNote(note),
                SalesFilterValidation.RequireRejectReason(reason),
                ct);

        public Task ReplaceUserCitiesAsync(int userId, IReadOnlyList<(string CityValue, string? CityName)> cities, CancellationToken ct = default) =>
            _repo.ReplaceUserCitiesAsync(userId, cities, ct);

        public Task ClearUserCitiesAsync(int userId, CancellationToken ct = default) =>
            _repo.ClearUserCitiesAsync(userId, ct);

        public Task<IReadOnlyList<SalesFilterCityDTO>> ListUserCitiesAsync(int userId, CancellationToken ct = default) =>
            _repo.ListUserCitiesAsync(userId, ct);

        private async Task<SalesFilterDetailDTO> TransitionAsync(
            SalesIdentity actor,
            int id,
            string newStatus,
            string? note,
            string? reason,
            CancellationToken ct)
        {
            EnsureFilterEmployee(actor);
            var row = await RequireInScope(actor, id, ct);
            if (!SalesFilterStatuses.CanTransition(row.FilterStatus, newStatus))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تحويل الطلب إلى هذه الحالة.");
            }

            // Never write a foreign-branch Users.UserID into local FKs.
            int? localUserId = actor.IsGateway || actor.EmployeeId <= 0 ? null : actor.EmployeeId;
            var actorName = string.IsNullOrWhiteSpace(actor.EmployeeName) ? null : actor.EmployeeName.Trim();

            var (ok, current) = await _repo.TryTransitionAsync(
                id, row.FilterStatus, newStatus, localUserId, actorName, note, reason, ct);
            if (!ok)
            {
                if (current == null)
                {
                    throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
                }

                throw new SalesCompleteException(
                    StatusCodes.Status409Conflict,
                    "تم تحديث الطلب من مستخدم آخر. حدّث القائمة وحاول مجددًا.");
            }

            return await RequireInScope(actor, id, ct);
        }

        private async Task<SalesFilterDetailDTO> RequireInScope(SalesIdentity actor, int id, CancellationToken ct)
        {
            var row = await _repo.GetRequestAsync(id, ct);
            if (row == null || row.TargetEmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
            }

            if (actor.IsGateway)
            {
                // City ACL is enforced by the gateway JWT + routing; hide unassigned rows only.
                return row;
            }

            if (string.IsNullOrWhiteSpace(row.CityValue)
                || !await _repo.UserHasCityAsync(actor.EmployeeId, row.CityValue, ct))
            {
                throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
            }

            return row;
        }

        private static void EnsureFilterEmployee(SalesIdentity actor)
        {
            if (!SalesRoles.IsSalesFilterEmployee(actor.UserType)
                && !string.Equals(actor.Role, SalesRoles.SalesFilterEmployee, StringComparison.Ordinal))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }
        }
    }
}
