namespace BE_SalesEmployee.MainFollower.Domain;

/// <summary>
/// Sale authenticity score 1–10 (matches follower PendingSales auth).
/// Note optional when score ≥ 7; required when score ≤ 6.
/// Shop stock + daily revenue are required for store-evaluation auth.
/// </summary>
public static class SaleReviewRules
{
    public const int OptionalNoteMinScore = 7;

    public static string? Validate(int score, string? note)
    {
        if (score is < 1 or > 10)
        {
            return "التقييم يجب أن يكون من 1 إلى 10.";
        }

        var trimmed = (note ?? "").Trim();
        if (score < OptionalNoteMinScore && trimmed.Length == 0)
        {
            return "الملاحظة إجبارية عندما يكون التقييم أقل من 7.";
        }

        return null;
    }

    public static string? ValidateWithShop(
        int score,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue)
    {
        var err = Validate(score, note);
        if (err != null) return err;
        if (shopStockEstimatedValue <= 0)
        {
            return "قيمة بضاعة المحل مطلوبة ويجب أن تكون أكبر من صفر.";
        }

        if (estimatedDailyRevenue <= 0)
        {
            return "قيمة واردات المحل اليومية مطلوبة ويجب أن تكون أكبر من صفر.";
        }

        return null;
    }
}
