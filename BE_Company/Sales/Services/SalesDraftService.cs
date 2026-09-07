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
        private readonly ISalesShopProfileService? _shops;

        public SalesDraftService(
            ISalesInventoryService inventory,
            IGlobalCustomerSearchService customers,
            ISalesPricingService pricing,
            ISalesDraftRepository drafts,
            ISalesRequestService requests,
            IIraqClock clock,
            ISalesShopProfileService? shops = null)
        {
            _inventory = inventory;
            _customers = customers;
            _pricing = pricing;
            _drafts = drafts;
            _requests = requests;
            _clock = clock;
            _shops = shops;
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
                throw new ArgumentException("القائمة/المندوب مطلوبة.");
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
            string? requestName = null;
            string? requestPhone = null;
            string? requestAddress = null;
            string? requestProvince = null;

            if (request.SalesRequestId is > 0)
            {
                var linked = await _requests.GetForEmployeeAsync(request.SalesRequestId.Value, employeeId, ct);
                requestName = linked.CustomerName;
                requestPhone = linked.CustomerPhone;
                requestAddress = linked.CustomerAddress;
                requestProvince = linked.CustomerProvince;
                if (customerId is not > 0 && linked.ExistingCustomerId is > 0)
                {
                    customerId = linked.ExistingCustomerId;
                }
            }

            if (customerId.HasValue && customerId.Value > 0)
            {
                var existing = await _customers.GetCustomerAsync(customerId.Value, ct)
                               ?? throw new ArgumentException("الزبون غير موجود");
                var submittedName = request.Customer?.FullName?.Trim();
                fullName = SalesCustomerIdentity.PreferName(
                    submittedName,
                    requestName,
                    existing.CustomerName);
                var submittedPhone = request.Customer?.Phone?.Trim();
                phone = SalesCustomerIdentity.PreferText(
                    submittedPhone,
                    requestPhone,
                    existing.PhoneNumber);
                province = SalesCustomerIdentity.PreferText(
                    request.Customer?.Province,
                    requestProvince,
                    string.IsNullOrWhiteSpace(existing.CityName) ? cityName : existing.CityName);
                sourceCity = cityValue;
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = SalesCustomerIdentity.PreferText(null, requestAddress, existing.Address);
                }
                if (string.IsNullOrWhiteSpace(landmark))
                {
                    landmark = existing.NearestFunctionPoint;
                }
            }
            else
            {
                fullName = SalesCustomerIdentity.PreferName(
                    request.Customer?.FullName,
                    requestName);
                phone = SalesCustomerIdentity.PreferText(request.Customer?.Phone, requestPhone);
                province = SalesCustomerIdentity.PreferText(
                    request.Customer?.Province,
                    requestProvince,
                    cityName);
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = requestAddress;
                }
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("اسم الزبون مطلوب");
            }

            if (!string.IsNullOrWhiteSpace(phone) && !SalesIraqPhone.IsValid(phone))
            {
                throw new ArgumentException(SalesIraqPhone.Message);
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                phone = SalesIraqPhone.Normalize(phone);
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
                CustomerListId = request.CustomerListId,
                WizardCurrentStep = request.WizardCurrentStep
            };

            if (request.SalesRequestId is > 0)
            {
                var existing = await _requests.GetForEmployeeAsync(request.SalesRequestId.Value, employeeId, ct);
                if (existing.ConvertedToSaleId is > 0)
                {
                    var current = await _drafts.GetByIdAsync(existing.ConvertedToSaleId.Value, employeeId, ct)
                                  ?? throw new ArgumentException("الطلب مرتبط بعملية بيع أخرى.");
                    if (SalesCompleteRules.AlreadyCompleted(current.Status))
                    {
                        throw new ArgumentException("الطلب مرتبط بعملية بيع مكتملة.");
                    }

                    draft.SaleId = current.SaleId;
                    draft.Status = SalesStatuses.Pending;
                    return await _drafts.ReplaceContentsAsync(draft, ct);
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

        public async Task<SalesDraftDTO> SaveProgressAsync(
            SalesDraftCreateRequestDTO request,
            int employeeId,
            string? userName,
            string? userType,
            string cityValue,
            string cityName,
            CancellationToken ct)
        {
            await _drafts.EnsureSchemaAsync(ct);
            var draftItems = new List<SalesDraftItemDTO>();
            decimal baseSalePrice = 0;
            decimal defaultDaily = 0;
            foreach (var line in request.Items ?? [])
            {
                if (line.Quantity <= 0)
                {
                    continue;
                }

                var product = await _inventory.GetProductAsync(line.ProductId, ct);
                if (product == null)
                {
                    continue;
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
            var snapshot = draftItems.Count == 0
                ? new SalesPriceSnapshot()
                : _pricing.ComputeCheckout(
                    baseSalePrice,
                    defaultDaily,
                    request.OverrideTotalSalePrice,
                    overrideDaily,
                    request.OverrideDownPayment);

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
            string? requestName = null;
            string? requestPhone = null;
            string? requestAddress = null;
            string? requestProvince = null;

            if (request.SalesRequestId is > 0)
            {
                var linked = await _requests.GetForEmployeeAsync(request.SalesRequestId.Value, employeeId, ct);
                requestName = linked.CustomerName;
                requestPhone = linked.CustomerPhone;
                requestAddress = linked.CustomerAddress;
                requestProvince = linked.CustomerProvince;
                if (customerId is not > 0 && linked.ExistingCustomerId is > 0)
                {
                    customerId = linked.ExistingCustomerId;
                }
            }

            if (customerId.HasValue && customerId.Value > 0)
            {
                var existing = await _customers.GetCustomerAsync(customerId.Value, ct);
                fullName = SalesCustomerIdentity.PreferName(
                    request.Customer?.FullName?.Trim(),
                    requestName,
                    existing?.CustomerName);
                phone = SalesCustomerIdentity.PreferText(
                    request.Customer?.Phone?.Trim(),
                    requestPhone,
                    existing?.PhoneNumber);
                province = SalesCustomerIdentity.PreferText(
                    request.Customer?.Province,
                    requestProvince,
                    string.IsNullOrWhiteSpace(existing?.CityName) ? cityName : existing!.CityName);
                sourceCity = cityValue;
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = SalesCustomerIdentity.PreferText(null, requestAddress, existing?.Address);
                }

                if (string.IsNullOrWhiteSpace(landmark))
                {
                    landmark = existing?.NearestFunctionPoint;
                }
            }
            else
            {
                fullName = SalesCustomerIdentity.PreferName(request.Customer?.FullName, requestName);
                phone = SalesCustomerIdentity.PreferText(request.Customer?.Phone, requestPhone);
                province = SalesCustomerIdentity.PreferText(request.Customer?.Province, requestProvince, cityName);
                if (string.IsNullOrWhiteSpace(address))
                {
                    address = requestAddress;
                }
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = "—";
            }

            if (!string.IsNullOrWhiteSpace(phone) && !SalesIraqPhone.IsValid(phone))
            {
                phone = null;
            }
            else if (!string.IsNullOrWhiteSpace(phone))
            {
                phone = SalesIraqPhone.Normalize(phone);
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
                CustomerListId = request.CustomerListId is > 0 ? request.CustomerListId : null,
                WizardCurrentStep = request.WizardCurrentStep
            };

            SalesDraftDTO saved;
            if (request.SalesRequestId is > 0)
            {
                var existing = await _requests.GetForEmployeeAsync(request.SalesRequestId.Value, employeeId, ct);
                if (existing.ConvertedToSaleId is > 0)
                {
                    var current = await _drafts.GetByIdAsync(existing.ConvertedToSaleId.Value, employeeId, ct)
                                  ?? throw new ArgumentException("الطلب مرتبط بعملية بيع أخرى.");
                    if (SalesCompleteRules.AlreadyCompleted(current.Status))
                    {
                        throw new ArgumentException("الطلب مرتبط بعملية بيع مكتملة.");
                    }

                    draft.SaleId = current.SaleId;
                    draft.Status = SalesStatuses.Pending;
                    saved = await _drafts.ReplaceContentsAsync(draft, ct);
                }
                else
                {
                    saved = await _drafts.CreateAsync(draft, ct);
                    await _requests.AttachDraftAsync(request.SalesRequestId.Value, employeeId, saved.SaleId, ct);
                    saved.SalesRequestId = request.SalesRequestId;
                }
            }
            else
            {
                saved = await _drafts.CreateAsync(draft, ct);
            }

            if (_shops != null && request.Shop != null && HasAnyShopData(request.Shop))
            {
                await _shops.UpsertFromCompleteAsync(saved, request.Shop, ct);
                saved.Shop = await _shops.GetBySaleIdAsync(saved.SaleId, ct);
            }

            if (request.MarkInspected && request.SalesRequestId is > 0)
            {
                await _requests.InspectAsync(request.SalesRequestId.Value, employeeId, saved.SaleId, ct);
            }

            return saved;
        }

        private static bool HasAnyShopData(SalesShopCompleteDTO shop) =>
            !string.IsNullOrWhiteSpace(shop.ShopName)
            || !string.IsNullOrWhiteSpace(shop.ShopBusinessType)
            || !string.IsNullOrWhiteSpace(shop.ShopImageKey)
            || shop.Latitude != null
            || shop.Longitude != null
            || shop.ShopLength > 0
            || shop.ShopWidth > 0
            || shop.ShopStockEstimatedValue > 0
            || shop.EstimatedDailyRevenue > 0;
    }
}
