using BE_Company.Sales.Rating;

namespace BE_Company.Sales.DTO
{
    public sealed class SalesRequestEvaluationCategoryDTO
    {
        public int ResultCount { get; set; }
        public string? WorstRatingLabel { get; set; }
        public int? WorstScore { get; set; }
        public string? WorstRatingLevel { get; set; }
    }

    public sealed class SalesRequestEvaluationSummaryDTO
    {
        public int RequestId { get; set; }
        /// <summary>Stable merge key across branches: "{sourceCity}:{requestId}".</summary>
        public string Key { get; set; } = "";
        public string? SourceCityValue { get; set; }
        public string? OverallRatingLabel { get; set; }
        public int? OverallScore { get; set; }
        public string? OverallRatingLevel { get; set; }
        public bool HasAnyMatch { get; set; }
        public SalesRequestEvaluationCategoryDTO TripleName { get; set; } = new();
        public SalesRequestEvaluationCategoryDTO Phone { get; set; } = new();
        public SalesRequestEvaluationCategoryDTO FatherGrandfather { get; set; } = new();
    }

    public sealed class SalesRequestEvaluationBatchRequestDTO
    {
        public List<int> RequestIds { get; set; } = [];
        /// <summary>Preferred for cross-branch gateway merge (name/phone payloads).</summary>
        public List<SalesRequestEvaluationPayloadDTO> Items { get; set; } = [];
    }

    public sealed class SalesRequestEvaluationPayloadDTO
    {
        public int RequestId { get; set; }
        public string? SourceCityValue { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string Key =>
            $"{(SourceCityValue ?? "").Trim()}:{RequestId}";
    }

    public sealed class SalesRequestEvaluationBatchResultDTO
    {
        public List<SalesRequestEvaluationSummaryDTO> Items { get; set; } = [];
    }

    public sealed class SalesRequestEvaluationHitsRequestDTO
    {
        public int RequestId { get; set; }
        public string? SourceCityValue { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string Category { get; set; } = "";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }

    public sealed class SalesRequestProvinceTransferDTO
    {
        public string ToCityValue { get; set; } = "";
        public string? ToCityName { get; set; }
        public int? ToEmployeeId { get; set; }
        public string? ToEmployeeName { get; set; }
    }

    public sealed class SalesRequestAcceptTransferDTO
    {
        public int FromRequestId { get; set; }
        public string FromCityValue { get; set; } = "";
        public string? FromCityName { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerProvince { get; set; }
        public string? CustomerAddress { get; set; }
        public string? Notes { get; set; }
        public string? CustomerSourceType { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? CustomerSourceCityValue { get; set; }
        public string? SaleRequestType { get; set; }
        public int? SourceListId { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByUserType { get; set; }
        public int? ToEmployeeId { get; set; }
        public string? ToEmployeeName { get; set; }
        public string? CityName { get; set; }
    }

    public sealed class SalesRequestMarkTransferredOutDTO
    {
        public string ToCityValue { get; set; } = "";
        public int ToRequestId { get; set; }
        public string? ToCityName { get; set; }
    }

    public sealed class SalesRequestEvaluationHitDTO
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string RatingLabel { get; set; } = "";
        public int Score { get; set; }
        public string MatchReason { get; set; } = "";
        public DateTime? DateSaleDevice { get; set; }
        public int? DaysToSettle { get; set; }
        public int? DaysSinceSale { get; set; }
        public double AmountTotalSales { get; set; }
        public double ReceiptsTotal { get; set; }
        public double AmountRemaining { get; set; }
        public int ReceiptCount { get; set; }
        public bool IsLegal { get; set; }
        public bool IsSettled { get; set; }
    }

    public sealed class SalesRequestEvaluationHitsPageDTO
    {
        public int RequestId { get; set; }
        public string Category { get; set; } = "";
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<SalesRequestEvaluationHitDTO> Items { get; set; } = [];
    }

    public sealed class SalesRequestManagerUpdateDTO
    {
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string? SaleType { get; set; }
    }

    public sealed class SalesRequestManagerReassignDTO
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
    }
}
