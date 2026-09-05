using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public interface ISalesDocumentService
    {
        Task<IReadOnlyList<SalesDocumentDTO>> EnsureGeneratedAsync(SalesDraftDTO sale, CancellationToken ct);
        Task<IReadOnlyList<SalesDocumentDTO>> EnsurePreviewGeneratedAsync(SalesDraftDTO sale, CancellationToken ct);

        Task<(SalesDocumentRecord Record, byte[] Bytes)> ReadOwnedFileAsync(
            int saleId,
            int documentId,
            int employeeId,
            CancellationToken ct);
    }
}
