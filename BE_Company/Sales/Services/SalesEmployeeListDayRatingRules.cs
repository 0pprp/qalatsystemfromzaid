namespace BE_Company.Sales.Services;

/// <summary>Deploy marker: SalesEmployeeListDayRating_V1 — same 0–10 scale as follower.</summary>
public static class SalesEmployeeListDayRatingRules
{
    public const string DeployMarker = "SalesEmployeeListDayRating_V1";

    public static (bool Ok, string? Error) ValidateScore(int score)
    {
        if (score is < 0 or > 10)
            return (false, "التقييم يجب أن يكون بين 0 و 10.");
        return (true, null);
    }

    public static DateOnly IraqToday(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow.AddHours(3));
}
