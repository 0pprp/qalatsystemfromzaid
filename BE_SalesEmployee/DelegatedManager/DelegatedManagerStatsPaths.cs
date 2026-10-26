namespace BE_SalesEmployee.DelegatedManager;

/// Company paths the delegated-manager gateway proxies for ratings charts.
/// Reuses BE_Company sales-manager/stats (same BranchManagerStatsService logic).
public static class DelegatedManagerStatsPaths
{
    public const string CompanyCatalog = "sales-manager/stats/catalog";
    public const string CompanyBranchMonth = "sales-manager/stats/branch-month";

    public static string CompanyTrend(string? scope, int id, DateTime from, DateTime to)
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "branch" : scope.Trim();
        return "sales-manager/stats/trend"
            + $"?scope={Uri.EscapeDataString(normalizedScope)}"
            + $"&id={id}"
            + $"&fromDate={Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))}"
            + $"&toDate={Uri.EscapeDataString(to.ToString("yyyy-MM-dd"))}";
    }

    public static string CompanyListPaymentsSum(int listId, DateTime from, DateTime to) =>
        "sales-manager/stats/list-payments-sum"
        + $"?listId={listId}"
        + $"&fromDate={Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))}"
        + $"&toDate={Uri.EscapeDataString(to.ToString("yyyy-MM-dd"))}";

    public static string CompanyRatingCustomers(
        string? scope,
        int id,
        string? level,
        DateTime from,
        DateTime to,
        int take = 500,
        int skip = 0,
        string? q = null)
    {
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? "branch" : scope.Trim();
        var path = "sales-manager/stats/rating-customers"
            + $"?scope={Uri.EscapeDataString(normalizedScope)}"
            + $"&id={id}"
            + $"&fromDate={Uri.EscapeDataString(from.ToString("yyyy-MM-dd"))}"
            + $"&toDate={Uri.EscapeDataString(to.ToString("yyyy-MM-dd"))}"
            + $"&take={take}"
            + $"&skip={skip}";
        if (!string.IsNullOrWhiteSpace(level))
        {
            path += $"&level={Uri.EscapeDataString(level.Trim())}";
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            path += $"&q={Uri.EscapeDataString(q.Trim())}";
        }

        return path;
    }
}
