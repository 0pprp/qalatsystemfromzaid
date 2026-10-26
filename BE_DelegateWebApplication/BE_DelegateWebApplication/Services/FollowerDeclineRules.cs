using BE_DelegateWebApplication.Services.FollowerRating;

namespace BE_DelegateWebApplication.Services;

/// <summary>
/// Follower-only declining customers: −10 points (two bands) within 7 days on
/// ممتاز→ضعيف, جيد→متلكئ, ضعيف→سيئ.
/// </summary>
public static class FollowerDeclineRules
{
    public const int LookbackDays = 7;
    public const int MinScoreDrop = 10;

    public static bool IsTargetTransition(string? fromLabel, string? toLabel)
    {
        var from = (fromLabel ?? "").Trim();
        var to = (toLabel ?? "").Trim();
        return
            (from == FollowerCustomerRatingLabels.Excellent && to == FollowerCustomerRatingLabels.Weak)
            || (from == FollowerCustomerRatingLabels.Good && to == FollowerCustomerRatingLabels.Rejected)
            || (from == FollowerCustomerRatingLabels.Weak && to == FollowerCustomerRatingLabels.Bad);
    }

    public static bool IsDeclining(
        string? fromLabel,
        int fromScore,
        string? toLabel,
        int toScore,
        int daysBetween)
    {
        if (daysBetween < 0 || daysBetween > LookbackDays) return false;
        if (!IsTargetTransition(fromLabel, toLabel)) return false;
        return fromScore - toScore >= MinScoreDrop;
    }
}

public sealed class FollowerDecliningCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public string FromLabel { get; set; } = "";
    public string ToLabel { get; set; } = "";
    public int FromScore { get; set; }
    public int ToScore { get; set; }
    public DateTime PriorCaptureDate { get; set; }
    public DateTime CurrentCaptureDate { get; set; }
    public int DaysBetween { get; set; }
}

public static class FollowerCollectionLoginGate
{
    public const string LockedMessage =
        "الحساب غير فعال للجباية — اليوم جباية المتابع على هذه القائمة";
}
