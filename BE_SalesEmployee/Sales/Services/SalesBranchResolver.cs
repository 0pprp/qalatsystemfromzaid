using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.Services;

/// <summary>
/// Canonical matching for GetAdmin cityValue / name / database → branch target.
/// Never falls back to Najaf when the key is unknown.
/// </summary>
public static class SalesBranchResolver
{
    public static bool Matches(AdminCity city, string? cityKey)
    {
        if (city is null || string.IsNullOrWhiteSpace(cityKey))
        {
            return false;
        }

        var key = cityKey.Trim();
        return string.Equals(city.Value?.Trim(), key, StringComparison.OrdinalIgnoreCase)
               || string.Equals(city.Name?.Trim(), key, StringComparison.OrdinalIgnoreCase)
               || string.Equals(city.Database?.Trim(), key, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// When <paramref name="cityKey"/> is empty → all sales branches (fan-out).
    /// When set → exact matches only (Value / Name / Database). No Najaf default.
    /// </summary>
    public static IReadOnlyList<AdminCity> ResolveTargets(
        IEnumerable<AdminCity> salesBranches,
        string? cityKey)
    {
        var list = salesBranches?.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Link)).ToList()
                   ?? [];
        if (string.IsNullOrWhiteSpace(cityKey))
        {
            return list;
        }

        return list.Where(c => Matches(c, cityKey)).ToList();
    }

    public static AdminCity? ResolveExact(IEnumerable<AdminCity> salesBranches, string? cityKey)
    {
        if (string.IsNullOrWhiteSpace(cityKey))
        {
            return null;
        }

        var matches = ResolveTargets(salesBranches, cityKey);
        if (matches.Count != 1)
        {
            return null;
        }

        return matches[0];
    }
}
