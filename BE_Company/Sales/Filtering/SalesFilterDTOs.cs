namespace BE_Company.Sales.Filtering
{
    public class SalesFilterListItemDTO
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = "";
        public string? CustomerPhone { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string? CustomerProvince { get; set; }
        public string? CustomerAddress { get; set; }
        /// <summary>شنو يريد — maps to SalesRequests.Notes</summary>
        public string? WantedDescription { get; set; }
        public string FilterStatus { get; set; } = SalesFilterStatuses.PendingFilter;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? FilteredAtUtc { get; set; }
    }

    public class SalesFilterDetailDTO : SalesFilterListItemDTO
    {
        public string? FilterNote { get; set; }
        public string? RejectReason { get; set; }
        public int? FilteredByUserId { get; set; }
        public int TargetEmployeeId { get; set; }
        public string? TargetEmployeeName { get; set; }
    }

    public sealed class SalesFilterNoteDTO
    {
        public string? Note { get; set; }
    }

    public sealed class SalesFilterRejectDTO
    {
        public string? Reason { get; set; }
        public string? Note { get; set; }
    }

    public sealed class SalesFilterCityDTO
    {
        public string CityValue { get; set; } = "";
        public string? CityName { get; set; }
    }

    public sealed class SalesFilterHistoryDTO
    {
        public int Id { get; set; }
        public int SaleRequestId { get; set; }
        public string? PreviousStatus { get; set; }
        public string NewStatus { get; set; } = "";
        public int? ChangedByUserId { get; set; }
        public string? Note { get; set; }
        public string? Reason { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public sealed class SalesFilterPagedResultDTO
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<SalesFilterListItemDTO> Items { get; set; } = [];
    }
}
