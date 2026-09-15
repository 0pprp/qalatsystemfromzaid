using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class CreateSalesExceptionInput
{
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public int? SalesRequestId { get; init; }
    public string? Reason { get; init; }
    public string? TargetApproverType { get; init; }
}

public enum SalesExceptionOutcome
{
    Success,
    Invalid,
    NotFound,
    Conflict,
    Forbidden
}

public sealed class SalesExceptionResult
{
    public required SalesExceptionOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public SalesExceptionRequest? Request { get; init; }

    public bool Ok => Outcome == SalesExceptionOutcome.Success;

    public static SalesExceptionResult Invalid(string error) =>
        new() { Outcome = SalesExceptionOutcome.Invalid, Error = error };

    public static SalesExceptionResult NotFound() =>
        new() { Outcome = SalesExceptionOutcome.NotFound, Error = "الطلب غير موجود" };

    public static SalesExceptionResult Conflict(string error) =>
        new() { Outcome = SalesExceptionOutcome.Conflict, Error = error };

    public static SalesExceptionResult Forbidden(string error) =>
        new() { Outcome = SalesExceptionOutcome.Forbidden, Error = error };

    public static SalesExceptionResult Success(SalesExceptionRequest request) =>
        new() { Outcome = SalesExceptionOutcome.Success, Request = request };
}

public sealed class SalesExceptionDetail
{
    public required SalesExceptionRequest Request { get; init; }
    public required IReadOnlyList<SalesExceptionAuditEntry> Audit { get; init; }
}

public sealed class SalesExceptionService
{
    private readonly ISalesExceptionStore _store;
    private readonly IBranchNotePoster _notePoster;

    public SalesExceptionService(ISalesExceptionStore store, IBranchNotePoster notePoster)
    {
        _store = store;
        _notePoster = notePoster;
    }

    public async Task<SalesExceptionResult> CreateAsync(
        GatewayUser requester,
        CreateSalesExceptionInput input,
        CancellationToken ct = default)
    {
        if (!SalesRoles.IsSalesManager(requester.UserType))
        {
            return SalesExceptionResult.Forbidden("طلب الاستثناء متاح لمدير المبيعات فقط");
        }

        var reason = input.Reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(reason))
        {
            return SalesExceptionResult.Invalid("سبب الاستثناء مطلوب");
        }

