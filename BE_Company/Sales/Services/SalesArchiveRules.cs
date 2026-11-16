namespace BE_Company.Sales.Services;

/// <summary>
/// Paper archive windows: 1–10, 11–20, and 21 through the end of the month.
/// A bundle can be sent only after that window’s last calendar day has finished (Iraq date).
/// </summary>
public static class SalesArchiveRules
{
    public const string DeployMarker = "SalesArchive_V1";
    public const int MaxDeliveryPhotos = 8;
    public const long MaxDeliveryPhotoBytes = 8_000_000;

    public const string ItemPending = "Pending";
    public const string ItemApproved = "Approved";
    public const string ItemRefused = "Refused";
    /// <summary>Sent by the sales agent to a committee member; waiting for that member to confirm receipt.</summary>
    public const string ItemToCommittee = "ToCommittee";
    /// <summary>The committee member confirmed receipt and holds the paper.</summary>
    public const string ItemWithCommittee = "WithCommittee";
    /// <summary>Handed by the committee member to the archive manager; waiting for confirmation.</summary>
    public const string ItemToArchive = "ToArchive";
    /// <summary>Refused by the archive manager; back with the committee member who sent it.</summary>
    public const string ItemArchiveRefused = "ArchiveRefused";

    public const string StageAtCommittee = "AtCommittee";
    public const string StageAtArchive = "AtArchive";

    public const string PhaseOpen = "Open";
    public const string PhaseReady = "Ready";
    public const string PhaseInTransit = "InTransit";
    public const string PhaseNeedsFix = "NeedsFix";
    public const string PhaseApproved = "Approved";

    public readonly record struct Period(DateOnly Start, DateOnly End);

    public readonly record struct ReviewDecision(int ArchiveId, bool Approved, string? Note);

    public readonly record struct ReviewUpdate(int ArchiveId, string Status, string? Note);

    public static Period PeriodFor(DateOnly saleDate)
    {
        var year = saleDate.Year;
        var month = saleDate.Month;
        var day = saleDate.Day;
        if (day <= 10)
        {
            return new Period(new DateOnly(year, month, 1), new DateOnly(year, month, 10));
        }

        if (day <= 20)
        {
            return new Period(new DateOnly(year, month, 11), new DateOnly(year, month, 20));
        }

        var last = DateTime.DaysInMonth(year, month);
        return new Period(new DateOnly(year, month, 21), new DateOnly(year, month, last));
    }

    /// <summary>True once the period’s last day is over. 10 Feb becomes sendable on 11 Feb.</summary>
    public static bool PeriodFinished(DateOnly periodEnd, DateOnly today) => today > periodEnd;

