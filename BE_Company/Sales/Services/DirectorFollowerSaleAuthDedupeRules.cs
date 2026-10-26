namespace BE_Company.Sales.Services;

/// <summary>
/// One card per sale on the director follower sales-auth board.
/// Same customer/sale authenticated under multiple lists must not appear twice.
/// </summary>
public static class DirectorFollowerSaleAuthDedupeRules
{
    public static string SaleKey(DirectorFollowerSaleAuthRowDto row)
    {
        if (row.SaleDraftId is > 0)
            return $"SD:{row.SaleDraftId.Value}";
        if (row.CustomerSaleId is > 0)
            return $"CS:{row.CustomerSaleId.Value}";
        return $"C:{row.CustomerId}";
    }

    public static IReadOnlyList<DirectorFollowerSaleAuthRowDto> Deduplicate(
        IEnumerable<DirectorFollowerSaleAuthRowDto> rows)
    {
        return rows
            .GroupBy(SaleKey, StringComparer.Ordinal)
            .Select(g => g
                .OrderByDescending(r => r.Authenticated)
                .ThenByDescending(r => r.AuthenticatedAtUtc ?? DateTime.MinValue)
                .ThenByDescending(r => r.SaleDate ?? DateTime.MinValue)
                .First())
            .ToList();
    }
}
