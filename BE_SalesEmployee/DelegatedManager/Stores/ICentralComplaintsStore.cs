using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class ComplaintInboxQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
    public bool UnreadOnly { get; init; }
    public string? CityValue { get; init; }
    public string? SourceApp { get; init; }
    public string? Search { get; init; }
}

public interface ICentralComplaintsStore
{
    Task<CentralComplaint> CreateAsync(CentralComplaint complaint, CancellationToken ct = default);

    Task<PagedResult<CentralComplaint>> QueryAsync(ComplaintInboxQuery query, CancellationToken ct = default);

    Task<CentralComplaint?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the complaint after marking it read, or null when it does not exist.</summary>
    Task<CentralComplaint?> MarkReadAsync(Guid id, DateTime readAtUtc, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(DateTime readAtUtc, CancellationToken ct = default);

    Task<int> CountUnreadAsync(CancellationToken ct = default);
}
