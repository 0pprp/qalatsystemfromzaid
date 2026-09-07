using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public interface ISalesExcelCustomerSearchService
    {
        Task<SalesExcelSearchResponseDTO> SearchAsync(IReadOnlyList<string> names, CancellationToken ct);
    }

    public sealed class SalesExcelCustomerSearchService : ISalesExcelCustomerSearchService
    {
        private readonly ISalesExcelCustomerSearchCatalog _catalog;

        public SalesExcelCustomerSearchService(ISalesExcelCustomerSearchCatalog catalog)
        {
            _catalog = catalog;
        }

        public async Task<SalesExcelSearchResponseDTO> SearchAsync(IReadOnlyList<string> names, CancellationToken ct)
        {
            if (names.Count > SalesCustomerNameMatch.MaxNames)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    $"عدد الأسماء أكبر من الحد المسموح ({SalesCustomerNameMatch.MaxNames}).");
            }

            var customers = await _catalog.LoadCustomersAsync(ct);
            var queries = new List<SalesExcelSearchQueryDTO>(names.Count);
            var matchedIds = new HashSet<int>();
            var pending = new List<(SalesExcelSearchQueryDTO Query, List<SalesExcelSearchCustomerRow> Hits)>();

            for (var i = 0; i < names.Count; i++)
            {
                var requested = names[i] ?? string.Empty;
                var trimmed = requested.Trim();
                var row = new SalesExcelSearchQueryDTO
                {
                    RowNumber = i + 2,
                    RequestedName = trimmed
                };

                if (trimmed.Length == 0)
                {
                    row.Warning = "الاسم فارغ";
                    queries.Add(row);
                    continue;
                }

                var normalized = SalesArabicText.Normalize(trimmed);
                if (normalized.Length < SalesCustomerNameMatch.MinimumLength)
                {
                    row.Warning = "الاسم قصير جداً للبحث الآمن";
                    queries.Add(row);
                    continue;
                }

                row.UsedFamilySearch = SalesCustomerNameMatch.UsesFamilySearch(normalized);
                row.SearchKey = SalesCustomerNameMatch.FamilySearchKey(normalized);

                var hits = customers
                    .Where(c => SalesCustomerNameMatch.IsMatch(c.FullName, normalized))
                    .GroupBy(c => c.CustomerId)
                    .Select(g => g.First())
                    .ToList();
                row.MatchCount = hits.Count;
                row.Found = hits.Count > 0;
                if (hits.Count > SalesCustomerNameMatch.MaxMatchesPerName)
                {
                    row.Truncated = true;
                    row.Warning = "الاسم عاماً جداً وأعاد عدداً كبيراً من النتائج. عُرضت أول "
                                  + SalesCustomerNameMatch.MaxMatchesPerName + " نتيجة.";
                    hits = hits.Take(SalesCustomerNameMatch.MaxMatchesPerName).ToList();
                }

                foreach (var hit in hits)
                {
                    matchedIds.Add(hit.CustomerId);
                }

                pending.Add((row, hits));
                queries.Add(row);
            }

            var sales = await _catalog.LoadSalesAsync(matchedIds, ct);
            var salesByCustomer = sales
                .GroupBy(s => s.CustomerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SaleDate).ThenByDescending(s => s.SaleId).ToList());

            foreach (var (query, hits) in pending)
            {
                query.Matches = hits.Select(hit => MapMatch(hit, salesByCustomer.GetValueOrDefault(hit.CustomerId))).ToList();
            }

            return new SalesExcelSearchResponseDTO
            {
                ReadOnly = true,
                NameCount = queries.Count,
                FoundCount = queries.Count(q => q.Found),
                MissingCount = queries.Count(q => !q.Found),
                Queries = queries
            };
        }

        private SalesExcelSearchMatchDTO MapMatch(
            SalesExcelSearchCustomerRow customer,
            List<SalesExcelSearchSaleRow>? sales)
        {
            var cityValue = _catalog.CityValue;
            var cityName = DisplayBranch(customer.Province, _catalog.CityName, cityValue);
            var accountReceipts = customer.ReceiptsTotal;
            var accountRemaining = customer.AmountRemaining;
            return new SalesExcelSearchMatchDTO
            {
                ResultKey = cityValue + ":" + customer.CustomerId,
                CityValue = cityValue,
                CityName = cityName,
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                Phone = customer.Phone,
                Province = cityName,
                Address = customer.Address,
                DelegateName = customer.DelegateName,
                DelegateId = customer.DelegateId,
                AmountTotalSales = customer.AmountTotalSales,
                ReceiptsTotal = accountReceipts,
                AmountRemaining = accountRemaining,
                Sales = (sales ?? [])
                    .Select(s => new SalesExcelSearchSaleDTO
                    {
                        SaleId = s.SaleId,
                        SaleDate = s.SaleDate,
                        SaleAmount = s.SaleAmount,
                        ReceiptsTotal = accountReceipts,
                        AmountRemaining = accountRemaining,
                        AccountZero = s.AccountZero
                    })
                    .ToList()
            };
        }

        private static string DisplayBranch(string? province, string catalogName, string cityValue)
        {
            var name = SalesCityDisplay.HumanName(province, catalogName, cityValue);
            if (SalesCityDisplay.IsInternalKey(name, cityValue))
            {
                name = SalesCityDisplay.HumanName(catalogName, null, cityValue);
            }

            return SalesCityDisplay.IsInternalKey(name, cityValue) ? string.Empty : name;
        }
    }
}
