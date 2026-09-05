using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services
{
    public interface ISalesCompleteService
    {
        Task<SalesCompleteResponseDTO> CompleteAsync(
            int saleId,
            SalesIdentity identity,
            SalesShopCompleteDTO? shop,
            CancellationToken ct);
        Task<IReadOnlyList<SalesDocumentDTO>> GetDocumentsAsync(int saleId, int employeeId, CancellationToken ct);
        Task<(string FileName, byte[] Bytes)> DownloadAsync(int saleId, int documentId, int employeeId, CancellationToken ct);
        Task AttachDocumentsAsync(SalesDraftDTO sale, int employeeId, CancellationToken ct);
    }

    public sealed class SalesCompleteService : ISalesCompleteService
    {
        private readonly ISalesCompleteRepository _complete;
        private readonly ISalesDraftRepository _drafts;
        private readonly ISalesDocumentService _documents;
        private readonly ISalesRequestService? _requests;
        private readonly ISalesShopProfileService? _shops;

        public SalesCompleteService(
            ISalesCompleteRepository complete,
            ISalesDraftRepository drafts,
            ISalesDocumentService documents,
            ISalesRequestService? requests = null,
            ISalesShopProfileService? shops = null)
        {
            _complete = complete;
            _drafts = drafts;
            _documents = documents;
            _requests = requests;
            _shops = shops;
        }

        public Task<SalesCompleteResponseDTO> CompleteAsync(int saleId, SalesIdentity identity, CancellationToken ct) =>
            CompleteAsync(saleId, identity, null, ct);

        public async Task<SalesCompleteResponseDTO> CompleteAsync(
            int saleId,
            SalesIdentity identity,
            SalesShopCompleteDTO? shop,
            CancellationToken ct)
        {
            await _drafts.EnsureSchemaAsync(ct);
            var current = await _complete.GetOwnedSaleAsync(saleId, identity.EmployeeId, ct);
            if (current == null)
            {
                var header = await _complete.GetSaleHeaderAsync(saleId, ct);
                if (header != null)
                {
                    throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك إتمام عملية تخص موظفاً أو فرعاً آخر.");
                }

                throw new SalesCompleteException(StatusCodes.Status404NotFound, "العملية غير موجودة.");
            }
            if (!SalesCompleteRules.AlreadyCompleted(current.Status))
            {
                if (current.Status == SalesStatuses.Rejected || SalesEvaluationLevels.BlocksSale(current.EvaluationLevel))
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن إتمام عملية بيع مرفوضة.");
                }

                var validation = SalesCompleteRules.ValidateForComplete(current);
                if (validation != null)
                {
                    throw new SalesCompleteException(StatusCodes.Status400BadRequest, validation);
                }

                if (_shops != null)
                {
                    _shops.RequireCompletePayload(shop);
                    await _shops.UpsertFromCompleteAsync(current, shop!, ct);
                }
            }

            var tx = await _complete.CompleteInTransactionAsync(saleId, identity.EmployeeId, identity.BranchId, ct);
            var documentsStatus = tx.Sale.DocumentsStatus ?? SalesStatuses.DocumentsPending;
            IReadOnlyList<SalesDocumentDTO> docs = [];
            try
            {
                docs = await _documents.EnsureGeneratedAsync(tx.Sale, ct);
                documentsStatus = docs.Count >= 2 ? SalesStatuses.DocumentsReady : SalesStatuses.DocumentsPending;
                await _complete.SetDocumentsStatusAsync(saleId, documentsStatus, ct);
            }
            catch
            {
                documentsStatus = SalesStatuses.DocumentsPending;
                await _complete.SetDocumentsStatusAsync(saleId, documentsStatus, ct);
                try
                {
                    docs = (await _complete.GetDocumentsAsync(saleId, identity.EmployeeId, ct))
                        .Select(SalesDocumentMapper.ToDto)
                        .ToList();
                }
                catch
                {
                    docs = [];
                }
            }

            try
            {
                if (_requests != null)
                {
                    await _requests.MarkCompletedBySaleIdAsync(saleId, DateTime.UtcNow, ct);
                }
            }
            catch
            {
                // Completing the sale must not roll back because request status failed.
            }

            return new SalesCompleteResponseDTO
            {
                SaleId = tx.Sale.SaleId,
                Status = SalesStatuses.Completed,
                DocumentsStatus = documentsStatus,
                CompletedAt = tx.Sale.CompletedAt,
                FinalSalePrice = tx.Sale.FinalSalePrice,
                Documents = docs.ToList()
            };
        }

        public async Task<IReadOnlyList<SalesDocumentDTO>> GetDocumentsAsync(int saleId, int employeeId, CancellationToken ct)
        {
            var sale = await _complete.GetOwnedSaleAsync(saleId, employeeId, ct)
                       ?? throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك الوصول إلى مستندات عملية لا تخصك.");
            if (SalesCompleteRules.AlreadyCompleted(sale.Status) || sale.Status == SalesStatuses.Completed)
            {
                try
                {
                    return await _documents.EnsureGeneratedAsync(sale, ct);
                }
                catch
                {
                    return (await _complete.GetDocumentsAsync(saleId, employeeId, ct)).Select(SalesDocumentMapper.ToDto).ToList();
                }
            }

            return (await _complete.GetDocumentsAsync(saleId, employeeId, ct)).Select(SalesDocumentMapper.ToDto).ToList();
        }

        public async Task<(string FileName, byte[] Bytes)> DownloadAsync(int saleId, int documentId, int employeeId, CancellationToken ct)
        {
            _ = await _complete.GetOwnedSaleAsync(saleId, employeeId, ct)
                ?? throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك تنزيل مستندات عملية لا تخصك.");
            var file = await _documents.ReadOwnedFileAsync(saleId, documentId, employeeId, ct);
            return (file.Record.FileName, file.Bytes);
        }

        public async Task AttachDocumentsAsync(SalesDraftDTO sale, int employeeId, CancellationToken ct)
        {
            try
            {
                sale.Documents = (await _complete.GetDocumentsAsync(sale.SaleId, employeeId, ct))
                    .Select(SalesDocumentMapper.ToDto)
                    .ToList();
            }
            catch (SalesCompleteException)
            {
                sale.Documents = [];
            }
        }
    }
}
