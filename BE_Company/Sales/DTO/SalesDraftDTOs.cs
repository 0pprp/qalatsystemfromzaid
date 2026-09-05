namespace BE_Company.Sales.DTO
{
    public sealed class SalesCustomerSearchDTO
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public decimal SalePrice { get; set; }
        public string? SourceCityValue { get; set; }
        public string? SourceCityName { get; set; }
    }

    public sealed class SalesInventoryItemDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }
        public decimal SalePrice { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("dailyInstallment")]
        public decimal? DailyInstallment { get; set; }
        public int? StoreId { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class SalesCustomerListDTO
    {
        public int ListId { get; set; }
        public string ListName { get; set; } = string.Empty;
    }

    public sealed class SalesDraftCustomerDTO
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? NationalCardNumber { get; set; }
        public string? Address { get; set; }
        public string? NearestLandmark { get; set; }
        public string? MukhtarName { get; set; }
        public string? RationCenterNumber { get; set; }
    }

    public sealed class SalesDraftItemRequestDTO
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class SalesDraftCreateRequestDTO
    {
        public int? CustomerId { get; set; }
        public SalesDraftCustomerDTO? Customer { get; set; }
        public List<SalesDraftItemRequestDTO> Items { get; set; } = [];
        public int EvaluationLevel { get; set; }
        public string? EvaluationNote { get; set; }
        public decimal DailyInstallment { get; set; }
        public int? SalesRequestId { get; set; }
        public int? CustomerListId { get; set; }
    }

    public sealed class SalesDraftItemDTO
    {
        public int SaleItemId { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitSalePrice { get; set; }
        public decimal LineSalePrice { get; set; }
    }

    public sealed class SalesDraftDTO
    {
        public int SaleId { get; set; }
        public int EmployeeId { get; set; }
        public string? UserName { get; set; }
        public string? UserType { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string? SourceCityValue { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? NationalCardNumber { get; set; }
        public string? Address { get; set; }
        public string? NearestLandmark { get; set; }
        public string? MukhtarName { get; set; }
        public string? RationCenterNumber { get; set; }
        public int EvaluationLevel { get; set; }
        public string EvaluationNote { get; set; } = string.Empty;
        public decimal BaseSalePrice { get; set; }
        public decimal FinalSalePrice { get; set; }
        public decimal DailyInstallment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? CompletedBy { get; set; }
        public string? DocumentsStatus { get; set; }
        public int? SalesRequestId { get; set; }
        public int? CustomerListId { get; set; }
        public string? CustomerListName { get; set; }
        public List<SalesDraftItemDTO> Items { get; set; } = [];
        public List<SalesDocumentDTO> Documents { get; set; } = [];
    }
}
