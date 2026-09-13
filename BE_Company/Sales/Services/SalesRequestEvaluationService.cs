using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Rating;
using Microsoft.AspNetCore.Http;

namespace BE_Company.Sales.Services
{
    public interface ISalesRequestEvaluationService
    {
        Task<SalesRequestEvaluationBatchResultDTO> EvaluateBatchAsync(
            SalesIdentity actor,
            SalesRequestEvaluationBatchRequestDTO body,
            CancellationToken ct);

        Task<SalesRequestEvaluationHitsPageDTO> ListHitsAsync(
            SalesIdentity actor,
            int requestId,
            string category,
            int page,
            int pageSize,
            CancellationToken ct);

        Task<SalesRequestEvaluationHitsPageDTO> ListHitsByPayloadAsync(
            SalesIdentity actor,
            SalesRequestEvaluationHitsRequestDTO body,
            CancellationToken ct);
    }

    public sealed class SalesRequestEvaluationService : ISalesRequestEvaluationService
    {
        public const string CategoryTriple = "tripleName";
        public const string CategoryPhone = "phone";
        public const string CategoryKinship = "fatherGrandfather";

        private readonly ISalesRequestRepository _requests;
        private readonly ISalesExcelCustomerSearchCatalog _catalog;
        private readonly IRatingDataSource _rating;
        private readonly IIraqClock _clock;

        public SalesRequestEvaluationService(
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

        public async Task<SalesRequestEvaluationBatchResultDTO> EvaluateBatchAsync(
            SalesIdentity actor,
            SalesRequestEvaluationBatchRequestDTO body,
            CancellationToken ct)
        {
            EnsureManager(actor);
            body ??= new SalesRequestEvaluationBatchRequestDTO();
            if (body.Items is { Count: > 0 })
            {
                return await EvaluatePayloadsAsync(body.Items, ct);
            }

            return await EvaluateRequestIdsAsync(body.RequestIds ?? [], ct);
        }

        public async Task<SalesRequestEvaluationHitsPageDTO> ListHitsAsync(
            SalesIdentity actor,
            int requestId,
            string category,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            EnsureManager(actor);
            await _requests.EnsureSchemaAsync(ct);
            var row = await _requests.GetByIdAsync(requestId, ct);
            if (row is null || row.IsDeleted)
            {
                throw new SalesCompleteException(StatusCodes.Status404NotFound, "الطلب غير موجود.");
            }

            return await BuildHitsPageAsync(
                requestId,
                row.CustomerName,
                row.CustomerPhone,
                category,
                page,
                pageSize,
                ct);
        }

        public async Task<SalesRequestEvaluationHitsPageDTO> ListHitsByPayloadAsync(
            SalesIdentity actor,
            SalesRequestEvaluationHitsRequestDTO body,
            CancellationToken ct)
        {
            EnsureManager(actor);
            body ??= new SalesRequestEvaluationHitsRequestDTO();
            return await BuildHitsPageAsync(
                body.RequestId,
                body.CustomerName,
                body.CustomerPhone,
                body.Category,
                body.Page,
                body.PageSize,
                ct);
        }

        private async Task<SalesRequestEvaluationBatchResultDTO> EvaluateRequestIdsAsync(
            IReadOnlyList<int> requestIds,
            CancellationToken ct)
        {
            await _requests.EnsureSchemaAsync(ct);
            var ids = requestIds.Where(x => x > 0).Distinct().Take(200).ToList();
            var result = new SalesRequestEvaluationBatchResultDTO();
            if (ids.Count == 0)
            {
                return result;
            }

            var customers = await _catalog.LoadCustomersAsync(ct);
            var asOf = _clock.UtcNow.Date;
            var allMatchedIds = new HashSet<int>();
            var perRequest = new Dictionary<int, (SalesRequestDTO Row, CategoryMatches Matches)>();

            foreach (var id in ids)
            {
                var row = await _requests.GetByIdAsync(id, ct);
                if (row is null || row.IsDeleted)
                {
                    continue;
                }

                var matches = MatchCategories(row.CustomerName, row.CustomerPhone, customers);
                perRequest[id] = (row, matches);
                foreach (var cid in matches.AllCustomerIds())
                {
                    allMatchedIds.Add(cid);
                }
            }

            var ratings = await LoadRatingsAsync(allMatchedIds, asOf, ct);
            foreach (var id in ids)
            {
                if (!perRequest.TryGetValue(id, out var pair))
                {
                    continue;
                }

                result.Items.Add(BuildSummary(
                    id,
                    pair.Row.CityValue ?? _catalog.CityValue,
                    pair.Matches,
                    ratings));
            }

            return result;
        }

        private async Task<SalesRequestEvaluationBatchResultDTO> EvaluatePayloadsAsync(
            IReadOnlyList<SalesRequestEvaluationPayloadDTO> items,
            CancellationToken ct)
        {
            var payloads = items
                .Where(x => x is not null && x.RequestId > 0)
                .Take(200)
                .ToList();
            var result = new SalesRequestEvaluationBatchResultDTO();
            if (payloads.Count == 0)
            {
                return result;
            }

            var customers = await _catalog.LoadCustomersAsync(ct);
            var asOf = _clock.UtcNow.Date;
            var allMatchedIds = new HashSet<int>();
            var perKey = new List<(SalesRequestEvaluationPayloadDTO Payload, CategoryMatches Matches)>();

            foreach (var payload in payloads)
            {
                var matches = MatchCategories(payload.CustomerName, payload.CustomerPhone, customers);
                perKey.Add((payload, matches));
                foreach (var cid in matches.AllCustomerIds())
                {
                    allMatchedIds.Add(cid);
                }
            }

            var ratings = await LoadRatingsAsync(allMatchedIds, asOf, ct);
            foreach (var (payload, matches) in perKey)
            {
                result.Items.Add(BuildSummary(
                    payload.RequestId,
                    payload.SourceCityValue ?? _catalog.CityValue,
                    matches,
                    ratings));
            }

            return result;
        }

        private async Task<SalesRequestEvaluationHitsPageDTO> BuildHitsPageAsync(
            int requestId,
            string? customerName,
            string? customerPhone,
            string? category,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var cat = (category ?? "").Trim();
            if (!IsKnownCategory(cat))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "فئة البحث غير معروفة.");
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 30 : pageSize, 1, 100);
            var customers = await _catalog.LoadCustomersAsync(ct);
            var matches = MatchCategories(customerName, customerPhone, customers);
            var ids = cat.Equals(CategoryTriple, StringComparison.OrdinalIgnoreCase) ? matches.Triple
                : cat.Equals(CategoryPhone, StringComparison.OrdinalIgnoreCase) ? matches.Phone
                : matches.Kinship;

