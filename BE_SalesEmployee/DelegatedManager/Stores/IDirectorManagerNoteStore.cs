using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class DirectorNoteInboxQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = PagingDefaults.DefaultPageSize;
    public bool UnreadOnly { get; init; }
    public string? CityValue { get; init; }
    public string? SourceApp { get; init; }
    public string? Search { get; init; }
}

public interface IDirectorManagerNoteStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<DirectorManagerNote> CreateAsync(DirectorManagerNote note, CancellationToken ct = default);

    Task<PagedResult<DirectorManagerNote>> QueryAsync(DirectorNoteInboxQuery query, CancellationToken ct = default);

    Task<DirectorManagerNote?> GetAsync(Guid id, CancellationToken ct = default);

    Task<DirectorManagerNote?> MarkReadAsync(Guid id, DateTime readAtUtc, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(DateTime readAtUtc, CancellationToken ct = default);

    Task<int> CountUnreadAsync(CancellationToken ct = default);
}
