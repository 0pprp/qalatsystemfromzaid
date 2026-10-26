using BE_Company.Sales.Rating;

namespace BE_Company.Sales.Services;

/// <summary>
/// Same transitions as follower app: −10 pts (two bands) within 7 days —
/// ممتاز→ضعيف، جيد→متلكئ، ضعيف→سيء.
/// </summary>
public static class DirectorFollowerOpsDeclineRules
{
    public const int LookbackDays = 7;
    public const int MinScoreDrop = 10;

    public static string NormalizeBand(string? label)
    {
        var s = (label ?? "").Trim();
        if (s.Length == 0)
        {
            return "";
        }

        var cut = s.IndexOf(" - ", StringComparison.Ordinal);
        if (cut > 0)
        {
            s = s[..cut].Trim();
        }

        // Historical / polluted labels: take first token
        var sp = s.IndexOf(' ');
        if (sp > 0
            && s is not (CustomerRatingLabels.Excellent or CustomerRatingLabels.Good
                or CustomerRatingLabels.Weak or CustomerRatingLabels.Rejected
                or CustomerRatingLabels.Bad or CustomerRatingLabels.Legal))
        {
            s = s[..sp].Trim();
        }

        return s;
    }

    public static bool IsTargetTransition(string? fromLabel, string? toLabel)
    {
        var from = NormalizeBand(fromLabel);
        var to = NormalizeBand(toLabel);
        return
            (from == CustomerRatingLabels.Excellent && to == CustomerRatingLabels.Weak)
            || (from == CustomerRatingLabels.Good && to == CustomerRatingLabels.Rejected)
            || (from == CustomerRatingLabels.Weak && to == CustomerRatingLabels.Bad);
    }

    public static bool IsDeclining(
        string? fromLabel,
        int fromScore,
        string? toLabel,
        int toScore,
        int daysBetween)
    {
        if (daysBetween < 0 || daysBetween > LookbackDays)
        {
            return false;
        }

        if (!IsTargetTransition(fromLabel, toLabel))
        {
            return false;
        }

        return fromScore - toScore >= MinScoreDrop;
    }
}
