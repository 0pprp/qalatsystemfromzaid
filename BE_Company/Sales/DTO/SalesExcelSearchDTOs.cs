namespace BE_Company.Sales.DTO
{
    public sealed class SalesExcelSearchRequestDTO
    {
        public List<string> Names { get; set; } = [];
        public string? CityValue { get; set; }
    }

    public sealed class SalesExcelSearchResponseDTO
    {
        public bool ReadOnly { get; set; } = true;
        public int NameCount { get; set; }
        public int FoundCount { get; set; }
        public int MissingCount { get; set; }
        public List<SalesExcelSearchQueryDTO> Queries { get; set; } = [];
    }

    public sealed class SalesExcelSearchQueryDTO
    {
        public int RowNumber { get; set; }
        public string RequestedName { get; set; } = string.Empty;
        public string SearchKey { get; set; } = string.Empty;
        public bool UsedFamilySearch { get; set; }
        public bool Found { get; set; }
        public int MatchCount { get; set; }
        public bool Truncated { get; set; }
        public string? Warning { get; set; }
        public List<SalesExcelSearchMatchDTO> Matches { get; set; } = [];
    }

    public sealed class SalesExcelSearchMatchDTO
    {
        public string ResultKey { get; set; } = string.Empty;
        public string CityValue { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? DelegateName { get; set; }
        public int? DelegateId { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
        public List<SalesExcelSearchSaleDTO> Sales { get; set; } = [];
    }

    public sealed class SalesExcelSearchSaleDTO
    {
        public int SaleId { get; set; }
        public DateTime? SaleDate { get; set; }
        public double? SaleAmount { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
        public bool? AccountZero { get; set; }
    }

    public sealed class SalesExcelSearchCustomerRow
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? DelegateName { get; set; }
        public int? DelegateId { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
    }

    public sealed class SalesExcelSearchSaleRow
    {
        public int CustomerId { get; set; }
        public int SaleId { get; set; }
        public DateTime? SaleDate { get; set; }
        public double? SaleAmount { get; set; }
        public bool? AccountZero { get; set; }
    }
}
