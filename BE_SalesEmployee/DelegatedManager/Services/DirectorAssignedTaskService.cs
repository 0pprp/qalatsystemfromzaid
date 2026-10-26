using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class DirectorAssignedTaskListItem
{
    public required DirectorAssignedTask Task { get; init; }
    public required bool IsOverdue { get; init; }
}

public sealed class DirectorAssignedTaskDetail
{
    public required DirectorAssignedTask Task { get; init; }
    public required bool IsOverdue { get; init; }
    public required IReadOnlyList<DirectorAssignedTaskEvent> Events { get; init; }
    public required IReadOnlyList<DirectorAssignedTaskAttachment> Attachments { get; init; }
}

public static class DirectorAssignedTaskEventTypes
{
    public const string Created = "Created";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Returned = "Returned";
    public const string Updated = "Updated";
    public const string Transferred = "Transferred";
    public const string Completed = "Completed";
    public const string Confirmed = "Confirmed";
}

public sealed class DirectorAssignedTaskService
{
    private static readonly Lazy<TimeZoneInfo> IraqTimeZone = new(ResolveIraqTimeZone);

    private readonly IDirectorAssignedTaskStore _store;
    private readonly DirectorAssignedTaskFileStorage _files;

    public DirectorAssignedTaskService(
        IDirectorAssignedTaskStore store,
        DirectorAssignedTaskFileStorage files)
    {
        _store = store;
        _files = files;
    }

    public Task EnsureSchemaAsync(CancellationToken ct) => _store.EnsureSchemaAsync(ct);

