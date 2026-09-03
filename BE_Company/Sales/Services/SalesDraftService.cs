using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public sealed class SalesDraftService
    {
        private readonly ISalesInventoryService _inventory;
        private readonly IGlobalCustomerSearchService _customers;
        private readonly ISalesPricingService _pricing;
        private readonly ISalesDraftRepository _drafts;
        private readonly ISalesRequestService _requests;
        private readonly IIraqClock _clock;

        public SalesDraftService(
            ISalesInventoryService inventory,
            IGlobalCustomerSearchService customers,
            ISalesPricingService pricing,
            ISalesDraftRepository drafts,
            ISalesRequestService requests,
            IIraqClock clock)
        {
            _inventory = inventory;
            _customers = customers;
            _pricing = pricing;
            _drafts = drafts;
            _requests = requests;
            _clock = clock;
        }

        public async Task<SalesDraftDTO> CreateAsync(
            SalesDraftCreateRequestDTO request,
            int employeeId,
            string? userName,
            string? userType,
            string cityValue,
            string cityName,
            CancellationToken ct)
        {
            if (!SalesEvaluationLevels.IsKnown(request.EvaluationLevel))
            {
                throw new ArgumentException("مستوى التقييم غير صالح");
            }
            if (string.IsNullOrWhiteSpace(request.EvaluationNote))
            {
                throw new ArgumentException("الملاحظة إلزامية لكل التقييمات");
            }
            if (request.DailyInstallment <= 0)
            {
                throw new ArgumentException("القسط اليومي يجب أن يكون أكبر من صفر");
            }
            if (request.Items == null || request.Items.Count == 0)
            {
                throw new ArgumentException("يجب اختيار مادة واحدة على الأقل");
            }

            await _drafts.EnsureSchemaAsync(ct);

            var draftItems = new List<SalesDraftItemDTO>();
            decimal baseSalePrice = 0;
            foreach (var line in request.Items)
            {
                if (line.Quantity <= 0)
                {
                    throw new ArgumentException("الكمية يجب أن تكون أكبر من صفر");
                }

                var product = await _inventory.GetProductAsync(line.ProductId, ct)
                              ?? throw new ArgumentException("المادة غير موجودة في مخزن الفرع");
                if (line.Quantity > product.AvailableQuantity)
                {
                    throw new ArgumentException("الكمية المطلوبة أكبر من المتوفر الحالي");
                }

                var linePrice = product.SalePrice * line.Quantity;
                baseSalePrice += linePrice;
                draftItems.Add(new SalesDraftItemDTO
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Quantity = line.Quantity,
                    UnitSalePrice = product.SalePrice,
                    LineSalePrice = linePrice
                });
            }

            string fullName;
            string? phone;
            string? province;
            string? nationalCard = request.Customer?.NationalCardNumber;
            string? address = request.Customer?.Address;
            string? landmark = request.Customer?.NearestLandmark;
            string? mukhtar = request.Customer?.MukhtarName;
            string? ration = request.Customer?.RationCenterNumber;
            int? customerId = request.CustomerId;
            string? sourceCity = null;

            if (customerId.HasValue && customerId.Value > 0)
            {
                var existing = await _customers.GetCustomerAsync(customerId.Value, ct)
                               ?? throw new ArgumentException("الزبون غير موجود");
                fullName = existing.CustomerName ?? request.Customer?.FullName ?? string.Empty;
                phone = existing.PhoneNumber ?? request.Customer?.Phone;
                province = string.IsNullOrWhiteSpace(existing.CityName) ? cityName : existing.CityName;
                sourceCity = cityValue;
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = existing.Address;
                }
                if (string.IsNullOrWhiteSpace(landmark))
                {
                    landmark = existing.NearestFunctionPoint;
                }
            }
            else
            {
                fullName = request.Customer?.FullName?.Trim() ?? string.Empty;
                phone = request.Customer?.Phone;
                province = string.IsNullOrWhiteSpace(request.Customer?.Province) ? cityName : request.Customer!.Province;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("اسم الزبون مطلوب");
            }

            var draft = new SalesDraftDTO
            {
                EmployeeId = employeeId,
                UserName = userName,
                UserType = userType,
                CityValue = cityValue,
                CityName = cityName,
                Status = _pricing.ResolveStatus(request.EvaluationLevel),
                CustomerId = customerId,
                SourceCityValue = sourceCity,
                FullName = fullName,
                Phone = phone,
                Province = province,
                NationalCardNumber = nationalCard,
                Address = address,
                NearestLandmark = landmark,
                MukhtarName = mukhtar,
                RationCenterNumber = string.IsNullOrWhiteSpace(ration) ? null : ration.Trim(),
                EvaluationLevel = request.EvaluationLevel,
                EvaluationNote = request.EvaluationNote.Trim(),
                BaseSalePrice = baseSalePrice,
                FinalSalePrice = _pricing.ComputeFinalSalePrice(baseSalePrice, request.EvaluationLevel),
                DailyInstallment = Math.Round(request.DailyInstallment, 0, MidpointRounding.AwayFromZero),
                Items = draftItems,
                SalesRequestId = request.SalesRequestId
            };

            if (request.SalesRequestId is > 0)
            {
                var existing = await _requests.GetForEmployeeAsync(request.SalesRequestId.Value, employeeId, ct);
                if (existing.Status == SalesRequestStatuses.Rejected)
                {
                    throw new ArgumentException("لا يمكن تحويل طلب مرفوض إلى عملية بيع.");
                }

                if (existing.ConvertedToSaleId is > 0)
                {
                    throw new ArgumentException("الطلب مرتبط بعملية بيع أخرى.");
                }
            }

            var created = await _drafts.CreateAsync(draft, ct);
            if (request.SalesRequestId is > 0)
            {
                await _requests.MarkConvertedAsync(request.SalesRequestId.Value, employeeId, created.SaleId, _clock.UtcNow, ct);
                created.SalesRequestId = request.SalesRequestId;
            }

            return created;
        }
    }
}
