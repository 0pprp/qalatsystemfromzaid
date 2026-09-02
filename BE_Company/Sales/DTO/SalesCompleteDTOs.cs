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
        public List<SalesDocumentDTO> Documents { get; set; } = [];
    }
}
