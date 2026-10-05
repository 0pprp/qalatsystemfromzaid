using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class CustomerWaiverQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
    public string? Status { get; init; }
    public string? CityValue { get; init; }
    public string? BranchManagerUserName { get; init; }
}

public sealed class CustomerWaiverDecision
{
    public required string NewStatus { get; init; }
    public required string DecisionMakerUserName { get; init; }
    public required string DecisionMakerDisplayName { get; init; }
    public string? DecisionNote { get; init; }
    public double? ApprovedAmount { get; init; }
    public required DateTime DecidedAtUtc { get; init; }
}

public interface ICustomerWaiverStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<CustomerWaiverRequest> CreateAsync(
        CustomerWaiverRequest request,
        CustomerWaiverAuditEntry audit,
        CancellationToken ct = default);

    Task<PagedResult<CustomerWaiverRequest>> QueryAsync(CustomerWaiverQuery query, CancellationToken ct = default);

    Task<CustomerWaiverRequest?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerWaiverAuditEntry>> GetAuditAsync(Guid id, CancellationToken ct = default);

    Task<CustomerWaiverRequest?> TryDecideAsync(
        Guid id,
        string expectedCurrentStatus,
        CustomerWaiverDecision decision,
        CustomerWaiverAuditEntry audit,
        CancellationToken ct = default);

    Task MarkPaymentPostedAsync(Guid id, CancellationToken ct = default);

    Task MarkAccountantNotifiedAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        string? cityValue,
        string? branchManagerUserName,
        CancellationToken ct = default);
}
