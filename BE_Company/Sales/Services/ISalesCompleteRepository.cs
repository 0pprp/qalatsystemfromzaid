using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public sealed class SalesCompleteTxResult
    {
        public required SalesDraftDTO Sale { get; init; }
        public bool InventoryDeducted { get; init; }
        public bool AlreadyCompleted { get; init; }
        public int DeductionCount { get; init; }
    }

    public sealed class SalesDocumentRecord
    {
        public int DocumentId { get; set; }
        public int SaleId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public interface ISalesCompleteRepository
    {
        Task<SalesCompleteTxResult> CompleteInTransactionAsync(
            int saleId,
            int employeeId,
            string cityValue,
            CancellationToken ct);

        Task<IReadOnlyList<SalesDocumentRecord>> GetDocumentsAsync(int saleId, int employeeId, CancellationToken ct);

        Task<SalesDocumentRecord?> GetDocumentAsync(int saleId, int documentId, int employeeId, CancellationToken ct);

        Task<SalesDocumentRecord> UpsertDocumentAsync(SalesDocumentRecord record, CancellationToken ct);

        Task SetDocumentsStatusAsync(int saleId, string documentsStatus, CancellationToken ct);

        Task<SalesDraftDTO?> GetOwnedSaleAsync(int saleId, int employeeId, CancellationToken ct);

        Task<SalesDraftDTO?> GetSaleHeaderAsync(int saleId, CancellationToken ct);
    }
}
