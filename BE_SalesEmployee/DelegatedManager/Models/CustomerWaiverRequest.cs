namespace BE_SalesEmployee.DelegatedManager.Models;

/// <summary>Branch-manager waiver request (طلب تنازل) awaiting delegated-manager decision.</summary>
public sealed class CustomerWaiverRequest
{
    public Guid Id { get; set; }
    public string CityValue { get; set; } = "";
    public string? CityName { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string BranchManagerUserName { get; set; } = "";
    public string BranchManagerDisplayName { get; set; } = "";
    public int? BranchManagerUserId { get; set; }
    public string Note { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionMakerUserName { get; set; }
    public string? DecisionMakerDisplayName { get; set; }
    public string? DecisionNote { get; set; }
    public double? ApprovedAmount { get; set; }
    public bool PaymentPosted { get; set; }
    public bool AccountantNotified { get; set; }

    public CustomerWaiverRequest Clone() => (CustomerWaiverRequest)MemberwiseClone();
}

public sealed class CustomerWaiverAuditEntry
{
    public long Id { get; set; }
    public Guid WaiverId { get; set; }
    public string Action { get; set; } = "";
    public string? ActorUserName { get; set; }
    public string? ActorDisplayName { get; set; }
    public string? DetailJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public CustomerWaiverAuditEntry Clone() => (CustomerWaiverAuditEntry)MemberwiseClone();
}
