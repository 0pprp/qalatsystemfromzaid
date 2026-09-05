namespace BE_Company.Sales.DTO
{
    public sealed class SalesShopCompleteDTO
    {
        public string? ShopName { get; set; }
        public string? ShopBusinessType { get; set; }
        public decimal ShopStockEstimatedValue { get; set; }
        public decimal EstimatedDailyRevenue { get; set; }
        public decimal ShopLength { get; set; }
        public decimal ShopWidth { get; set; }
        public decimal ShopArea { get; set; }
        public string? ShopImageKey { get; set; }
        public string? ShopImageUrl { get; set; }
        public string? EmployeeNote { get; set; }
    }

    public sealed class SalesShopProfileDTO
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string ShopBusinessType { get; set; } = string.Empty;
        public decimal ShopStockEstimatedValue { get; set; }
        public decimal EstimatedDailyRevenue { get; set; }
        public decimal ShopLength { get; set; }
        public decimal ShopWidth { get; set; }
        public decimal ShopArea { get; set; }
        public string ShopImageKey { get; set; } = string.Empty;
        public string? ShopImageUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class SalesCustomerNoteDTO
    {
        public int Id { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string AuthorRole { get; set; } = string.Empty;
        public string? AuthorName { get; set; }
        public string Note { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string? Source { get; set; }
    }

    public sealed class SalesCustomerNoteCreateDTO
    {
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Note { get; set; }
    }

    public sealed class SalesCustomerProfileDTO
    {
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public List<SalesCustomerProfileSaleDTO> Sales { get; set; } = [];
        public List<SalesShopProfileDTO> Shops { get; set; } = [];
        public SalesShopProfileDTO? LatestShop { get; set; }
        public List<SalesCustomerProfileEvaluationDTO> Evaluations { get; set; } = [];
        public List<SalesRequestDTO> SalesRequests { get; set; } = [];
        public List<SalesRequestHistoryDTO> History { get; set; } = [];
        public List<SalesCustomerNoteDTO> Notes { get; set; } = [];
    }

    public sealed class SalesCustomerProfileSaleDTO
    {
        public int SaleId { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal BaseSalePrice { get; set; }
        public decimal FinalSalePrice { get; set; }
        public decimal DailyInstallment { get; set; }
    }

    public sealed class SalesCustomerProfileEvaluationDTO
    {
        public int SaleId { get; set; }
        public int EvaluationLevel { get; set; }
        public string EvaluationName { get; set; } = string.Empty;
        public string EvaluationNote { get; set; } = string.Empty;
    }
}
