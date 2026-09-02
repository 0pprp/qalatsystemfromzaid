namespace BE_Company.Sales.DTO
{
    public sealed class SalesRequestCustomerDTO
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
    }

    public sealed class SalesRequestCreateDTO
    {
        public int TargetEmployeeId { get; set; }
        public string? TargetEmployeeName { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? CustomerSourceCityValue { get; set; }
        public SalesRequestCustomerDTO? Customer { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class SalesRequestRejectDTO
    {
        public string? Reason { get; set; }
    }

    public sealed class SalesRequestDTO
    {
        public int Id { get; set; }
        public int CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByUserType { get; set; }
        public int TargetEmployeeId { get; set; }
        public string? TargetEmployeeName { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string CustomerSourceType { get; set; } = "NewCustomer";
        public int? ExistingCustomerId { get; set; }
        public string? CustomerSourceCityValue { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerProvince { get; set; }
        public string? CustomerAddress { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "New";
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ViewedAtUtc { get; set; }
        public DateTime? ProcessingAtUtc { get; set; }
        public int? ConvertedToSaleId { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
        public List<SalesRequestTimelineItemDTO> Timeline { get; set; } = [];
    }

    public sealed class SalesRequestTimelineItemDTO
    {
        public string Event { get; set; } = string.Empty;
        public DateTime? AtUtc { get; set; }
        public string? Detail { get; set; }
    }
}
