namespace BE_Company.Sales.Services;

/// <summary>
/// Lists shown for BM branch-visit delegate ratings (0–10).
/// Same visibility as المراقب «متابعة المندوبين»: payment within last 40 days.
/// Deploy marker: BmVisitRatingLists_V1
/// </summary>
public static class BmVisitRatingListsRules
{
    public const string DeployMarker = "BmVisitRatingLists_V1";

    public readonly record struct ListRow(int Id, string Name, DateTime? LastPaymentDate);

    public static IReadOnlyList<ListRow> SelectForVisitRating(
        IEnumerable<(int Id, string Name, DateTime? LastPaymentDate)> rows,
        DateTime asOf)
    {
        return rows
            .Where(r => r.Id > 0)
            .Where(r => DirectorDelegateFollowupRules.IsListActive(r.LastPaymentDate, asOf))
            .Select(r => new ListRow(
                r.Id,
                string.IsNullOrWhiteSpace(r.Name) ? $"قائمة {r.Id}" : r.Name.Trim(),
                r.LastPaymentDate))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
