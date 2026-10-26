namespace BE_Company.Sales.Services;

/// <summary>
/// Score / count trends for director follower-ops cards (align with follower app week rules).
/// </summary>
public static class DirectorFollowerOpsTrendRules
{
    public const string Up = "up";
    public const string Down = "down";
    public const string Flat = "flat";

    public static string ComputeScoreTrend(double scoreToday, double? scorePrior)
    {
        if (scorePrior is null)
        {
            return Flat;
        }

        var delta = scoreToday - scorePrior.Value;
        if (Math.Abs(delta) < 0.01)
        {
            return Flat;
        }

        return delta > 0 ? Up : Down;
    }

    public static string ComputeCountTrend(int countToday, int? countPrior)
    {
        if (countPrior is null)
        {
            return Flat;
        }

        var delta = countToday - countPrior.Value;
        if (delta == 0)
        {
            return Flat;
        }

        return delta > 0 ? Up : Down;
    }

    public static DateTime WeekAgoDate(DateTime asOfLocal) => asOfLocal.Date.AddDays(-7);

    public static DateTime YesterdayDate(DateTime asOfLocal) => asOfLocal.Date.AddDays(-1);
}
