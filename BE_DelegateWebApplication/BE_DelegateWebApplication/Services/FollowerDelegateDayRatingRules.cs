namespace BE_DelegateWebApplication.Services;

public static class FollowerDelegateDayRatingRules
{
    public static (bool Ok, string? Error) ValidateScore(int score)
    {
        if (score is < 0 or > 10)
            return (false, "التقييم يجب أن يكون بين 0 و 10.");
        return (true, null);
    }

    public static DateOnly IraqToday(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow.AddHours(3));
}
