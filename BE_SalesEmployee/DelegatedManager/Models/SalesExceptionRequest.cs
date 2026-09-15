using BE_SalesEmployee.DelegatedManager.Domain;

namespace BE_SalesEmployee.DelegatedManager.Models;

/// <summary>Sales exception raised by a branch sales manager for central approval.</summary>
public sealed class SalesExceptionRequest
{
    public Guid Id { get; set; }
    public string CityValue { get; set; } = "";
    public string? CityName { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int? SalesRequestId { get; set; }
    public string RequestingManagerUserName { get; set; } = "";
    public string RequestingManagerDisplayName { get; set; } = "";
    public string Reason { get; set; } = "";
    public string TargetApproverType { get; set; } = TargetApproverTypes.DelegatedManager;
    public string Status { get; set; } = ExceptionStatuses.Pending;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionMakerUserName { get; set; }
    public string? DecisionMakerDisplayName { get; set; }
    public string? DecisionNote { get; set; }
    public bool BranchCustomerNotePosted { get; set; }

    /// <summary>True after branch assignment successfully consumed this Approved exception.</summary>
    public bool AssignmentConsumed { get; set; }
    public int? AssignedEmployeeId { get; set; }
    public string? AssignedEmployeeName { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public string? AssignedByManagerUserName { get; set; }

    public SalesExceptionRequest Clone() => (SalesExceptionRequest)MemberwiseClone();
}
