namespace BE_Company.Sales.Services;

/// <summary>
/// Red «مصادق» badge when follower or delegate sale-auth rating is at or below threshold.
/// </summary>
public static class DirectorFollowerSaleAuthRiskRules
{
    public const int DangerRatingMaxInclusive = 6;

    public static bool IsDangerAuthenticated(bool authenticated, int? authRating, int? shopRating)
    {
        if (!authenticated)
        {
            return false;
        }

        return IsLowRating(authRating) || IsLowRating(shopRating);
    }

    public static bool IsLowRating(int? rating) =>
        rating is >= 1 and <= DangerRatingMaxInclusive;
}