        var cityValue = Trimmed(input.CityValue) ?? Trimmed(requester.CityValue);
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return SalesExceptionResult.Invalid("المحافظة مطلوبة");
        }

        var target = Trimmed(input.TargetApproverType) ?? TargetApproverTypes.DelegatedManager;
        if (!string.Equals(target, TargetApproverTypes.DelegatedManager, StringComparison.Ordinal)
            && !string.Equals(target, TargetApproverTypes.BranchManager, StringComparison.Ordinal))
        {
            return SalesExceptionResult.Invalid("نوع الجهة المعتمدة غير صحيح");
        }

        var now = DateTime.UtcNow;
        var displayName = Trimmed(requester.UserName) ?? Trimmed(requester.UserID) ?? "مدير المبيعات";
        var request = new SalesExceptionRequest
        {
            Id = Guid.NewGuid(),
            CityValue = cityValue,
            CityName = Trimmed(input.CityName) ?? Trimmed(requester.CityName),
            CustomerId = input.CustomerId,
            CustomerName = Trimmed(input.CustomerName),
            CustomerPhone = Trimmed(input.CustomerPhone),
            SalesRequestId = input.SalesRequestId,
            RequestingManagerUserName = Trimmed(requester.UserName) ?? Trimmed(requester.UserID) ?? "",
            RequestingManagerDisplayName = displayName,
            Reason = reason,
            TargetApproverType = target,
            Status = ExceptionStatuses.Pending,
            RequestedAtUtc = now
        };

        var audit = new SalesExceptionAuditEntry
        {
            ActorUserName = request.RequestingManagerUserName,
            ActorDisplayName = displayName,
            ActorRole = SalesRoles.SalesManager,
            PreviousStatus = null,
            NewStatus = ExceptionStatuses.Pending,
            CityValue = cityValue,
            CreatedAtUtc = now
        };

        var created = await _store.CreateAsync(request, audit, ct);
        return SalesExceptionResult.Success(created);
    }

    public Task<PagedResult<SalesExceptionRequest>> ListAsync(SalesExceptionQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        return _store.QueryAsync(new SalesExceptionQuery
        {
            Page = page,
            PageSize = pageSize,
            Status = query.Status,
            CityValue = query.CityValue,
            TargetApproverType = query.TargetApproverType,
            RequestingManagerUserName = query.RequestingManagerUserName
        }, ct);
    }

    public async Task<SalesExceptionDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var request = await _store.GetAsync(id, ct);
        if (request is null)
        {
            return null;
        }

        return new SalesExceptionDetail
        {
            Request = request,
            Audit = await _store.GetAuditAsync(id, ct)
        };
    }

    public Task<IReadOnlyDictionary<string, int>> DashboardCountsAsync(string? cityValue, CancellationToken ct = default) =>
        _store.CountByStatusAsync(cityValue, ct);

    /// <summary>
    /// Applies Approve/Reject/Cancel. Anything already decided comes back as Conflict so the API can answer 409.
    /// </summary>
    public async Task<SalesExceptionResult> DecideAsync(
        GatewayUser actor,
        Guid id,
        string? decision,
        string? note = null,
        CancellationToken ct = default)
    {
        var newStatus = NormalizeDecision(decision);
        if (newStatus is null)
        {
            return SalesExceptionResult.Invalid("القرار غير صحيح");
        }

        var actorRole = SalesRoles.ToModuleRole(actor.UserType);
        if (!CanDecide(actorRole, newStatus))
        {
            return SalesExceptionResult.Forbidden("غير مخوّل لاتخاذ هذا القرار");
        }

        var existing = await _store.GetAsync(id, ct);
        if (existing is null)
        {
            return SalesExceptionResult.NotFound();
        }

        if (string.Equals(actorRole, SalesRoles.SalesManager, StringComparison.Ordinal)
            && !string.Equals(existing.RequestingManagerUserName, actor.UserName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return SalesExceptionResult.Forbidden("يمكن إلغاء الطلبات الخاصة بك فقط");
        }

        if (!ExceptionStateMachine.CanTransition(existing.Status, newStatus))
        {
            return SalesExceptionResult.Conflict($"لا يمكن تغيير الحالة من {existing.Status} إلى {newStatus}");
        }

        var now = DateTime.UtcNow;
        var displayName = Trimmed(actor.UserName) ?? Trimmed(actor.UserID) ?? "";
        var decided = await _store.TryDecideAsync(
            id,
            ExceptionStatuses.Pending,
            new SalesExceptionDecision
            {
                NewStatus = newStatus,
                DecisionMakerUserName = Trimmed(actor.UserName) ?? "",
                DecisionMakerDisplayName = displayName,
                DecisionNote = Trimmed(note),
                DecidedAtUtc = now
            },
            new SalesExceptionAuditEntry
            {
                ActorUserName = Trimmed(actor.UserName) ?? "",
                ActorDisplayName = displayName,
                ActorRole = actorRole ?? Trimmed(actor.UserType) ?? "Unknown",
                PreviousStatus = existing.Status,
                NewStatus = newStatus,
                DecisionNote = Trimmed(note),
                CityValue = existing.CityValue,
                CreatedAtUtc = now
            },
            ct);

        if (decided is null)
        {
            // Lost the race: another decision landed between the read and the guarded update.
            return SalesExceptionResult.Conflict("تمت معالجة الطلب مسبقاً");
        }

        if (string.Equals(newStatus, ExceptionStatuses.Approved, StringComparison.Ordinal)
            && await _notePoster.PostApprovalNoteAsync(decided, ct))
        {
            await _store.MarkBranchNotePostedAsync(decided.Id, ct);
            decided.BranchCustomerNotePosted = true;
        }

        return SalesExceptionResult.Success(decided);
    }

    private static bool CanDecide(string? actorRole, string newStatus) => actorRole switch
    {
        SalesRoles.DelegatedManager => true,
        SalesRoles.SalesManager => string.Equals(newStatus, ExceptionStatuses.Cancelled, StringComparison.Ordinal),
        _ => false
    };

    private static string? NormalizeDecision(string? decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            return null;
        }

        return decision.Trim().ToLowerInvariant() switch
        {
            "approve" or "approved" => ExceptionStatuses.Approved,
            "reject" or "rejected" => ExceptionStatuses.Rejected,
            "cancel" or "cancelled" or "canceled" => ExceptionStatuses.Cancelled,
            _ => null
        };
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
