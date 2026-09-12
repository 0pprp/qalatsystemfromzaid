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
        Task<IReadOnlyDictionary<string, int>> CountsAsync(SalesIdentity actor, string? city, CancellationToken ct = default);
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

            var scope = await ResolveCityScopeAsync(actor, city, ct);
            var ownByActor = !string.Equals(filterStatus, SalesFilterStatuses.PendingFilter, StringComparison.OrdinalIgnoreCase);
            var (actorUserId, actorUserName) = ActorKeys(actor);

            var (items, total) = await _repo.ListRequestsAsync(
                scope,
                filterStatus,
                search,
                page,
                pageSize,
                ownByActor,
                actorUserId,
                actorUserName,
                ct);
            return new SalesFilterPagedResultDTO
            {
                Page = Math.Max(1, page),
                PageSize = Math.Clamp(pageSize <= 0 ? 30 : pageSize, 1, 100),
                Total = total,
                Items = items.ToList(),
            };
        }

        public async Task<IReadOnlyDictionary<string, int>> CountsAsync(
            SalesIdentity actor,
            string? city,
            CancellationToken ct = default)
        {
            EnsureFilterEmployee(actor);
            var scope = await ResolveCityScopeAsync(actor, city, ct);
            var (actorUserId, actorUserName) = ActorKeys(actor);
            return await _repo.CountByStatusAsync(scope, actorUserId, actorUserName, ct);
        }

        public async Task<SalesFilterDetailDTO> GetAsync(SalesIdentity actor, int id, CancellationToken ct = default)
        {
            EnsureFilterEmployee(actor);
            return await RequireInScope(actor, id, ct);
        }

        public Task<SalesFilterDetailDTO> HoldAsync(SalesIdentity actor, int id, string? note, CancellationToken ct = default) =>
            TransitionAsync(actor, id, SalesFilterStatuses.OnHold, SalesFilterValidation.RequireHoldNote(note), null, ct);

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

        private async Task<SalesFilterCityScope> ResolveCityScopeAsync(SalesIdentity actor, string? city, CancellationToken ct)
        {
            if (actor.IsGateway)
            {
                // Trusted gateway already routed to the correct branch DB.
                // Do not filter by legacy SalesRequests.CityValue (database name vs short value vs Arabic name).
                if (string.IsNullOrWhiteSpace(city))
                {
                    throw new SalesCompleteException(StatusCodes.Status400BadRequest, "المحافظة مطلوبة.");
                }

                return SalesFilterCityScope.ForTrustedGatewayBranch(city.Trim());
            }

            var allowed = await _repo.ListUserCitiesAsync(actor.EmployeeId, ct);
            var allowedValues = allowed.Select(c => c.CityValue).ToList();
            if (!string.IsNullOrWhiteSpace(city)
                && !allowedValues.Any(v => string.Equals(v, city.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "المحافظة غير مسموحة لحسابك.");
            }

            if (!string.IsNullOrWhiteSpace(city))
            {
                allowedValues = allowedValues
                    .Where(v => string.Equals(v, city.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return SalesFilterCityScope.ForDirectEmployee(allowedValues);
        }

        private async Task<SalesFilterDetailDTO> TransitionAsync(
            SalesIdentity actor,
            int id,
            string newStatus,
            string? note,
            string? reason,
            CancellationToken ct)
        {
            EnsureFilterEmployee(actor);
            var row = await RequireInScope(actor, id, forMutation: true, ct);
            if (!SalesFilterStatuses.CanTransition(row.FilterStatus, newStatus))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تحويل الطلب إلى هذه الحالة.");
            }

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

            return await RequireInScope(actor, id, forMutation: false, ct);
        }

        private Task<SalesFilterDetailDTO> RequireInScope(SalesIdentity actor, int id, CancellationToken ct) =>
            RequireInScope(actor, id, forMutation: false, ct);

        private async Task<SalesFilterDetailDTO> RequireInScope(
            SalesIdentity actor,
            int id,
            bool forMutation,
            CancellationToken ct)
        {
            var row = await _repo.GetRequestAsync(id, ct);
            if (row == null || row.TargetEmployeeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
            }

            var isPending = string.Equals(row.FilterStatus, SalesFilterStatuses.PendingFilter, StringComparison.OrdinalIgnoreCase);

            if (!actor.IsGateway)
            {
                if (string.IsNullOrWhiteSpace(row.CityValue)
                    || !await _repo.UserHasCityAsync(actor.EmployeeId, row.CityValue, ct))
                {
                    throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
                }
            }

            // PendingFilter is a shared inbox for the city/branch.
            // Non-pending rows are owned by the actor who last set the filter status.
            // Mutations from PendingFilter (or OnHold re-hold) still require city/branch access only.
            if (!isPending && !forMutation)
            {
                if (!IsOwnedByActor(row, actor))
                {
                    throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
                }
            }
            else if (!isPending && forMutation)
            {
                // Allow transitions from OnHold only if this actor owns the hold.
                if (string.Equals(row.FilterStatus, SalesFilterStatuses.OnHold, StringComparison.OrdinalIgnoreCase)
                    && !IsOwnedByActor(row, actor))
                {
                    throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
                }
            }

            return row;
        }

        private static bool IsOwnedByActor(SalesFilterDetailDTO row, SalesIdentity actor)
        {
            var (actorUserId, actorUserName) = ActorKeys(actor);
            if (actorUserId is > 0 && row.FilteredByUserId is > 0 && row.FilteredByUserId == actorUserId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(actorUserName)
                && !string.IsNullOrWhiteSpace(row.FilteredByUserName)
                && string.Equals(actorUserName, row.FilteredByUserName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static (int? UserId, string? UserName) ActorKeys(SalesIdentity actor)
        {
            int? userId = actor.IsGateway || actor.EmployeeId <= 0 ? null : actor.EmployeeId;
            var name = string.IsNullOrWhiteSpace(actor.EmployeeName) ? null : actor.EmployeeName.Trim();
            return (userId, name);
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

    /// <summary>
    /// City scoping for list/count queries.
    /// Trusted gateway skips legacy CityValue equality; direct employees keep ACL on stored values.
    /// </summary>
    public sealed class SalesFilterCityScope
    {
        private SalesFilterCityScope(bool trustBranch, IReadOnlyList<string> cityValues, string? gatewayCityLabel)
        {
            TrustEntireBranch = trustBranch;
            CityValues = cityValues;
            GatewayCityLabel = gatewayCityLabel;
        }

        public bool TrustEntireBranch { get; }
        public IReadOnlyList<string> CityValues { get; }
        public string? GatewayCityLabel { get; }

        public static SalesFilterCityScope ForTrustedGatewayBranch(string gatewayCityValue) =>
            new(true, Array.Empty<string>(), gatewayCityValue);

        public static SalesFilterCityScope ForDirectEmployee(IReadOnlyList<string> cityValues) =>
            new(false, cityValues, null);
    }
}
