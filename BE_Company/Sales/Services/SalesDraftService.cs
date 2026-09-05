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
            if (request.Items == null || request.Items.Count == 0)
            {
                throw new ArgumentException("يجب اختيار مادة واحدة على الأقل");
            }
            if (request.CustomerListId is null or <= 0)
            {
                throw new ArgumentException("قائمة الزبون مطلوبة.");
            }

            await _drafts.EnsureSchemaAsync(ct);

            var draftItems = new List<SalesDraftItemDTO>();
            decimal baseSalePrice = 0;
            decimal defaultDaily = 0;
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
                defaultDaily += (product.DailyInstallment ?? 0) * line.Quantity;
                draftItems.Add(new SalesDraftItemDTO
                {
                    ProductId = product.ProductId,
                    ProductName = product.ProductName,
                    Quantity = line.Quantity,
                    UnitSalePrice = product.SalePrice,
                    LineSalePrice = linePrice
                });
            }

            var overrideDaily = request.OverrideDailyInstallment
                                ?? (request.DailyInstallment > 0 ? request.DailyInstallment : null);
            var snapshot = _pricing.ComputeCheckout(
                baseSalePrice,
                defaultDaily,
                request.OverrideTotalSalePrice,
                overrideDaily,
                request.OverrideDownPayment);
            if (snapshot.FinalTotalSalePrice <= 0)
            {
                throw new ArgumentException("سعر البيع الكلي يجب أن يكون أكبر من صفر");
            }
            if (snapshot.FinalDailyInstallment <= 0)
            {
                throw new ArgumentException("القسط اليومي يجب أن يكون أكبر من صفر");
            }
            if (snapshot.FinalDownPayment < 0)
            {
                throw new ArgumentException("الدفعة المقدمة غير صالحة");
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
                Status = SalesStatuses.Pending,
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
                EvaluationNote = string.IsNullOrWhiteSpace(request.EvaluationNote) ? string.Empty : request.EvaluationNote.Trim(),
                BaseSalePrice = snapshot.DefaultTotalSalePrice,
                FinalSalePrice = snapshot.FinalTotalSalePrice,
                DailyInstallment = snapshot.FinalDailyInstallment,
                DefaultTotalSalePrice = snapshot.DefaultTotalSalePrice,
                DefaultDailyInstallment = snapshot.DefaultDailyInstallment,
                DefaultDownPayment = snapshot.DefaultDownPayment,
                OverrideTotalSalePrice = snapshot.OverrideTotalSalePrice,
                OverrideDailyInstallment = snapshot.OverrideDailyInstallment,
                OverrideDownPayment = snapshot.OverrideDownPayment,
                DownPayment = snapshot.FinalDownPayment,
                Items = draftItems,
                SalesRequestId = request.SalesRequestId,
                CustomerListId = request.CustomerListId
            };

            if (request.SalesRequestId is > 0)
            {
                var existing = await _requests.GetForEmployeeAsync(request.SalesRequestId.Value, employeeId, ct);
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
