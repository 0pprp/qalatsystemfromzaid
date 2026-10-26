namespace BE_DelegateWebApplication.Services;

/// <summary>Week/month list score trend for follower home cards and stats.</summary>
public static class FollowerListTrendRules
{
    public const string Up = "up";
    public const string Down = "down";
    public const string Flat = "flat";

    /// <summary>Compare current average score to a prior snapshot score.</summary>
    public static string ComputeTrend(double scoreToday, double? scorePrior)
    {
        if (scorePrior is null) return Flat;
        var delta = scoreToday - scorePrior.Value;
        if (Math.Abs(delta) < 0.01) return Flat;
        return delta > 0 ? Up : Down;
    }

    public static DateTime WeekAgoDate(DateTime asOfLocal) => asOfLocal.Date.AddDays(-7);

    /// <summary>Same calendar day one month earlier (AddMonths handles short months).</summary>
    public static DateTime MonthAgoDate(DateTime asOfLocal) => asOfLocal.Date.AddMonths(-1);
}

/// <summary>DTO for follower home list cards (phantom score, unlabeled).</summary>
public sealed class FollowerListCardDto
{
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public double Score { get; set; }
    public string? RatingLabel { get; set; }
    public double? ScoreWeekAgo { get; set; }
    public string Trend { get; set; } = FollowerListTrendRules.Flat;
    public double? ScoreMonthAgo { get; set; }
    public string MonthTrend { get; set; } = FollowerListTrendRules.Flat;
    public bool FollowerCollectionEnabled { get; set; }
}

public sealed class FollowerCollectionListDto
{
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public bool FollowerCollectionEnabled { get; set; }
}
