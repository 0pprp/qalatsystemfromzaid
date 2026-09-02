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
    }
}
