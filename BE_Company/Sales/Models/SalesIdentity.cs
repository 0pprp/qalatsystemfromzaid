namespace BE_Company.Sales.Models
{
    public sealed class SalesIdentity
    {
        public int EmployeeId { get; init; }
        public string EmployeeName { get; init; } = string.Empty;
        public string BranchId { get; init; } = string.Empty;
        public string BranchName { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public string? UserType { get; init; }
        public bool? IsSalesShiftStarted { get; init; }
        /// <summary>True when authenticated via trusted BE_SalesEmployee gateway key.</summary>
        public bool IsGateway { get; init; }
        /// <summary>Home-branch user id from gateway (never used as local FK).</summary>
        public string? ExternalUserId { get; init; }
    }
}
