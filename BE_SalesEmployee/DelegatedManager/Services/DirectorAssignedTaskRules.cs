using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Services;

public readonly record struct DirectorAssignedTaskTransitionResult(
    bool Ok,
    string? NewStatus,
    string? Error);

public readonly record struct DirectorAssignedTaskDueResolveResult(
    bool Ok,
    string? Error,
    DateOnly DueDate,
    DateTime? DueAtUtc);

public static class DirectorAssignedTaskRules
{
    private static readonly Lazy<TimeZoneInfo> IraqTimeZone = new(ResolveIraqTimeZone);

    public static int CompareSortOrder(
        byte priorityA,
        DateOnly dueDateA,
        byte priorityB,
        DateOnly dueDateB)
    {
        var byPriority = priorityA.CompareTo(priorityB);
        if (byPriority != 0)
        {
            return byPriority;
        }

        return dueDateA.CompareTo(dueDateB);
    }

    public static bool IsOverdue(DateOnly dueDate, DateOnly iraqToday)
        => dueDate < iraqToday;

    /// <summary>
    /// When <paramref name="dueAtUtc"/> is set, overdue is vs <paramref name="utcNow"/>;
    /// otherwise falls back to Iraq calendar day vs <paramref name="iraqToday"/>.
    /// </summary>
    public static bool IsOverdue(
        DateOnly dueDate,
        DateTime? dueAtUtc,
        DateOnly iraqToday,
        DateTime utcNow)
    {
        if (dueAtUtc.HasValue)
        {
            var due = dueAtUtc.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dueAtUtc.Value, DateTimeKind.Utc)
                : dueAtUtc.Value.ToUniversalTime();
            var now = utcNow.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
                : utcNow.ToUniversalTime();
            return due < now;
        }

        return IsOverdue(dueDate, iraqToday);
    }

    public static bool CanAccept(string? status) =>
        string.Equals(status, DirectorAssignedTaskStatus.Sent, StringComparison.Ordinal)
        || string.Equals(status, DirectorAssignedTaskStatus.Transferred, StringComparison.Ordinal);

    public static bool CanReject(string? status) => CanAccept(status);

    public static DirectorAssignedTaskDueResolveResult ResolveDue(
        DateOnly dueDate,
        DateTime? dueAtUtc,
        int? hour12,
        int? minute,
        string? period)
    {
        if (dueAtUtc.HasValue)
        {
            var utc = dueAtUtc.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dueAtUtc.Value, DateTimeKind.Utc)
                : dueAtUtc.Value.ToUniversalTime();
            var iraqLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, IraqTimeZone.Value);
            return new(true, null, DateOnly.FromDateTime(iraqLocal), utc);
        }

        if (hour12 is null && minute is null && string.IsNullOrWhiteSpace(period))
        {
            if (dueDate == default)
                return new(false, "تاريخ الاستحقاق مطلوب.", default, null);
            return new(true, null, dueDate, null);
        }

        if (dueDate == default)
            return new(false, "تاريخ الاستحقاق مطلوب.", default, null);

        if (hour12 is null || hour12 is < 1 or > 12)
            return new(false, "ساعة الاستحقاق يجب أن تكون بين 1 و 12.", default, null);

        if (minute is null || minute is < 0 or > 59)
            return new(false, "دقيقة الاستحقاق يجب أن تكون بين 0 و 59.", default, null);

        var periodTrim = (period ?? "").Trim();
        var isPm = string.Equals(periodTrim, "مساء", StringComparison.Ordinal)
            || string.Equals(periodTrim, "مساءً", StringComparison.OrdinalIgnoreCase)
            || string.Equals(periodTrim, "pm", StringComparison.OrdinalIgnoreCase);
        var isAm = string.Equals(periodTrim, "صباح", StringComparison.Ordinal)
            || string.Equals(periodTrim, "صباحاً", StringComparison.OrdinalIgnoreCase)
            || string.Equals(periodTrim, "صباحا", StringComparison.Ordinal)
            || string.Equals(periodTrim, "am", StringComparison.OrdinalIgnoreCase);

        if (!isPm && !isAm)
            return new(false, "فترة الاستحقاق يجب أن تكون صباح أو مساء.", default, null);

        var hour24 = hour12.Value % 12;
        if (isPm)
            hour24 += 12;

        var local = new DateTime(
            dueDate.Year,
            dueDate.Month,
            dueDate.Day,
            hour24,
            minute.Value,
            0,
            DateTimeKind.Unspecified);
        var utcFromLocal = TimeZoneInfo.ConvertTimeToUtc(local, IraqTimeZone.Value);
        return new(true, null, dueDate, utcFromLocal);
    }

    public static DirectorAssignedTaskTransitionResult Reject(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return new(false, null, "ملاحظة الرفض مطلوبة.");
        }

        return new(true, DirectorAssignedTaskStatus.Returned, null);
    }

    public static DirectorAssignedTaskTransitionResult Complete(
        string status,
        string assigneeKind,
        bool hasTransfer)
    {
        if (!string.Equals(status, DirectorAssignedTaskStatus.Accepted, StringComparison.Ordinal))
        {
            return new(false, null, "لا يمكن إنجاز المهمة في حالتها الحالية.");
        }

        if (string.Equals(assigneeKind, DirectorAssignedTaskAssigneeKind.BranchManager, StringComparison.Ordinal)
            || string.Equals(assigneeKind, DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower, StringComparison.Ordinal))
        {
            return new(true, DirectorAssignedTaskStatus.Completed, null);
        }

        if (string.Equals(assigneeKind, DirectorAssignedTaskAssigneeKind.DelegatedManager, StringComparison.Ordinal))
        {
            if (hasTransfer)
            {
                return new(true, DirectorAssignedTaskStatus.AwaitingDelegatedConfirm, null);
            }

            return new(true, DirectorAssignedTaskStatus.Completed, null);
        }

        return new(false, null, "نوع المكلف غير معروف.");
    }

    public static DirectorAssignedTaskTransitionResult Confirm(string status, bool hasTransfer)
    {
        if (!hasTransfer)
        {
            return new(false, null, "لا يمكن التصديق بدون نقل سابق.");
        }

        if (!string.Equals(status, DirectorAssignedTaskStatus.AwaitingDelegatedConfirm, StringComparison.Ordinal))
        {
            return new(false, null, "التصديق متاح فقط بعد إنجاز الفرع.");
        }

        return new(true, DirectorAssignedTaskStatus.Completed, null);
    }

    private static TimeZoneInfo ResolveIraqTimeZone()
    {
        foreach (var id in new[] { "Asia/Baghdad", "Arabic Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Iraq(+03)",
            TimeSpan.FromHours(3),
            "Iraq",
            "Iraq Standard Time");
    }
}
