namespace BE_SalesEmployee.DelegatedManager.Models;

public sealed class DirectorAssignedTask
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    /// <summary>Long free-text body sent with the task (NVARCHAR(MAX)).</summary>
    public string Description { get; set; } = "";
    public DateOnly DueDate { get; set; }
    /// <summary>Exact due instant (UTC). When set, overdue uses this vs UtcNow; DueDate is the Iraq calendar day.</summary>
    public DateTime? DueAtUtc { get; set; }
    public byte Priority { get; set; } = DirectorAssignedTaskPriority.Medium;
    public string Status { get; set; } = DirectorAssignedTaskStatus.Sent;
    public string CreatedByUserId { get; set; } = "";
    public string CreatedByUserName { get; set; } = "";
    public string AssigneeKind { get; set; } = "";
    public string AssigneeKey { get; set; } = "";
    public string AssigneeDisplayName { get; set; } = "";
    public string? AssigneeCityValue { get; set; }
    public string? RejectNote { get; set; }
    public string CurrentHolderKind { get; set; } = "";
    public string CurrentHolderKey { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? TransferredAtUtc { get; set; }
    public DateTime? BranchCompletedAtUtc { get; set; }
    public DateTime? DelegatedCompletedAtUtc { get; set; }
    public DateTime? DelegatedConfirmedAtUtc { get; set; }

    public DirectorAssignedTask Clone() => (DirectorAssignedTask)MemberwiseClone();
}

public sealed class DirectorAssignedTaskAttachment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public string UploadedBy { get; set; } = "";

    public DirectorAssignedTaskAttachment Clone() => (DirectorAssignedTaskAttachment)MemberwiseClone();
}

public sealed class DirectorAssignedTaskEvent
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string EventType { get; set; } = "";
    public DateTime AtUtc { get; set; }
    public string? ActorKind { get; set; }
    public string? ActorKey { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? ActorRoleLabel { get; set; }
    public string? TargetKind { get; set; }
    public string? TargetKey { get; set; }
    public string? TargetDisplayName { get; set; }
    public string? TargetRoleLabel { get; set; }
    public string? Note { get; set; }

    public DirectorAssignedTaskEvent Clone() => (DirectorAssignedTaskEvent)MemberwiseClone();
}
