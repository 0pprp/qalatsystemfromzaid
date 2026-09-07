using BE_Company.DTO;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services
{
    public interface ISalesPurchaseService
    {
        Task<SalesPurchaseLookupsDTO> GetLookupsAsync(CancellationToken ct);
        Task<IReadOnlyList<SalesPurchaseItemOptionDTO>> GetItemsAsync(int storeId, string? search, CancellationToken ct);
        Task<IReadOnlyList<SalesPurchaseListItemDTO>> ListAsync(DateTime? fromDate, DateTime? toDate, string? textSearch, CancellationToken ct);
        Task<SalesPurchaseListItemDTO> CreateAsync(SalesIdentity identity, SalesPurchaseCreateDTO request, CancellationToken ct);
    }

    public sealed class SalesPurchaseService : ISalesPurchaseService
    {
        private readonly ISalesPurchaseRepository _purchases;

        public SalesPurchaseService(ISalesPurchaseRepository purchases)
        {
            _purchases = purchases;
        }

        public Task<SalesPurchaseLookupsDTO> GetLookupsAsync(CancellationToken ct) =>
            _purchases.GetLookupsAsync(ct);

        public Task<IReadOnlyList<SalesPurchaseItemOptionDTO>> GetItemsAsync(int storeId, string? search, CancellationToken ct)
        {
            if (storeId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب تحديد المخزن.");
            }

            return _purchases.GetItemsAsync(storeId, search, ct);
        }

        public Task<IReadOnlyList<SalesPurchaseListItemDTO>> ListAsync(
            DateTime? fromDate,
            DateTime? toDate,
            string? textSearch,
            CancellationToken ct) =>
            _purchases.ListAsync(fromDate, toDate, textSearch, ct);

        public async Task<SalesPurchaseListItemDTO> CreateAsync(
            SalesIdentity identity,
            SalesPurchaseCreateDTO request,
            CancellationToken ct)
        {
            if (identity.Role != SalesRoles.SalesManager)
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك إدخال فاتورة شراء للمخزن.");
            }

            var invoice = SalesPurchaseRules.NormalizeInvoiceNumber(request.SupplierInvoiceNumber);
            if (!SalesPurchaseRules.HasInvoiceNumber(invoice))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "رقم فاتورة المورد مطلوب لمنع تكرار الإدخال.");
            }

            if (request.SupplierId <= 0 || request.StoreId <= 0 || request.BoxId <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "المورد والمخزن والخزينة مطلوبة.");
            }

            if (request.Date == default)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "تاريخ الفاتورة مطلوب.");
            }

            var lines = (request.Contents ?? [])
                .Where(l => l.ItemId > 0)
                .ToList();
            if (lines.Count == 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "يجب إضافة عنصر واحد على الأقل.");
            }

            if (lines.GroupBy(l => l.ItemId).Any(g => g.Count() > 1))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "لا يمكن تكرار نفس المادة داخل الفاتورة.");
            }

            var catalog = await _purchases.GetItemsAsync(request.StoreId, null, ct);
            var byId = catalog.ToDictionary(i => i.ItemId);
            foreach (var line in lines)
            {
                if (line.Quantity <= 0)
                {
                    throw new SalesCompleteException(StatusCodes.Status400BadRequest, "كمية المادة يجب أن تكون أكبر من صفر.");
                }

                if (!byId.TryGetValue(line.ItemId, out var item))
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "أحد المنتجات لم يعد موجوداً في هذا المخزن.");
                }

                line.ItemCostDenar = item.ItemCostDenar;
                line.TotalItemCostDenar = item.ItemCostDenar * line.Quantity;
            }

            if (await _purchases.InvoiceExistsAsync(request.SupplierId, invoice, ct))
            {
                throw new SalesCompleteException(
                    StatusCodes.Status409Conflict,
                    "فاتورة الشراء هذه مسجّلة مسبقاً لنفس المورد.");
            }

            var amountTotal = lines.Sum(l => l.TotalItemCostDenar);
            var supplierAccount = await _purchases.GetSupplierAccountAmountAsync(request.SupplierId, ct);
            var finalTotal = amountTotal + supplierAccount;
            var spent = request.TotalAmountSpent > 0 ? request.TotalAmountSpent : amountTotal;
            if (spent < 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "المبلغ المصروف غير صالح.");
            }

            var boxAmount = await _purchases.GetBoxAmountDenarAsync(request.BoxId, ct);
            if (spent > boxAmount)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "المبلغ المراد صرفه أكبر من الموجود في الخزينة.");
            }

            var remaining = finalTotal - spent;
            var displayName = string.IsNullOrWhiteSpace(identity.EmployeeName)
                ? "مسؤول مبيعات"
                : identity.EmployeeName.Trim();
            var userName = displayName;

            return await _purchases.CreateOfficialBuyAsync(new SalesPurchaseCreateCommand
            {
                Buy = new BuysPostDTO
                {
                    SupplierID = request.SupplierId,
                    StoreID = request.StoreId,
                    BoxID = request.BoxId,
                    Date = request.Date,
                    TotalAmountSpent = spent,
                    AmountTotalDenar = amountTotal,
                    FinalTotalItemCostDenar = finalTotal,
                    RemainingAmountDenar = remaining,
                    Contents = lines.Select(l => new ContentsBuyDTO
                    {
                        ItemID = l.ItemId,
                        Quantity = l.Quantity,
                        ItemCostDenar = l.ItemCostDenar,
                        TotalItemCostDenar = l.TotalItemCostDenar
                    }).ToList()
                },
                SupplierInvoiceNumber = invoice,
                Notes = request.Notes,
                CreatedByUserName = userName,
                CreatedByDisplayName = displayName,
                CreatedByUserType = SalesRoles.UserTypeSalesManager,
                BranchId = identity.BranchId,
                BranchName = identity.BranchName,
                PreferredUserId = identity.EmployeeId
            }, ct);
        }
    }
}
