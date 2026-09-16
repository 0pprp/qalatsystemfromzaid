using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Rating;
using Microsoft.AspNetCore.Http;

namespace BE_Company.Sales.Services
{
    public interface IExceptionReviewService
    {
        Task<ExceptionReviewContextDTO> BuildContextAsync(
            SalesIdentity actor,
            int salesRequestId,
            CancellationToken ct);
    }

    public sealed class ExceptionReviewService : IExceptionReviewService
    {
        public const int MaxMatches = 20;
        public const int MaxSalesPerMatch = 10;

        private readonly ISalesRequestRepository _requests;
        private readonly ISalesExcelCustomerSearchCatalog _catalog;
        private readonly IRatingDataSource _rating;
        private readonly IIraqClock _clock;

        public ExceptionReviewService(
            ISalesRequestRepository requests,
            ISalesExcelCustomerSearchCatalog catalog,
            IRatingDataSource rating,
            IIraqClock clock)
        {
            _requests = requests;
            _catalog = catalog;
            _rating = rating;
            _clock = clock;
        }

        public async Task<ExceptionReviewContextDTO> BuildContextAsync(
            SalesIdentity actor,
            int salesRequestId,
            CancellationToken ct)
        {
            EnsureManagerOrGateway(actor);
            if (salesRequestId <= 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    "معرّف طلب البيع مطلوب.");
            }