    public static DateOnly IraqToday(DateTime utcNow) =>
        DateOnly.FromDateTime(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).AddHours(3));

    public static DateOnly SaleCalendarDate(DateTime completedAt)
    {
        if (completedAt.Kind == DateTimeKind.Utc)
        {
            return DateOnly.FromDateTime(completedAt.AddHours(3));
        }

        return DateOnly.FromDateTime(completedAt);
    }

    public static string ItemLabel(IEnumerable<(string? Name, int Quantity)> lines)
    {
        var parts = new List<string>();
        foreach (var line in lines)
        {
            var name = string.IsNullOrWhiteSpace(line.Name) ? "صنف" : line.Name.Trim();
            var qty = line.Quantity < 1 ? 1 : line.Quantity;
            parts.Add(qty > 1 ? $"{name} × {qty}" : name);
        }

        if (parts.Count == 0)
        {
            return "—";
        }

        var text = string.Join("، ", parts);
        return text.Length <= 500 ? text : text[..500];
    }

    /// <summary>
    /// Phase as the sales agent sees it. <paramref name="withAgentCount"/> = papers still with the agent
    /// (never sent). Papers in the committee/archive chain count as in transit.
    /// </summary>
    public static string Phase(DateOnly periodEnd, DateOnly today, int itemCount, int approvedCount, int refusedCount, int withAgentCount)
    {
        if (itemCount <= 0 || !PeriodFinished(periodEnd, today))
        {
            return PhaseOpen;
        }

        if (approvedCount >= itemCount)
        {
            return PhaseApproved;
        }

        if (refusedCount > 0)
        {
            return PhaseNeedsFix;
        }

        return withAgentCount > 0 ? PhaseReady : PhaseInTransit;
    }

    /// <summary>Board grouping for the archive manager: who has to act on the bundle now.</summary>
    public static string Stage(string phase, int atCommitteeCount, int toArchiveCount)
    {
        if (toArchiveCount > 0)
        {
            return StageAtArchive;
        }

        if (atCommitteeCount > 0 && phase is not (PhaseOpen or PhaseApproved))
        {
            return StageAtCommittee;
        }

        return phase;
    }

    public static bool IsSendableByAgent(string status) =>
        status is ItemPending or ItemRefused;

    public static bool IsHeldByCommittee(string status) =>
        status is ItemWithCommittee or ItemArchiveRefused;

    public static bool CanSend(string phase) =>
        phase is PhaseReady or PhaseNeedsFix;

    /// <summary>Agent toggle hides bundles already handed off or fully approved.</summary>
    public static bool ShownWhenHidingSentAndApproved(string phase) =>
        phase is not (PhaseInTransit or PhaseApproved);

    public static (bool Ok, string? Error, string? Name, string? Phone) ValidateCourier(string? name, string? phone)
    {
        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length < 2)
        {
            return (false, "اسم الوسيط الذي يسلّم الأرشيف مطلوب.", null, null);
        }

        if (cleanName.Length > 200)
        {
            return (false, "اسم الوسيط طويل جداً.", null, null);
        }

        var digits = NormalizePhone(phone);
        if (digits.Length != 11 || !digits.StartsWith("07", StringComparison.Ordinal))
        {
            return (false, "رقم هاتف الوسيط يجب أن يكون 11 رقماً ويبدأ بـ 07.", null, null);
        }

        return (true, null, cleanName, digits);
    }

    /// <summary>Committee member / actor display name: trimmed, 2–200 chars, else null.</summary>
    public static string? CleanPersonName(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length < 2)
        {
            return null;
        }

        return text.Length <= 200 ? text : text[..200];
    }

    public static string? CleanOptionalNote(string? note)
    {
        var text = (note ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= 1000 ? text : text[..1000];
    }

    public static (bool Ok, string? Error) ValidateDeliveryPhotos(int count, IEnumerable<long> lengths)
    {
        if (count > MaxDeliveryPhotos)
        {
            return (false, "يمكن إرفاق 8 صور كحد أقصى.");
        }

        if (lengths.Any(length => length > MaxDeliveryPhotoBytes))
        {
            return (false, "إحدى الصور كبيرة جداً.");
        }

        return (true, null);
    }

    public static (bool Ok, string? Error, IReadOnlyList<ReviewUpdate> Updates) ApplyReview(
        IReadOnlyList<(int ArchiveId, string Status)> items,
        IReadOnlyList<ReviewDecision> decisions) =>
        ApplyReview(items, decisions, ItemPending, ItemApproved, ItemRefused);

    /// <summary>
    /// Every paper in <paramref name="waitingStatus"/> needs a decision: approved papers move to
    /// <paramref name="approvedStatus"/>, refused ones (with a note) to <paramref name="refusedStatus"/>.
    /// </summary>
    public static (bool Ok, string? Error, IReadOnlyList<ReviewUpdate> Updates) ApplyReview(
        IReadOnlyList<(int ArchiveId, string Status)> items,
        IReadOnlyList<ReviewDecision> decisions,
        string waitingStatus,
        string approvedStatus,
        string refusedStatus)
    {
        var pending = items.Where(i => i.Status == waitingStatus).ToList();
        if (pending.Count == 0)
        {
            return (false, "لا توجد أوراق بانتظار المراجعة في هذه الحزمة.", Array.Empty<ReviewUpdate>());
        }

        var map = new Dictionary<int, ReviewDecision>();
        foreach (var decision in decisions)
        {
            map[decision.ArchiveId] = decision;
        }

        if (pending.Any(p => !map.ContainsKey(p.ArchiveId)))
        {
            return (false, "يجب اعتماد أو رفض كل ورقة وصلت مع الحزمة.", Array.Empty<ReviewUpdate>());
        }

        var pendingIds = pending.Select(p => p.ArchiveId).ToHashSet();
        if (map.Keys.Any(id => !pendingIds.Contains(id)))
        {
            return (false, "أحد القرارات يخص ورقة غير معلّقة.", Array.Empty<ReviewUpdate>());
        }

        var updates = new List<ReviewUpdate>(pending.Count);
        foreach (var item in pending)
        {
            var decision = map[item.ArchiveId];
            if (decision.Approved)
            {
                updates.Add(new ReviewUpdate(item.ArchiveId, approvedStatus, null));
                continue;
            }

            var note = (decision.Note ?? string.Empty).Trim();
            if (note.Length < 2)
            {
                return (false, "اكتب ملاحظة للأوراق المرفوضة.", Array.Empty<ReviewUpdate>());
            }

            if (note.Length > 1000)
            {
                note = note[..1000];
            }

            updates.Add(new ReviewUpdate(item.ArchiveId, refusedStatus, note));
        }

        return (true, null, updates);
    }

    public static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("964", StringComparison.Ordinal) && digits.Length >= 13)
        {
            digits = "0" + digits[3..];
        }

        if (digits.Length > 11)
        {
            digits = digits[..11];
        }

        return digits;
    }
}
