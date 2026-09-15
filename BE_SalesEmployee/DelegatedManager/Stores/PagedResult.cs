namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public static PagedResult<T> Empty(int page, int pageSize) => new()
    {
        Items = Array.Empty<T>(),
        Page = page,
        PageSize = pageSize,
        TotalCount = 0
    };
}

/// <summary>Clamps caller-supplied paging so a bad client cannot pull the whole table.</summary>
public static class PagingDefaults
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1 ? DefaultPageSize : pageSize.Value;
        if (size > MaxPageSize)
        {
            size = MaxPageSize;
        }
        return (p, size);
    }
}
