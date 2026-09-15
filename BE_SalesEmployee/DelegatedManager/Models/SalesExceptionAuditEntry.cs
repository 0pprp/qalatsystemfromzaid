namespace BE_SalesEmployee.DelegatedManager.Models;

/// <summary>Append-only trail for every exception status change, including creation.</summary>
public sealed class SalesExceptionAuditEntry
{
    public long Id { get; set; }
    public Guid ExceptionRequestId { get; set; }
    public string ActorUserName { get; set; } = "";
    public string ActorDisplayName { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = "";
    public string? DecisionNote { get; set; }
    public string? CityValue { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public SalesExceptionAuditEntry Clone() => (SalesExceptionAuditEntry)MemberwiseClone();
}