    public static DateOnly GetIraqToday()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IraqTimeZone.Value);
        return DateOnly.FromDateTime(local);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTask? Task)> CreateAsync(
        string? title,
        string? description,
        DateOnly dueDate,
        byte priority,
        string? assigneeKind,
        string? assigneeKey,
        string? assigneeDisplayName,
        string? assigneeCityValue,
        string? createdByUserId,
        string? createdByUserName,
        DateTime? dueAtUtc = null,
        int? dueHour = null,
        int? dueMinute = null,
        string? duePeriod = null,
        CancellationToken ct = default)
    {
        var (ok, error, kind, key, display) = ValidateAssignee(assigneeKind, assigneeKey, assigneeDisplayName);
        if (!ok)
            return (false, error, null);

        var trimmedTitle = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
            return (false, "عنوان المهمة مطلوب.", null);

        if (!IsValidPriority(priority))
            return (false, "أولوية غير صالحة.", null);

        var due = DirectorAssignedTaskRules.ResolveDue(dueDate, dueAtUtc, dueHour, dueMinute, duePeriod);
        if (!due.Ok)
            return (false, due.Error, null);

        var now = DateTime.UtcNow;
        var task = new DirectorAssignedTask
        {
            Id = Guid.NewGuid(),
            Title = trimmedTitle,
            Description = (description ?? "").Trim(),
            DueDate = due.DueDate,
            DueAtUtc = due.DueAtUtc,
            Priority = priority,
            Status = DirectorAssignedTaskStatus.Sent,
            CreatedByUserId = (createdByUserId ?? "").Trim(),
            CreatedByUserName = (createdByUserName ?? "").Trim(),
            AssigneeKind = kind!,
            AssigneeKey = key!,
            AssigneeDisplayName = display!,
            AssigneeCityValue = string.IsNullOrWhiteSpace(assigneeCityValue) ? null : assigneeCityValue.Trim(),
            CurrentHolderKind = kind!,
            CurrentHolderKey = key!,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        var saved = await _store.CreateAsync(task, ct);
        await _store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = saved.Id,
            EventType = DirectorAssignedTaskEventTypes.Created,
            AtUtc = now,
            ActorKind = "Director",
            ActorKey = saved.CreatedByUserId,
            ActorDisplayName = saved.CreatedByUserName,
            ActorRoleLabel = "المدير",
            TargetKind = saved.AssigneeKind,
            TargetKey = saved.AssigneeKey,
            TargetDisplayName = saved.AssigneeDisplayName,
            TargetRoleLabel = RoleLabel(saved.AssigneeKind),
        }, ct);

        return (true, null, saved);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTask? Task)> UpdateReturnedAsync(
        Guid taskId,
        string? title,
        string? description,
        DateOnly dueDate,
        byte priority,
        string? assigneeKind,
        string? assigneeKey,
        string? assigneeDisplayName,
        string? assigneeCityValue,
        string? updatedByUserId,
        string? updatedByUserName,
        DateTime? dueAtUtc = null,
        int? dueHour = null,
        int? dueMinute = null,
        string? duePeriod = null,
        CancellationToken ct = default)
    {
        var existing = await _store.GetAsync(taskId, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        if (!string.Equals(existing.Status, DirectorAssignedTaskStatus.Returned, StringComparison.Ordinal))
            return (false, "التحديث متاح فقط للمهام المعادة.", null);

        var (ok, error, kind, key, display) = ValidateAssignee(assigneeKind, assigneeKey, assigneeDisplayName);
        if (!ok)
            return (false, error, null);

        var trimmedTitle = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
            return (false, "عنوان المهمة مطلوب.", null);

        if (!IsValidPriority(priority))
            return (false, "أولوية غير صالحة.", null);

        var due = DirectorAssignedTaskRules.ResolveDue(dueDate, dueAtUtc, dueHour, dueMinute, duePeriod);
        if (!due.Ok)
            return (false, due.Error, null);

        var now = DateTime.UtcNow;
        existing.Title = trimmedTitle;
        existing.Description = (description ?? "").Trim();
        existing.DueDate = due.DueDate;
        existing.DueAtUtc = due.DueAtUtc;
        existing.Priority = priority;
        existing.AssigneeKind = kind!;
        existing.AssigneeKey = key!;
        existing.AssigneeDisplayName = display!;
        existing.AssigneeCityValue = string.IsNullOrWhiteSpace(assigneeCityValue) ? null : assigneeCityValue.Trim();
        existing.CurrentHolderKind = kind!;
        existing.CurrentHolderKey = key!;
        existing.Status = DirectorAssignedTaskStatus.Sent;
        existing.RejectNote = null;
        existing.TransferredAtUtc = null;
        existing.UpdatedAtUtc = now;

        await _store.UpdateAsync(existing, ct);
        await _store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = existing.Id,
            EventType = DirectorAssignedTaskEventTypes.Updated,
            AtUtc = now,
            ActorKind = "Director",
            ActorKey = (updatedByUserId ?? "").Trim(),
            ActorDisplayName = (updatedByUserName ?? "").Trim(),
            ActorRoleLabel = "المدير",
            TargetKind = existing.AssigneeKind,
            TargetKey = existing.AssigneeKey,
            TargetDisplayName = existing.AssigneeDisplayName,
            TargetRoleLabel = RoleLabel(existing.AssigneeKind),
            Note = "إعادة إرسال بعد التعديل",
        }, ct);

        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTask? Task)> AcceptAsync(
        Guid taskId,
        string actorKind,
        string actorKey,
        string? actorDisplayName,
        CancellationToken ct)
    {
        var existing = await _store.GetAsync(taskId, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        if (!IsCurrentHolder(existing, actorKind, actorKey))
            return (false, "لست حامل المهمة الحالي.", null);

        if (!DirectorAssignedTaskRules.CanAccept(existing.Status))
            return (false, "القبول متاح فقط للمهام المرسلة أو المنقولة.", null);

        var now = DateTime.UtcNow;
        existing.Status = DirectorAssignedTaskStatus.Accepted;
        existing.UpdatedAtUtc = now;
        await _store.UpdateAsync(existing, ct);
        await _store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = existing.Id,
            EventType = DirectorAssignedTaskEventTypes.Accepted,
            AtUtc = now,
            ActorKind = existing.CurrentHolderKind,
            ActorKey = existing.CurrentHolderKey,
            ActorDisplayName = (actorDisplayName ?? "").Trim(),
            ActorRoleLabel = RoleLabel(existing.CurrentHolderKind),
        }, ct);

        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTask? Task)> RejectAsync(
        Guid taskId,
        string actorKind,
        string actorKey,
        string? actorDisplayName,
        string? note,
        CancellationToken ct)
    {
        var existing = await _store.GetAsync(taskId, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        if (!IsCurrentHolder(existing, actorKind, actorKey))
            return (false, "لست حامل المهمة الحالي.", null);

        if (!DirectorAssignedTaskRules.CanReject(existing.Status))
            return (false, "الرفض متاح فقط للمهام المرسلة أو المنقولة.", null);

        var transition = DirectorAssignedTaskRules.Reject(note);
        if (!transition.Ok)
            return (false, transition.Error, null);

        var now = DateTime.UtcNow;
        var trimmedNote = (note ?? "").Trim();
        existing.Status = transition.NewStatus!;
        existing.RejectNote = trimmedNote;
        existing.UpdatedAtUtc = now;
        await _store.UpdateAsync(existing, ct);
        await _store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = existing.Id,
            EventType = DirectorAssignedTaskEventTypes.Rejected,
            AtUtc = now,
            ActorKind = existing.CurrentHolderKind,
            ActorKey = existing.CurrentHolderKey,
            ActorDisplayName = (actorDisplayName ?? "").Trim(),
            ActorRoleLabel = RoleLabel(existing.CurrentHolderKind),
            Note = trimmedNote,
        }, ct);

        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTask? Task)> TransferAsync(
        Guid taskId,
        string actorKind,
        string actorKey,
        string? actorDisplayName,
        string? targetKind,
        string? targetKey,
        string? targetDisplayName,
        string? note,
        CancellationToken ct)
    {
        var existing = await _store.GetAsync(taskId, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        if (!IsCurrentHolder(existing, actorKind, actorKey))
            return (false, "لست حامل المهمة الحالي.", null);

        if (!string.Equals(actorKind, DirectorAssignedTaskAssigneeKind.DelegatedManager, StringComparison.Ordinal))
            return (false, "النقل متاح للمدير المفوض فقط.", null);

        if (!string.Equals(existing.AssigneeKind, DirectorAssignedTaskAssigneeKind.DelegatedManager, StringComparison.Ordinal))
            return (false, "النقل متاح فقط للمهام المكلفة أصلاً للمفوض.", null);

        if (!string.Equals(existing.Status, DirectorAssignedTaskStatus.Accepted, StringComparison.Ordinal))
            return (false, "النقل متاح بعد قبول المهمة.", null);

        if (!string.Equals(targetKind, DirectorAssignedTaskAssigneeKind.BranchManager, StringComparison.Ordinal))
            return (false, "يجب النقل إلى إدارة فرع.", null);

        var tKey = (targetKey ?? "").Trim();
        var tDisplay = (targetDisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tKey))
            return (false, "مستخدم الفرع مطلوب.", null);

        var now = DateTime.UtcNow;
        existing.CurrentHolderKind = DirectorAssignedTaskAssigneeKind.BranchManager;
        existing.CurrentHolderKey = tKey;
        existing.Status = DirectorAssignedTaskStatus.Transferred;
        existing.TransferredAtUtc = now;
        existing.UpdatedAtUtc = now;
        await _store.UpdateAsync(existing, ct);
        await _store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = existing.Id,
            EventType = DirectorAssignedTaskEventTypes.Transferred,
            AtUtc = now,
            ActorKind = DirectorAssignedTaskAssigneeKind.DelegatedManager,
            ActorKey = (actorKey ?? "").Trim(),
            ActorDisplayName = (actorDisplayName ?? "").Trim(),
            ActorRoleLabel = RoleLabel(DirectorAssignedTaskAssigneeKind.DelegatedManager),
            TargetKind = DirectorAssignedTaskAssigneeKind.BranchManager,
            TargetKey = tKey,
            TargetDisplayName = tDisplay,
            TargetRoleLabel = RoleLabel(DirectorAssignedTaskAssigneeKind.BranchManager),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        }, ct);

        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTask? Task)> CompleteAsync(
        Guid taskId,
        string actorKind,
        string actorKey,
        string? actorDisplayName,
        CancellationToken ct)
    {
        var existing = await _store.GetAsync(taskId, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        if (!IsCurrentHolder(existing, actorKind, actorKey))
            return (false, "لست حامل المهمة الحالي.", null);

        // Active transfer only (holder is BM while original assignee is DM).
        // Historical Transferred events must not force awaiting-confirm after reject/re-send.
        var hasTransfer = HasActiveTransfer(existing);
        var transition = DirectorAssignedTaskRules.Complete(
            existing.Status,
            existing.AssigneeKind,
            hasTransfer);
        if (!transition.Ok)
            return (false, transition.Error, null);

        var now = DateTime.UtcNow;
        existing.Status = transition.NewStatus!;
        existing.UpdatedAtUtc = now;

        if (string.Equals(existing.AssigneeKind, DirectorAssignedTaskAssigneeKind.BranchManager, StringComparison.Ordinal)
            || string.Equals(existing.AssigneeKind, DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower, StringComparison.Ordinal))
        {
            existing.BranchCompletedAtUtc = now;
        }
        else if (hasTransfer)
        {
            existing.BranchCompletedAtUtc = now;
            existing.CurrentHolderKind = existing.AssigneeKind;
            existing.CurrentHolderKey = existing.AssigneeKey;
        }
        else
        {
            existing.DelegatedCompletedAtUtc = now;
        }

        await _store.UpdateAsync(existing, ct);
        await _store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = existing.Id,
            EventType = DirectorAssignedTaskEventTypes.Completed,
            AtUtc = now,
            ActorKind = (actorKind ?? "").Trim(),
            ActorKey = (actorKey ?? "").Trim(),
            ActorDisplayName = (actorDisplayName ?? "").Trim(),
            ActorRoleLabel = RoleLabel(actorKind),
        }, ct);

        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTask? Task)> ConfirmAsync(
        Guid taskId,
        string actorKind,
        string actorKey,
        string? actorDisplayName,
        CancellationToken ct)
    {
        var existing = await _store.GetAsync(taskId, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        if (!string.Equals(actorKind, DirectorAssignedTaskAssigneeKind.DelegatedManager, StringComparison.Ordinal))
            return (false, "التصديق متاح للمدير المفوض فقط.", null);

        if (!string.Equals(existing.AssigneeKind, DirectorAssignedTaskAssigneeKind.DelegatedManager, StringComparison.Ordinal)
            || !string.Equals(existing.AssigneeKey, (actorKey ?? "").Trim(), StringComparison.Ordinal))
            return (false, "التصديق متاح للمكلف الأصلي فقط.", null);

        // Confirm is only valid after BM completed under an active transfer path.
        var hasTransfer = string.Equals(
                existing.Status,
                DirectorAssignedTaskStatus.AwaitingDelegatedConfirm,
                StringComparison.Ordinal)
            || await HasTransferEventAsync(existing.Id, ct);
        var transition = DirectorAssignedTaskRules.Confirm(existing.Status, hasTransfer);
        if (!transition.Ok)
            return (false, transition.Error, null);

        var now = DateTime.UtcNow;
        existing.Status = transition.NewStatus!;
        existing.DelegatedConfirmedAtUtc = now;
        existing.UpdatedAtUtc = now;
        await _store.UpdateAsync(existing, ct);
        await _store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = existing.Id,
            EventType = DirectorAssignedTaskEventTypes.Confirmed,
            AtUtc = now,
            ActorKind = DirectorAssignedTaskAssigneeKind.DelegatedManager,
            ActorKey = (actorKey ?? "").Trim(),
            ActorDisplayName = (actorDisplayName ?? "").Trim(),
            ActorRoleLabel = RoleLabel(DirectorAssignedTaskAssigneeKind.DelegatedManager),
        }, ct);

        return (true, null, existing);
    }

    public async Task<IReadOnlyList<DirectorAssignedTaskListItem>> ListForDirectorAsync(CancellationToken ct)
    {
        var iraqToday = GetIraqToday();
        var utcNow = DateTime.UtcNow;
        var rows = await _store.ListAllSortedAsync(ct);
        return rows
            .Select(t => new DirectorAssignedTaskListItem
            {
                Task = t,
                IsOverdue = ComputeIsOverdue(t, iraqToday, utcNow),
            })
            .ToList();
    }

    public async Task<DirectorAssignedTaskDetail?> GetDetailAsync(Guid taskId, CancellationToken ct)
    {
        var task = await _store.GetAsync(taskId, ct);
        if (task is null)
            return null;

        var events = await _store.ListEventsAsync(taskId, ct);
        var attachments = await _store.ListAttachmentsAsync(taskId, ct);
        var isOverdue = ComputeIsOverdue(task, GetIraqToday(), DateTime.UtcNow);

        return new DirectorAssignedTaskDetail
        {
            Task = task,
            IsOverdue = isOverdue,
            Events = events,
            Attachments = attachments,
        };
    }

    public Task<IReadOnlyList<DirectorAssignedTaskEvent>> ListEventsAsync(Guid taskId, CancellationToken ct) =>
        _store.ListEventsAsync(taskId, ct);

    public Task<IReadOnlyList<DirectorAssignedTaskAttachment>> ListAttachmentsAsync(Guid taskId, CancellationToken ct) =>
        _store.ListAttachmentsAsync(taskId, ct);

    public async Task<IReadOnlyList<DirectorAssignedTask>> InboxForDmAsync(
        string holderKey,
        CancellationToken ct)
    {
        // Include tasks assigned to this DM even after transfer (holder may be BM).
        var rows = await _store.ListInboxForDelegatedAssigneeAsync(
            (holderKey ?? "").Trim(),
            ct);
        return FilterActiveInbox(rows);
    }

    public async Task<IReadOnlyList<DirectorAssignedTask>> InboxForBmAsync(
        string holderKey,
        CancellationToken ct,
        string? cityValue = null)
    {
        var rows = await _store.ListInboxByHolderAsync(
            DirectorAssignedTaskAssigneeKind.BranchManager,
            (holderKey ?? "").Trim(),
            ct);
        var city = (cityValue ?? "").Trim();
        if (city.Length > 0)
        {
            rows = rows
                .Where(t =>
                    string.IsNullOrWhiteSpace(t.AssigneeCityValue)
                    || string.Equals(
                        t.AssigneeCityValue.Trim(),
                        city,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return FilterActiveInbox(rows);
    }

    public async Task<(bool Ok, string? Error, DirectorAssignedTaskAttachment? Attachment)> UploadAttachmentAsync(
        Guid taskId,
        IFormFile file,
        string? uploadedBy,
        CancellationToken ct)
    {
        var existing = await _store.GetAsync(taskId, ct);
        if (existing is null)
            return (false, "المهمة غير موجودة.", null);

        var (ok, error, storedName) = await _files.SaveAsync(taskId, file, ct);
        if (!ok || string.IsNullOrWhiteSpace(storedName))
            return (false, error ?? "تعذر حفظ الملف.", null);

        var attachment = await _store.AddAttachmentAsync(new DirectorAssignedTaskAttachment
        {
            TaskId = taskId,
            FileName = Path.GetFileName(file.FileName ?? storedName),
            StoredFileName = storedName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            SizeBytes = file.Length,
            UploadedAtUtc = DateTime.UtcNow,
            UploadedBy = (uploadedBy ?? "").Trim(),
        }, ct);

        return (true, null, attachment);
    }

    public async Task<(bool Ok, string? Error, string? FullPath, string? ContentType, string? FileName)> DownloadAttachmentAsync(
        Guid taskId,
        Guid attachmentId,
        CancellationToken ct)
    {
        var attachment = await _store.GetAttachmentAsync(attachmentId, ct);
        if (attachment is null || attachment.TaskId != taskId)
            return (false, "المرفق غير موجود.", null, null, null);

        var resolved = _files.Resolve(taskId, attachment.StoredFileName);
        if (!resolved.Ok || resolved.FullPath is null)
            return (false, "ملف المرفق غير موجود على السيرفر.", null, null, null);

        return (true, null, resolved.FullPath, resolved.ContentType, attachment.FileName);
    }

    private static bool HasActiveTransfer(DirectorAssignedTask task) =>
        string.Equals(task.AssigneeKind, DirectorAssignedTaskAssigneeKind.DelegatedManager, StringComparison.Ordinal)
        && string.Equals(task.CurrentHolderKind, DirectorAssignedTaskAssigneeKind.BranchManager, StringComparison.Ordinal);

    private async Task<bool> HasTransferEventAsync(Guid taskId, CancellationToken ct)
    {
        var events = await _store.ListEventsAsync(taskId, ct);
        return events.Any(e =>
            string.Equals(e.EventType, DirectorAssignedTaskEventTypes.Transferred, StringComparison.Ordinal));
    }

    private static IReadOnlyList<DirectorAssignedTask> FilterActiveInbox(
        IReadOnlyList<DirectorAssignedTask> rows) =>
        rows.Where(t =>
                !string.Equals(t.Status, DirectorAssignedTaskStatus.Completed, StringComparison.Ordinal))
            .ToList();

    private static bool ComputeIsOverdue(DirectorAssignedTask task, DateOnly iraqToday, DateTime utcNow) =>
        !string.Equals(task.Status, DirectorAssignedTaskStatus.Completed, StringComparison.Ordinal)
        && DirectorAssignedTaskRules.IsOverdue(task.DueDate, task.DueAtUtc, iraqToday, utcNow);

    private static bool IsCurrentHolder(DirectorAssignedTask task, string actorKind, string actorKey) =>
        string.Equals(task.CurrentHolderKind, (actorKind ?? "").Trim(), StringComparison.Ordinal)
        && string.Equals(task.CurrentHolderKey, (actorKey ?? "").Trim(), StringComparison.Ordinal);

    private static (bool Ok, string? Error, string? Kind, string? Key, string? Display) ValidateAssignee(
        string? assigneeKind,
        string? assigneeKey,
        string? assigneeDisplayName)
    {
        var kind = (assigneeKind ?? "").Trim();
        var key = (assigneeKey ?? "").Trim();
        var display = (assigneeDisplayName ?? "").Trim();

        if (!string.Equals(kind, DirectorAssignedTaskAssigneeKind.DelegatedManager, StringComparison.Ordinal)
            && !string.Equals(kind, DirectorAssignedTaskAssigneeKind.BranchManager, StringComparison.Ordinal)
            && !string.Equals(kind, DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower, StringComparison.Ordinal))
            return (false, "نوع المكلف غير صالح.", null, null, null);

        if (string.IsNullOrWhiteSpace(key))
            return (false, "معرف المكلف مطلوب.", null, null, null);

        if (string.IsNullOrWhiteSpace(display))
            return (false, "اسم المكلف مطلوب.", null, null, null);

        return (true, null, kind, key, display);
    }

    private static bool IsValidPriority(byte priority) =>
        priority is DirectorAssignedTaskPriority.High
            or DirectorAssignedTaskPriority.Medium
            or DirectorAssignedTaskPriority.Low;

    private static string RoleLabel(string? kind) => kind switch
    {
        DirectorAssignedTaskAssigneeKind.DelegatedManager => "المدير المفوض",
        DirectorAssignedTaskAssigneeKind.BranchManager => "إدارة الفرع",
        DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower => "مسؤول اللجنة",
        "Director" => "المدير",
        _ => kind ?? "",
    };

    public async Task<IReadOnlyList<DirectorAssignedTask>> InboxForFollowerAsync(
        string holderKey,
        CancellationToken ct)
    {
        var rows = await _store.ListInboxByHolderAsync(
            DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower,
            (holderKey ?? "").Trim(),
            ct);
        return FilterActiveInbox(rows);
    }

    public static string FollowerAssigneeKey(string cityValue, int userId) =>
        $"{(cityValue ?? "").Trim()}|{userId}";

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
