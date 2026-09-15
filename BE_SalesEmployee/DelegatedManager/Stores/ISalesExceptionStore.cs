using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class SalesExceptionQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
    public string? Status { get; init; }
    public string? CityValue { get; init; }
    public string? TargetApproverType { get; init; }
    public string? RequestingManagerUserName { get; init; }
}

public sealed class SalesExceptionDecision
{
    public required string NewStatus { get; init; }
    public required string DecisionMakerUserName { get; init; }
    public required string DecisionMakerDisplayName { get; init; }
    public string? DecisionNote { get; init; }
    public required DateTime DecidedAtUtc { get; init; }
}

public interface ISalesExceptionStore
{
    Task<SalesExceptionRequest> CreateAsync(
        SalesExceptionRequest request,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default);

    Task<PagedResult<SalesExceptionRequest>> QueryAsync(SalesExceptionQuery query, CancellationToken ct = default);

    Task<SalesExceptionRequest?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SalesExceptionAuditEntry>> GetAuditAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Optimistic decide: applies the decision only while the row is still <paramref name="expectedCurrentStatus"/>.
    /// Returns null when the row is missing or already moved on, which the caller maps to 404/409.
    /// </summary>
    Task<SalesExceptionRequest?> TryDecideAsync(
        Guid id,
        string expectedCurrentStatus,
        SalesExceptionDecision decision,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default);

    Task MarkBranchNotePostedAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(string? cityValue, CancellationToken ct = default);

    /// <summary>
    /// Active = Pending, or Approved and not yet assignment-consumed.
    /// </summary>
    Task<SalesExceptionRequest?> FindActiveBySalesRequestIdAsync(int salesRequestId, CancellationToken ct = default);

    /// <summary>
    /// Optimistic consume: succeeds only while Status=Approved and AssignmentConsumed=false.
    /// </summary>
    Task<SalesExceptionRequest?> TryConsumeAsync(
        Guid id,
        int employeeId,
        string? employeeName,
        string? assignedByManagerUserName,
        DateTime assignedAtUtc,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default);
}
