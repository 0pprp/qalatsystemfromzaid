using BE_Company.Sales.Rating;

namespace BE_Company.Sales.Services;

/// <summary>
/// متابعة المبيعات: تقسيم تقييمات 0–10 إلى جزء ≥7 وجزء ≤6، مع حد آخر N صف.
/// Deploy marker: DirectorSalesFollowup_V10_DraftSalesOnly
/// </summary>
public static class DirectorSalesFollowupRules
{
    public const string DeployMarker = "DirectorSalesFollowup_V10_DraftSalesOnly";

    /// <summary>
    /// A completed module sale counts for the agent only while that branch sale still exists.
    /// Drafts not posted yet still count when the customer is active — they were not deleted.
    /// A stored CustomerSaleId that is gone does not count, even if the customer has another sale.
    /// </summary>
    public static bool CountsAsExistingSale(
        int? customerSaleId,
        string? postingStatus,
        bool customerExists,
        bool customerActive,
        bool linkedOfficialSaleExists,
        bool customerHasAnyOfficialSale)
    {
        if (!customerExists)
            return false;

        if (customerSaleId is > 0)
            return linkedOfficialSaleExists;

        if (customerHasAnyOfficialSale)
            return true;

        return customerActive && IsAwaitingBranchPost(postingStatus);
    }

    public static bool IsAwaitingBranchPost(string? postingStatus) =>
        string.Equals(postingStatus, "Pending", StringComparison.OrdinalIgnoreCase)
        || string.Equals(postingStatus, "Failed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(postingStatus, "Processing", StringComparison.OrdinalIgnoreCase);

    public readonly record struct BranchListRef(int ListId, string ListName, bool Active);

    /// <summary>
    /// One row per branch list: this agent's sale count and the same system average
    /// used on the employee card. Inactive lists appear only when he has sales there.
    /// Sales with no list are grouped as «بدون قائمة».
    /// </summary>
    public static IReadOnlyList<DirectorSalesFollowupListSalesDto> BuildListSales(
        IEnumerable<BranchListRef> lists,
        IReadOnlyList<CustomerRatingFacts> facts,
        DateTime asOfIraqDate)
    {
        var byList = facts
            .Where(f => f.DelegateId is int id && id > 0)
            .GroupBy(f => f.DelegateId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CustomerRatingFacts>)g.ToList());

        var known = new HashSet<int>();
        var rows = new List<DirectorSalesFollowupListSalesDto>();
        foreach (var list in lists.Where(l => l.ListId > 0).GroupBy(l => l.ListId).Select(g => g.First()))
        {
            known.Add(list.ListId);
            byList.TryGetValue(list.ListId, out var group);
            group ??= [];
            if (!list.Active && group.Count == 0)
                continue;
            rows.Add(ToListSalesRow(list.ListId, list.ListName, group, asOfIraqDate));
        }

        foreach (var pair in byList)
        {
            if (known.Contains(pair.Key))
                continue;
            rows.Add(ToListSalesRow(pair.Key, $"قائمة {pair.Key}", pair.Value, asOfIraqDate));
        }

        var unassigned = facts.Where(f => f.DelegateId is null or <= 0).ToList();
        if (unassigned.Count > 0)
            rows.Add(ToListSalesRow(0, "بدون قائمة", unassigned, asOfIraqDate));

        return rows
            .OrderByDescending(r => r.SalesCount)
            .ThenBy(r => r.ListName, StringComparer.CurrentCulture)
            .ToList();
    }

    private static DirectorSalesFollowupListSalesDto ToListSalesRow(
        int listId,
        string? listName,
        IReadOnlyList<CustomerRatingFacts> group,
        DateTime asOf)
    {
        var snap = EmployeeSalesRating.Snapshot(group, asOf, usePhantomSaleDate: false);
        var count = group.Count;
        return new DirectorSalesFollowupListSalesDto
        {
            ListId = listId,
            ListName = string.IsNullOrWhiteSpace(listName) ? $"قائمة {listId}" : listName.Trim(),
            SalesCount = count,
            AverageScore = count == 0 ? 0 : snap.AverageScore,
            RatingLabel = count == 0 ? "" : snap.RatingLabel,
        };
    }
    public const int HighMinScore = 7;
    public const int LowMaxScore = 6;
    public const int DefaultHistoryLimit = 30;
    public const int SystemAverageHistoryDays = 30;

