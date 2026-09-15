namespace BE_Company.Sales.DTO
{
    /// <summary>Branch-side payload for Delegated Manager exception review enrichment.</summary>
    public sealed class ExceptionReviewContextDTO
    {
        public ExceptionReviewClassificationDTO CustomerClassification { get; set; } = new();
        public ExceptionReviewCurrentRequestDTO? CurrentRequest { get; set; }
        public ExceptionReviewSourceDTO Source { get; set; } = new();
        public List<ExceptionReviewMatchCustomerDTO> MatchingCustomers { get; set; } = [];
    }

    public sealed class ExceptionReviewClassificationDTO
    {
        public string Type { get; set; } = "New"; // New | Existing
        public string LabelArabic { get; set; } = "زبون جديد";
        public int MatchCount { get; set; }
        public string ExplanationArabic { get; set; } = string.Empty;
    }

    public sealed class ExceptionReviewCurrentRequestDTO
    {
        public int SalesRequestId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string? CustomerProvince { get; set; }
        public string? CustomerAddress { get; set; }
        public string? Occupation { get; set; }
        public string? Notes { get; set; }
        public string? SaleTypeOrProduct { get; set; }
        public string? SaleRequestType { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByUserType { get; set; }
        public string? CustomerSourceType { get; set; }
        public int? SourceListId { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? TargetEmployeeName { get; set; }
        public string? PendingNote { get; set; }
        public string? PreparedForSaleNote { get; set; }
    }

    public sealed class ExceptionReviewSourceDTO
    {
        public string Type { get; set; } = string.Empty;
        public string DisplayLabel { get; set; } = "غير متوفر";
        public string PersonName { get; set; } = "غير متوفر";
        public string? ListName { get; set; }
        public string BranchName { get; set; } = "غير متوفر";
    }

    public sealed class ExceptionReviewMatchCustomerDTO
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? Occupation { get; set; }
        public List<string> MatchReasons { get; set; } = [];
        public string? RatingLabel { get; set; }
        public int? RatingScore { get; set; }
        public bool IsLegal { get; set; }
        public ExceptionReviewFinancialSummaryDTO FinancialSummary { get; set; } = new();
        public List<ExceptionReviewPreviousSaleDTO> PreviousSales { get; set; } = [];
    }

    public sealed class ExceptionReviewFinancialSummaryDTO
    {
        public double TotalSales { get; set; }
        public double TotalPaid { get; set; }
        public double Remaining { get; set; }
        public double CurrentDebt { get; set; }
        public DateTime? LastSaleDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public bool FullyPaid { get; set; }
        public string? LegalStatus { get; set; }
        public int ReceiptCount { get; set; }
    }

    public sealed class ExceptionReviewPreviousSaleDTO
    {
        public int SaleId { get; set; }
        public DateTime? SaleDate { get; set; }
        public double? SaleAmount { get; set; }
        public bool? AccountZero { get; set; }
        public string? BranchName { get; set; }
    }

    /// <summary>Candidate row for pure matching (unit tests + service).</summary>
    public sealed class ExceptionReviewCandidate
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        /// <summary>Authoritative branch key; null/blank = unknown → excluded unless stamped by branch catalog.</summary>
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? Occupation { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
    }

    public sealed class ExceptionReviewMatchHit
    {
        public ExceptionReviewCandidate Customer { get; set; } = new();
        public bool PhoneMatch { get; set; }
        public bool TripleNameMatch { get; set; }
        public List<string> MatchReasons { get; set; } = [];
    }
}
