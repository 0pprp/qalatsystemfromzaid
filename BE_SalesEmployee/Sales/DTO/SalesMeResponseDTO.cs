namespace BE_SalesEmployee.Sales.DTO
{
    public sealed class SalesMeResponseDTO
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string BranchId { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool? IsSalesShiftStarted { get; set; }
    }
}