    /// <summary>
    /// Keep the customer so the director can open the profile.
    /// List and delegate names stay off this window.
    /// </summary>
    public static DirectorSalesFollowupRatingRowDto Anonymize(DirectorSalesFollowupRatingRowDto row) =>
        new()
        {
            Score = row.Score,
            Note = row.Note,
            RatingDate = row.RatingDate,
            Kind = row.Kind,
            CustomerId = row.CustomerId is > 0 ? row.CustomerId : null,
            CustomerName = string.IsNullOrWhiteSpace(row.CustomerName) ? null : row.CustomerName.Trim(),
        };

    public static IReadOnlyList<DirectorSalesFollowupRatingRowDto> AnonymizeLowOnly(
        IEnumerable<DirectorSalesFollowupRatingRowDto> rows,
        int limit = DefaultHistoryLimit) =>
        rows
            .Where(r => IsLow(r.Score))
            .OrderByDescending(r => r.RatingDate)
            .ThenByDescending(r => r.Score)
            .Take(limit)
            .Select(Anonymize)
            .ToList();

    /// <summary>
    /// Last N calendar days (Baghdad asOf) of employee system average — newest first.
    /// </summary>
    public static IReadOnlyList<DirectorSalesFollowupSystemDayAvgDto> BuildSystemAverageHistory(
        IReadOnlyList<CustomerRatingFacts> facts,
        DateTime asOfIraqDate,
        int days = SystemAverageHistoryDays)
    {
        var result = new List<DirectorSalesFollowupSystemDayAvgDto>(days);
        for (var i = 0; i < days; i++)
        {
            var day = asOfIraqDate.Date.AddDays(-i);
            var snap = EmployeeSalesRating.Snapshot(facts, day, usePhantomSaleDate: false);
            result.Add(new DirectorSalesFollowupSystemDayAvgDto
            {
                RatingDate = day,
                AverageScore = snap.AverageScore,
                RatingLabel = snap.RatingLabel,
                CustomerCount = snap.CustomerCount,
            });
        }

        return result;
    }

    /// <summary>
    /// مصفر: بيع فعلي + الواصل يغطي سعر البيع + الباقي ≤ 0 (غير قانوني).
    /// صفوف بلا بيع (سعر/واصل/باقي = 0) ليست مصفرين.
    /// </summary>
    public static bool IsZeroedCustomer(CustomerRatingFacts f) =>
        !f.IsLegal
        && f.AmountTotalSales > 0
        && f.ReceiptsTotal >= f.AmountTotalSales
        && f.AmountRemaining <= 0;

    public static int CountZeroedCustomers(IEnumerable<CustomerRatingFacts> facts) =>
        facts.Count(IsZeroedCustomer);

    public static bool IsHigh(int score) => score >= HighMinScore;

    public static bool IsLow(int score) => score >= 0 && score <= LowMaxScore;

    public static (IReadOnlyList<T> High, IReadOnlyList<T> Low) SplitByScore<T>(
        IEnumerable<T> rows,
        Func<T, int> scoreSelector,
        int limit = DefaultHistoryLimit)
    {
        var ordered = rows
            .OrderByDescending(r => scoreSelector(r))
            .ThenByDescending(r => r)
            .ToList();

        // Prefer chronological for display: newest first when T has date — callers should pre-sort.
        var high = ordered.Where(r => IsHigh(scoreSelector(r))).Take(limit).ToList();
        var low = ordered.Where(r => IsLow(scoreSelector(r))).Take(limit).ToList();
        return (high, low);
    }

    /// <summary>Newest-first then split (stable for history rows with date).</summary>
    public static (IReadOnlyList<T> High, IReadOnlyList<T> Low) SplitNewestFirst<T>(
        IEnumerable<T> rows,
        Func<T, int> scoreSelector,
        Func<T, DateTime> dateSelector,
        int limit = DefaultHistoryLimit)
    {
        var ordered = rows
            .OrderByDescending(r => dateSelector(r))
            .ThenByDescending(r => scoreSelector(r))
            .ToList();

        var high = ordered.Where(r => IsHigh(scoreSelector(r))).Take(limit).ToList();
        var low = ordered.Where(r => IsLow(scoreSelector(r))).Take(limit).ToList();
        return (high, low);
    }
}