            var pageIds = ids.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var facts = await _rating.GetFactsByCustomerIdsAsync(pageIds, ct);
            var factMap = facts.ToDictionary(f => f.CustomerId);
            var asOf = _clock.UtcNow.Date;
            var customerMap = customers.ToDictionary(c => c.CustomerId);
            var items = new List<SalesRequestEvaluationHitDTO>();
            foreach (var cid in pageIds)
            {
                if (!customerMap.TryGetValue(cid, out var cust))
                {
                    continue;
                }

                factMap.TryGetValue(cid, out var fact);
                var rating = fact is null
                    ? null
                    : CustomerRatingCalculator.Evaluate(fact, asOf);
                var reason = cat.Equals(CategoryPhone, StringComparison.OrdinalIgnoreCase)
                    ? "تطابق رقم الهاتف"
                    : cat.Equals(CategoryTriple, StringComparison.OrdinalIgnoreCase)
                        ? "تشابه الاسم الثلاثي"
                        : SalesRequestNameSimilarity.FatherGrandfatherMatchReason(customerName, cust.FullName);

                items.Add(new SalesRequestEvaluationHitDTO
                {
                    CustomerId = cid,
                    FullName = cust.FullName,
                    Phone = cust.Phone,
                    Province = cust.Province,
                    CityValue = _catalog.CityValue,
                    CityName = _catalog.CityName,
                    RatingLabel = rating?.RatingLabel ?? CustomerRatingLabels.Weak,
                    Score = rating?.Score ?? 0,
                    MatchReason = reason,
                    DateSaleDevice = rating?.DateSaleDevice ?? fact?.DateSaleDevice,
                    DaysToSettle = rating?.DaysToSettle,
                    DaysSinceSale = rating?.DaysSinceSale,
                    AmountTotalSales = fact?.AmountTotalSales ?? cust.AmountTotalSales ?? 0,
                    ReceiptsTotal = fact?.ReceiptsTotal ?? cust.ReceiptsTotal ?? 0,
                    AmountRemaining = fact?.AmountRemaining ?? cust.AmountRemaining ?? 0,
                    ReceiptCount = rating?.ReceiptCount ?? fact?.ReceiptCount ?? 0,
                    IsLegal = rating?.IsLegal ?? fact?.IsLegal ?? false,
                    IsSettled = rating?.IsSettled ?? false
                });
            }

            return new SalesRequestEvaluationHitsPageDTO
            {
                RequestId = requestId,
                Category = cat,
                Total = ids.Count,
                Page = page,
                PageSize = pageSize,
                Items = items
            };
        }

        private async Task<IReadOnlyDictionary<int, CustomerRatingResult>> LoadRatingsAsync(
            HashSet<int> customerIds,
            DateTime asOf,
            CancellationToken ct)
        {
            if (customerIds.Count == 0)
            {
                return new Dictionary<int, CustomerRatingResult>();
            }

            var facts = await _rating.GetFactsByCustomerIdsAsync(customerIds.ToList(), ct);
            return facts.ToDictionary(
                f => f.CustomerId,
                f => CustomerRatingCalculator.Evaluate(f, asOf));
        }

