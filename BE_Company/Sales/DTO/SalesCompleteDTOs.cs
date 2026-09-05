namespace BE_Company.Sales.DTO
{
    public sealed class SalesDocumentDTO
    {
        public int DocumentId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class SalesCompleteResponseDTO
    {
        public int SaleId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string DocumentsStatus { get; set; } = string.Empty;
        public DateTime? CompletedAt { get; set; }
        public decimal FinalSalePrice { get; set; }
        public decimal DailyInstallment { get; set; }
        public decimal DownPayment { get; set; }
        public List<SalesDocumentDTO> Documents { get; set; } = [];
    }

    public sealed class SalesPreviewDocumentsResponseDTO
    {
        public int SaleId { get; set; }
        public decimal DefaultTotalSalePrice { get; set; }
        public decimal DefaultDailyInstallment { get; set; }
        public decimal DefaultDownPayment { get; set; }
        public decimal FinalSalePrice { get; set; }
        public decimal DailyInstallment { get; set; }
        public decimal DownPayment { get; set; }
        public List<SalesDocumentDTO> Documents { get; set; } = [];
    }
}
