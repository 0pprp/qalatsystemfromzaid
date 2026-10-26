using BE_Company.Sales.Rating;

namespace BE_Company.Sales.Services;

/// <summary>
/// متابعة المبيعات: تقسيم تقييمات 0–10 إلى جزء ≥7 وجزء ≤6، مع حد آخر N صف.
/// Deploy marker: DirectorSalesFollowup_V6_ZeroedPaidEqualsSale
/// </summary>
public static class DirectorSalesFollowupRules
{
    public const string DeployMarker = "DirectorSalesFollowup_V6_ZeroedPaidEqualsSale";
    public const int HighMinScore = 7;
    public const int LowMaxScore = 6;
    public const int DefaultHistoryLimit = 30;
    public const int SystemAverageHistoryDays = 30;

    /// <summary>Strip customer/list identity for director board privacy (score + note + date only).</summary>
    public static DirectorSalesFollowupRatingRowDto Anonymize(DirectorSalesFollowupRatingRowDto row) =>
        new()
        {
            Score = row.Score,
            Note = row.Note,
            RatingDate = row.RatingDate,
            Kind = row.Kind,
            // Intentionally omit CustomerName / ListName / DelegateName / ids
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