        private static CategoryMatches MatchCategories(
            string? customerName,
            string? customerPhone,
            IReadOnlyList<SalesExcelSearchCustomerRow> customers)
        {
            var triple = new List<int>();
            var phone = new List<int>();
            var kinship = new List<int>();
            var kinshipSeen = new HashSet<int>();

            foreach (var c in customers)
            {
                if (c.CustomerId <= 0)
                {
                    continue;
                }

                if (SalesRequestNameSimilarity.IsTripleNameMatch(customerName, c.FullName))
                {
                    triple.Add(c.CustomerId);
                }

                if (!string.IsNullOrWhiteSpace(customerPhone)
                    && SalesPhoneNormalizer.Matches(customerPhone, c.Phone))
                {
                    phone.Add(c.CustomerId);
                }

                if (SalesRequestNameSimilarity.IsFatherOrGrandfatherMatch(customerName, c.FullName)
                    && kinshipSeen.Add(c.CustomerId))
                {
                    kinship.Add(c.CustomerId);
                }
            }

            return new CategoryMatches(triple, phone, kinship);
        }

        private static SalesRequestEvaluationSummaryDTO BuildSummary(
            int requestId,
            string? sourceCityValue,
            CategoryMatches matches,
            IReadOnlyDictionary<int, CustomerRatingResult> ratings)
        {
            var tripleCat = BuildCategory(matches.Triple, ratings);
            var phoneCat = BuildCategory(matches.Phone, ratings);
            var kinCat = BuildCategory(matches.Kinship, ratings);
            var overall = SalesRequestEvaluationAggregator.Overall(
                ToPair(tripleCat), ToPair(phoneCat), ToPair(kinCat));
            var source = (sourceCityValue ?? "").Trim();

            return new SalesRequestEvaluationSummaryDTO
            {
                RequestId = requestId,
                SourceCityValue = source,
                Key = $"{source}:{requestId}",
                HasAnyMatch = overall is not null,
                OverallRatingLabel = overall is null ? "لا يوجد تطابق" : CustomerRatingLabels.FromLevel(overall.Value.Level),
                OverallScore = overall?.Score,
                OverallRatingLevel = overall?.Level.ToString(),
                TripleName = tripleCat,
                Phone = phoneCat,
                FatherGrandfather = kinCat
            };
        }

        private static SalesRequestEvaluationCategoryDTO BuildCategory(
            IReadOnlyList<int> ids,
            IReadOnlyDictionary<int, CustomerRatingResult> ratings)
        {
            if (ids.Count == 0)
            {
                return new SalesRequestEvaluationCategoryDTO
                {
                    ResultCount = 0,
                    WorstRatingLabel = "لا توجد نتائج"
                };
            }

            var pairs = ids
                .Select(id => ratings.TryGetValue(id, out var r) ? r : null)
                .Where(r => r is not null)
                .Select(r => (r!.Level, r.Score))
                .ToList();
            var worst = pairs.Count == 0
                ? null
                : SalesRequestEvaluationAggregator.WorstOf(pairs);

            return new SalesRequestEvaluationCategoryDTO
            {
                ResultCount = ids.Count,
                WorstRatingLabel = worst is null ? CustomerRatingLabels.Weak : CustomerRatingLabels.FromLevel(worst.Value.Level),
                WorstScore = worst?.Score,
                WorstRatingLevel = worst?.Level.ToString()
            };
        }

        private static (CustomerRatingLevel Level, int Score)? ToPair(SalesRequestEvaluationCategoryDTO cat)
        {
            if (cat.ResultCount <= 0 || cat.WorstScore is null || string.IsNullOrWhiteSpace(cat.WorstRatingLevel))
            {
                return null;
            }

            if (!Enum.TryParse<CustomerRatingLevel>(cat.WorstRatingLevel, out var level))
            {
                return null;
            }

            return (level, cat.WorstScore.Value);
        }

        private static bool IsKnownCategory(string cat) =>
            cat.Equals(CategoryTriple, StringComparison.OrdinalIgnoreCase)
            || cat.Equals(CategoryPhone, StringComparison.OrdinalIgnoreCase)
            || cat.Equals(CategoryKinship, StringComparison.OrdinalIgnoreCase);

        private static void EnsureManager(SalesIdentity actor)
        {
            if (!SalesRoles.IsSalesManager(actor.UserType)
                && !string.Equals(actor.Role, SalesRoles.SalesManager, StringComparison.Ordinal))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "غير مصرح.");
            }
        }

        private sealed class CategoryMatches(
            List<int> triple,
            List<int> phone,
            List<int> kinship)
        {
            public List<int> Triple { get; } = triple;
            public List<int> Phone { get; } = phone;
            public List<int> Kinship { get; } = kinship;

            public IEnumerable<int> AllCustomerIds() =>
                Triple.Concat(Phone).Concat(Kinship).Distinct();
        }
    }
}
