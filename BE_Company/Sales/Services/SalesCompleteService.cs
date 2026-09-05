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
        Task<SalesPreviewDocumentsResponseDTO> PreviewDocumentsAsync(
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
        private readonly ISalesInventoryService? _inventory;
        private readonly ISalesPricingService _pricing;

        public SalesCompleteService(
            ISalesCompleteRepository complete,
            ISalesDraftRepository drafts,
            ISalesDocumentService documents,
            ISalesRequestService? requests = null,
            ISalesShopProfileService? shops = null,
            ISalesInventoryService? inventory = null,
            ISalesPricingService? pricing = null)
        {
            _complete = complete;
            _drafts = drafts;
            _documents = documents;
            _requests = requests;
            _shops = shops;
            _inventory = inventory;
            _pricing = pricing ?? new SalesPricingService();
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
                if (current.Status == SalesStatuses.Rejected)
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن إتمام عملية بيع مرفوضة.");
                }

                await ApplyCheckoutAsync(current, shop, ct);

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
                DailyInstallment = tx.Sale.DailyInstallment,
                DownPayment = tx.Sale.DownPayment,
                Documents = docs.ToList()
            };
        }

        public async Task<SalesPreviewDocumentsResponseDTO> PreviewDocumentsAsync(
            int saleId,
            SalesIdentity identity,
            SalesShopCompleteDTO? shop,
            CancellationToken ct)
        {
            await _drafts.EnsureSchemaAsync(ct);
            var current = await _complete.GetOwnedSaleAsync(saleId, identity.EmployeeId, ct)
                          ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "العملية غير موجودة.");
            if (SalesCompleteRules.AlreadyCompleted(current.Status))
            {
                var docs = await _documents.EnsureGeneratedAsync(current, ct);
                return ToPreview(current, docs);
            }

            if (current.Status == SalesStatuses.Rejected)
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن إنشاء معاينة لعملية مرفوضة.");
            }

            await ApplyCheckoutAsync(current, shop, ct);
            if (_shops != null && shop != null && HasShopPayload(shop))
            {
                _shops.RequireCompletePayload(shop);
                await _shops.UpsertFromCompleteAsync(current, shop, ct);
            }

            var preview = await _documents.EnsurePreviewGeneratedAsync(current, ct);
            return ToPreview(current, preview);
        }

        private static bool HasShopPayload(SalesShopCompleteDTO shop) =>
            !string.IsNullOrWhiteSpace(shop.ShopName)
            && !string.IsNullOrWhiteSpace(shop.ShopBusinessType)
            && !string.IsNullOrWhiteSpace(shop.ShopImageKey);

        private static SalesPreviewDocumentsResponseDTO ToPreview(SalesDraftDTO sale, IReadOnlyList<SalesDocumentDTO> docs) => new()
        {
            SaleId = sale.SaleId,
            DefaultTotalSalePrice = sale.DefaultTotalSalePrice,
            DefaultDailyInstallment = sale.DefaultDailyInstallment,
            DefaultDownPayment = sale.DefaultDownPayment,
            FinalSalePrice = sale.FinalSalePrice,
            DailyInstallment = sale.DailyInstallment,
            DownPayment = sale.DownPayment,
            Documents = docs.ToList()
        };

        private async Task ApplyCheckoutAsync(SalesDraftDTO sale, SalesShopCompleteDTO? shop, CancellationToken ct)
        {
            decimal defaultTotal = sale.Items?.Sum(i => i.LineSalePrice) ?? sale.BaseSalePrice;
            decimal defaultDaily = sale.DefaultDailyInstallment > 0 ? sale.DefaultDailyInstallment : sale.DailyInstallment;
            if (_inventory != null && sale.Items != null)
            {
                defaultTotal = 0;
                defaultDaily = 0;
                foreach (var line in sale.Items)
                {
                    var product = await _inventory.GetProductAsync(line.ProductId, ct);
                    var unit = product?.SalePrice ?? line.UnitSalePrice;
                    defaultTotal += unit * line.Quantity;
                    defaultDaily += (product?.DailyInstallment ?? 0) * line.Quantity;
                }
            }

            var snapshot = _pricing.ComputeCheckout(
                defaultTotal,
                defaultDaily,
                shop?.OverrideTotalSalePrice ?? sale.OverrideTotalSalePrice,
                shop?.OverrideDailyInstallment ?? sale.OverrideDailyInstallment,
                shop?.OverrideDownPayment ?? sale.OverrideDownPayment);

            sale.BaseSalePrice = snapshot.DefaultTotalSalePrice;
            sale.DefaultTotalSalePrice = snapshot.DefaultTotalSalePrice;
            sale.DefaultDailyInstallment = snapshot.DefaultDailyInstallment;
            sale.DefaultDownPayment = snapshot.DefaultDownPayment;
            sale.OverrideTotalSalePrice = snapshot.OverrideTotalSalePrice;
            sale.OverrideDailyInstallment = snapshot.OverrideDailyInstallment;
            sale.OverrideDownPayment = snapshot.OverrideDownPayment;
            sale.FinalSalePrice = snapshot.FinalTotalSalePrice;
            sale.DailyInstallment = snapshot.FinalDailyInstallment;
            sale.DownPayment = snapshot.FinalDownPayment;
            await _drafts.UpdateCheckoutAsync(sale, ct);
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
