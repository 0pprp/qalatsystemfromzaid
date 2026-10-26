namespace BE_Company.Sales.Services;

/// <summary>
/// List visibility for director/monitor «متابعة المندوبين»:
/// a list has work when any customer paid within the last 40 days.
/// Remaining bands match follower list_detail (IQD).
/// Board metrics: required due (non-zeroed excl. legal), collected revenue, last Friday.
/// </summary>
public static class DirectorDelegateFollowupRules
{
    public const int MaxDaysListActivity = 40;
    public const int CollectedHistoryDays = 30;
    public const int BoardOperatorScoreMin = 0;
    public const int BoardOperatorScoreMax = 10;

    /// <summary>Same IQD bands as <see cref="DirectorFollowerOpsService"/>.</summary>
    public const double RemainingBand100k = 100_000;
    public const double RemainingBand200k = 200_000;

    public readonly record struct RemainingBandCounts(int Lt100k, int From100To200k, int Ge100k);

    public static bool IsListActive(DateTime? lastPaymentDate, DateTime asOf)
    {
        if (lastPaymentDate is not DateTime paid)
        {
            return false;
        }

        var cutoff = asOf.Date.AddDays(-MaxDaysListActivity);
        return paid.Date >= cutoff;
    }

    /// <summary>
    /// Required daily installment cohort: open balance and not legal.
    /// </summary>
    public static bool IsRequiredDueEligible(double amountRemaining, bool isLegal) =>
        amountRemaining > 0 && !isLegal;

    public static double SumRequiredDueInstallment(
        IEnumerable<(double AmountDaySales, double AmountRemaining, bool IsLegal)> rows)
    {
        double sum = 0;
        foreach (var row in rows)
        {
            if (IsRequiredDueEligible(row.AmountRemaining, row.IsLegal))
                sum += row.AmountDaySales;
        }

        return Math.Round(sum, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Most recent Friday strictly before <paramref name="asOf"/> (Baghdad calendar date).
    /// If <paramref name="asOf"/> is Friday, returns the previous Friday.
    /// </summary>
    public static DateTime LastFridayBefore(DateTime asOf)
    {
        var d = asOf.Date;
        var daysBack = ((int)d.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        if (daysBack == 0)
            daysBack = 7;
        return d.AddDays(-daysBack);
    }

    public static IReadOnlyList<DateTime> LastNCalendarDaysEndingYesterday(DateTime asOf, int days)
    {
        if (days <= 0)
            return [];

        var yesterday = asOf.Date.AddDays(-1);
        var list = new List<DateTime>(days);
        for (var i = 0; i < days; i++)
            list.Add(yesterday.AddDays(-i));
        return list;
    }

    /// <summary>Most recent N Fridays ending at <see cref="LastFridayBefore"/> (inclusive).</summary>
    public static IReadOnlyList<DateTime> LastNFridays(DateTime asOf, int count)
    {
        if (count <= 0)
            return [];

        var friday = LastFridayBefore(asOf);
        var list = new List<DateTime>(count);
        for (var i = 0; i < count; i++)
            list.Add(friday.AddDays(-7 * i));
        return list;
    }

    /// <summary>
    /// Score tier order for conversion (higher index = better).
    /// ممتاز=4 … سيء=0. Unknown labels treated as equal (0 delta).
    /// </summary>
    public static int RatingTierRank(string? label) => label?.Trim() switch
    {
        "ممتاز" => 4,
        "جيد" => 3,
        "ضعيف" => 2,
        "متلكئ" => 1,
        "سيء" => 0,
        _ => -1
    };

    public readonly record struct ConversionCounts(int Improved, int Declined, int Unchanged);

    public const string StatusLegal = "قانونية";
    public const string StatusUnknown = "غير شيء";

    public static string NormalizeStatusLabel(string? label)
    {
        var s = (label ?? "").Trim();
        if (string.IsNullOrEmpty(s))
            return StatusUnknown;
        if (s is "ممتاز" or "جيد" or "ضعيف" or "متلكئ" or "سيء" or StatusLegal)
            return s;
        return StatusUnknown;
    }

    public static ConversionCounts CountConversions(
        IEnumerable<(string? PreviousLabel, string? CurrentLabel)> pairs)
    {
        var improved = 0;
        var declined = 0;
        var unchanged = 0;
        foreach (var (prev, cur) in pairs)
        {
            var a = RatingTierRank(prev);
            var b = RatingTierRank(cur);
            if (a < 0 || b < 0)
            {
                unchanged++;
                continue;
            }

            if (b > a)
                improved++;
            else if (b < a)
                declined++;
            else
                unchanged++;
        }

        return new ConversionCounts(improved, declined, unchanged);
    }

    /// <summary>
    /// Arrivals into each target status today vs previous day, with from-label breakdown.
    /// Only pairs where current == target and previous != target count as arrivals.
    /// </summary>
    public static Dictionary<string, List<(string From, int Count)>> BuildArrivalBreakdown(
        IEnumerable<(string? PreviousLabel, string? CurrentLabel)> pairs,
        IReadOnlyList<string> targets)
    {
        var result = targets.ToDictionary(
            t => t,
            _ => new Dictionary<string, int>(StringComparer.Ordinal));

        foreach (var (prevRaw, curRaw) in pairs)
        {
            var cur = NormalizeStatusLabel(curRaw);
            var prev = NormalizeStatusLabel(prevRaw);
            if (!result.ContainsKey(cur))
                continue;
            if (prev == cur)
                continue;
            var bag = result[cur];
            bag[prev] = bag.TryGetValue(prev, out var n) ? n + 1 : 1;
        }

        return result.ToDictionary(
            kv => kv.Key,
            kv => kv.Value
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => (From: x.Key, Count: x.Value))
                .ToList());
    }

    public static bool IsValidBoardOperatorScore(int score) =>
        score >= BoardOperatorScoreMin && score <= BoardOperatorScoreMax;

    public static bool IsRemainingLt100k(double amountRemaining) =>
        amountRemaining > 0 && amountRemaining < RemainingBand100k;

    public static bool IsRemaining100To200k(double amountRemaining) =>
        amountRemaining >= RemainingBand100k && amountRemaining < RemainingBand200k;

    public static bool IsRemainingGe100k(double amountRemaining) =>
        amountRemaining >= RemainingBand100k;

    public static RemainingBandCounts CountRemainingBands(IEnumerable<double> amountRemainings)
    {
        var lt100 = 0;
        var from100To200 = 0;
        var ge100 = 0;
        foreach (var amount in amountRemainings)
        {
            if (IsRemainingLt100k(amount))
                lt100++;
            if (IsRemaining100To200k(amount))
                from100To200++;
            if (IsRemainingGe100k(amount))
                ge100++;
        }

        return new RemainingBandCounts(lt100, from100To200, ge100);
    }
}