            await _requests.EnsureSchemaAsync(ct);
            var row = await _requests.GetByIdAsync(salesRequestId, ct);
            if (row is null || row.IsDeleted)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status404NotFound,
                    "لا يمكن تحميل تفاصيل طلب البيع الأصلي.");
            }

            var requestCity = (row.CityValue ?? _catalog.CityValue ?? "").Trim();
            var branchCity = (_catalog.CityValue ?? "").Trim();
            var customers = await _catalog.LoadCustomersAsync(ct);
            var candidates = customers.Select(c => new ExceptionReviewCandidate
            {
                CustomerId = c.CustomerId,
                FullName = c.FullName,
                Phone = c.Phone,
                // Branch catalog host is the province stamp (rows themselves lack CityValue).
                CityValue = null,
                CityName = _catalog.CityName,
                Province = c.Province,
                Address = c.Address,
                Occupation = null,
                AmountTotalSales = c.AmountTotalSales,
                ReceiptsTotal = c.ReceiptsTotal,
                AmountRemaining = c.AmountRemaining
            }).ToList();

            var hits = ExceptionReviewCustomerMatcher.FindMatches(
                row.CustomerName,
                row.CustomerPhone,
                requestCity,
                branchCity,
                candidates);

            // Cap for payload size; keep stable order by customer id.
            hits = hits.OrderBy(h => h.Customer.CustomerId).Take(MaxMatches).ToList();
            var classification = ExceptionReviewCustomerMatcher.Classify(hits);

            var matchIds = hits.Select(h => h.Customer.CustomerId).ToList();
            var facts = matchIds.Count == 0
                ? []
                : await _rating.GetFactsByCustomerIdsAsync(matchIds, ct);
            var factMap = facts.ToDictionary(f => f.CustomerId);
            var sales = matchIds.Count == 0
                ? []
                : await _catalog.LoadSalesAsync(matchIds, ct);
            var salesByCustomer = sales
                .GroupBy(s => s.CustomerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.SaleId)
                    .Take(MaxSalesPerMatch).ToList());

            var asOf = _clock.UtcNow.Date;
            var friendlyCity = SalesCityDisplay.FriendlyOrUnavailable(_catalog.CityName, branchCity);
            var matching = new List<ExceptionReviewMatchCustomerDTO>();
            foreach (var hit in hits)
            {
                factMap.TryGetValue(hit.Customer.CustomerId, out var fact);
                var rating = fact is null ? null : CustomerRatingCalculator.Evaluate(fact, asOf);
                salesByCustomer.TryGetValue(hit.Customer.CustomerId, out var prevSales);
                prevSales ??= [];

                var totalSales = fact?.AmountTotalSales ?? hit.Customer.AmountTotalSales ?? 0;
                var totalPaid = fact?.ReceiptsTotal ?? hit.Customer.ReceiptsTotal ?? 0;
                var remaining = fact?.AmountRemaining ?? hit.Customer.AmountRemaining ?? 0;
                var fullyPaid = remaining <= 0 && totalSales > 0;
                var lastSale = prevSales.FirstOrDefault()?.SaleDate ?? rating?.DateSaleDevice ?? fact?.DateSaleDevice;
                var lastPayment = fact?.LastPaymentDate;

                matching.Add(new ExceptionReviewMatchCustomerDTO
                {
                    CustomerId = hit.Customer.CustomerId,
                    FullName = hit.Customer.FullName,
                    Phone = hit.Customer.Phone,
                    CityValue = branchCity,
                    CityName = friendlyCity,
                    Province = friendlyCity,
                    Address = hit.Customer.Address,
                    Occupation = hit.Customer.Occupation,
                    MatchReasons = hit.MatchReasons,
                    RatingLabel = rating?.RatingLabel ?? (fact is null ? null : CustomerRatingLabels.Weak),
                    RatingScore = rating?.Score,
                    IsLegal = rating?.IsLegal ?? fact?.IsLegal ?? false,
                    FinancialSummary = new ExceptionReviewFinancialSummaryDTO
                    {
                        TotalSales = totalSales,
                        TotalPaid = totalPaid,
                        Remaining = remaining,
                        CurrentDebt = remaining > 0 ? remaining : 0,
                        LastSaleDate = lastSale,
                        LastPaymentDate = lastPayment,
                        FullyPaid = fullyPaid || (rating?.IsSettled ?? false),
                        LegalStatus = (rating?.IsLegal ?? fact?.IsLegal ?? false) ? "قانونية" : null,
                        ReceiptCount = rating?.ReceiptCount ?? fact?.ReceiptCount ?? 0
                    },
                    PreviousSales = prevSales.Select(s => MapPreviousSale(s, friendlyCity, asOf)).ToList()
                });
            }

            // List name: SourceListId present but no resolver in this module → غير متوفر.
            string? listName = null;
            if (row.SourceListId is > 0)
            {
                listName = null; // mapper will emit غير متوفر
            }

            return new ExceptionReviewContextDTO
            {
                CustomerClassification = classification,
                CurrentRequest = MapCurrentRequest(row),
                Source = ExceptionReviewSourceMapper.FromRequest(row, listName),
                MatchingCustomers = matching
            };
        }

        private static ExceptionReviewCurrentRequestDTO MapCurrentRequest(SalesRequestDTO row)
        {
            var saleType = ExtractSaleType(row.Notes);
            return new ExceptionReviewCurrentRequestDTO
            {
                SalesRequestId = row.Id,
                CustomerName = row.CustomerName,
                CustomerPhone = row.CustomerPhone,
                CityValue = row.CityValue,
                CityName = SalesCityDisplay.FriendlyOrUnavailable(row.CityName, row.CityValue),
                CustomerProvince = string.IsNullOrWhiteSpace(row.CustomerProvince)
                    ? SalesCityDisplay.FriendlyOrUnavailable(row.CityName, row.CityValue)
                    : (SalesCityDisplay.IsInternalKey(row.CustomerProvince, row.CityValue)
                        ? SalesCityDisplay.FriendlyOrUnavailable(row.CustomerProvince, row.CityValue)
                        : row.CustomerProvince),
                CustomerAddress = row.CustomerAddress,
                Occupation = null,
                Notes = row.Notes,
                SaleTypeOrProduct = saleType,
                SaleRequestType = row.SaleRequestType,
                Status = row.Status,
                CreatedAtUtc = row.CreatedAtUtc,
                CreatedByName = row.CreatedByName,
                CreatedByUserType = row.CreatedByUserType,
                CustomerSourceType = row.CustomerSourceType,
                SourceListId = row.SourceListId,
                ExistingCustomerId = row.ExistingCustomerId,
                TargetEmployeeName = row.TargetEmployeeName,
                PendingNote = row.PendingNote,
                PreparedForSaleNote = row.PreparedForSaleNote
            };
        }

        private static ExceptionReviewPreviousSaleDTO MapPreviousSale(
            SalesExcelSearchSaleRow s,
            string friendlyCity,
            DateTime asOf)
        {
            var saleAmount = s.SaleAmount ?? 0;
            var paid = s.PaidAmount;
            var remaining = s.RemainingAmount;
            if (remaining is null && paid is not null)
            {
                remaining = Math.Max(0, saleAmount - paid.Value);
            }

            if (s.AccountZero == true)
            {
                remaining = 0;
                paid ??= saleAmount;
            }

            var zero = s.AccountZero == true || (remaining is <= 0 && saleAmount > 0);
            int? repaymentDays = null;
            if (s.SaleDate is not null)
            {
                if (zero && s.LastPaymentDate is not null)
                {
                    repaymentDays = CustomerRatingCalculator.DiffDaysInclusive(
                        s.SaleDate.Value, s.LastPaymentDate.Value);
                }
                else if (!zero)
                {
                    repaymentDays = CustomerRatingCalculator.DiffDaysInclusive(s.SaleDate.Value, asOf);
                }
            }

            return new ExceptionReviewPreviousSaleDTO
            {
                SaleId = s.SaleId,
                SaleDate = s.SaleDate,
                ProductOrType = string.IsNullOrWhiteSpace(s.ItemsNames) ? null : s.ItemsNames.Trim(),
                SaleAmount = saleAmount,
                PaidAmount = paid,
                RemainingAmount = remaining,
                AccountZero = zero,
                AccountStatusArabic = zero ? "مصفر" : "مفتوح",
                PaymentCount = s.PaymentCount,
                RepaymentDays = repaymentDays,
                LastPaymentDate = s.LastPaymentDate,
                BranchName = friendlyCity,
                FriendlyCityName = friendlyCity
            };
        }

        private static string? ExtractSaleType(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            const string prefix = "نوع المبيع:";
            var text = notes.Trim();
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                return text[prefix.Length..].Trim();
            }

            return null;
        }

        private static void EnsureManagerOrGateway(SalesIdentity actor)
        {
            if (actor.IsGateway)
            {
                return;
            }

            if (!string.Equals(actor.Role, SalesRoles.SalesManager, StringComparison.Ordinal)
                && !SalesRoles.IsSalesManager(actor.UserType))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }
        }
    }
}
