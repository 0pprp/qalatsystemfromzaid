namespace BE_SalesEmployee.MainFollower.Domain;

public static class StaffTargetTypes
{
    public const string Delegate = "Delegate";
    public const string SubFollower = "SubFollower";
    public const string SalesEmployee = "SalesEmployee";

    public static string DisplayName(string? type) => type switch
    {
        Delegate => "المندوب",
        SubFollower => "المتابع الفرعي",
        SalesEmployee => "موظف المبيعات",
        _ => "الموظف"
    };
}

public static class StaffReviewRules
{
    public static string? ValidateTargetType(string? targetType)
    {
        var t = (targetType ?? "").Trim();
        if (string.Equals(t, StaffTargetTypes.Delegate, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, StaffTargetTypes.SubFollower, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, StaffTargetTypes.SalesEmployee, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return "نوع الهدف غير معروف (مندوب / متابع فرعي / موظف مبيعات).";
    }

    public static string? ValidateScoreAndNote(int score, string? note) =>
        SaleReviewRules.Validate(score, note);

    public static string NormalizeTargetType(string? targetType)
    {
        var t = (targetType ?? "").Trim();
        if (string.Equals(t, StaffTargetTypes.Delegate, StringComparison.OrdinalIgnoreCase))
        {
            return StaffTargetTypes.Delegate;
        }

        if (string.Equals(t, StaffTargetTypes.SubFollower, StringComparison.OrdinalIgnoreCase))
        {
            return StaffTargetTypes.SubFollower;
        }

        if (string.Equals(t, StaffTargetTypes.SalesEmployee, StringComparison.OrdinalIgnoreCase))
        {
            return StaffTargetTypes.SalesEmployee;
        }

        return t;
    }

    /// <summary>Durable Arabic note that appears on the target's committee feed.</summary>
    public static string BuildFeedNote(
        string committeeName,
        string targetType,
        string? targetName,
        int score,
        string? userNote,
        DateTime atUtc)
    {
        var who = string.IsNullOrWhiteSpace(targetName) ? StaffTargetTypes.DisplayName(targetType) : targetName.Trim();
        var committee = string.IsNullOrWhiteSpace(committeeName) ? "اللجنة" : committeeName.Trim();
        var iraq = atUtc.Kind == DateTimeKind.Utc ? atUtc.AddHours(3) : atUtc;
        var date = iraq.ToString("yyyy/MM/dd HH:mm");
        var body = $"اللجنة «{committee}» قيّمت {StaffTargetTypes.DisplayName(targetType)} ({who}) بتاريخ {date}: {score}/10.";
        var note = (userNote ?? "").Trim();
        if (note.Length > 0)
        {
            body += " " + note;
        }

        return body;
    }
}
