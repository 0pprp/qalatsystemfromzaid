using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class SalesExcelCustomerSearchTests
    {
        [Fact]
        public void ExcelSearchEndpoint_IsSalesManagerOnly_AndReadOnlyRoute()
        {
            Assert.Equal(
                SalesPolicies.SalesManager,
                typeof(SalesManagerController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
            Assert.NotNull(typeof(SalesManagerController).GetMethod(nameof(SalesManagerController.ExcelSearchCustomers)));
        }

        [Fact]
        public void ArabicNormalization_AlefYaTatweelAndSpaces()
        {
            Assert.Equal("حسين محمد", SalesArabicText.Normalize("  حـسين   محمـد  "));
            Assert.Equal("احمد علي", SalesArabicText.Normalize("أحمد على"));
            Assert.Equal("ابراهيم", SalesArabicText.Normalize("إبراهيم"));
        }

        [Fact]
        public void LogicalMatch_ExactAndLongerSamePerson_NotContains()
        {
            Assert.True(SalesCustomerNameMatch.IsLogicalMatch("حسين محمد", "حسين محمد"));
            Assert.True(SalesCustomerNameMatch.IsLogicalMatch("حسين محمد علي", "حسين محمد"));
            Assert.True(SalesCustomerNameMatch.IsLogicalMatch("حسين محمد حسن", "حسين محمد"));
            Assert.False(SalesCustomerNameMatch.IsLogicalMatch("علي حسين محمد", "حسين محمد"));
            Assert.False(SalesCustomerNameMatch.IsLogicalMatch("حسين محمد علي", "حسين"));
            Assert.True(SalesCustomerNameMatch.IsLogicalMatch("حسين محمد 2", SalesArabicText.Normalize("حسين محمد 2")));
            Assert.False(SalesCustomerNameMatch.IsLogicalMatch("حسين محمد", SalesArabicText.Normalize("حسين محمد 2")));
        }

        [Fact]
        public async Task MissingName_ReturnsZero()
        {
            var result = await Search("اسم غير موجود");
            var query = Assert.Single(result.Queries);
            Assert.Equal("اسم غير موجود", query.RequestedName);
            Assert.False(query.Found);
            Assert.Equal(0, query.MatchCount);
            Assert.Empty(query.Matches);
        }

        [Fact]
        public async Task SingleExactName_ReturnsOneCustomer()
        {
            var result = await SearchWith(SeedKarbala(), "علي جاسم");
            var query = Assert.Single(result.Queries);
            Assert.True(query.Found);
            Assert.Equal(1, query.MatchCount);
            var match = Assert.Single(query.Matches);
            Assert.Equal(20, match.CustomerId);
            Assert.Equal("karbala:20", match.ResultKey);
        }

        [Fact]
        public async Task ShortName_ReturnsAllLongerCustomers()
        {
            var result = await Search("حسين محمد");
            var query = Assert.Single(result.Queries);
            Assert.Equal(4, query.MatchCount);
            Assert.Equal(new[] { 1, 2, 3, 4 }, query.Matches.Select(m => m.CustomerId).OrderBy(id => id));
        }

        [Fact]
        public async Task CustomerWithMultipleSales_ReturnsEverySale()
        {
            var result = await Search("حسين محمد علي");
            var match = Assert.Single(Assert.Single(result.Queries).Matches);
            Assert.Equal(2, match.Sales.Count);
            Assert.Equal(new[] { 101, 102 }, match.Sales.Select(s => s.SaleId).OrderBy(id => id));
            Assert.All(match.Sales, s => Assert.Equal(400000, s.AmountRemaining));
            Assert.All(match.Sales, s => Assert.Equal(600000, s.ReceiptsTotal));
        }

        [Fact]
        public async Task SameNameInTwoBranches_KeepsSeparateResultKeys()
        {
            var najaf = await SearchWith(SeedNajaf(), "حسين محمد");
            var karbala = await SearchWith(SeedKarbala(), "حسين محمد");
            var keys = najaf.Queries[0].Matches.Select(m => m.ResultKey)
                .Concat(karbala.Queries[0].Matches.Select(m => m.ResultKey))
                .ToList();
            Assert.Contains("najaf:1", keys);
            Assert.Contains("karbala:21", keys);
            Assert.Distinct(keys);
        }

        [Fact]
        public async Task RequestedName_PreservesOriginalExcelText()
        {
            var result = await Search("  حسين محمد 2  ");
            Assert.Equal("حسين محمد 2", result.Queries[0].RequestedName);
            Assert.True(result.Queries[0].Found);
            Assert.Equal(4, Assert.Single(result.Queries[0].Matches).CustomerId);
        }

        [Fact]
        public async Task ReportFlatten_IncludesEveryMatchAndSale()
        {
            var result = await Search("حسين محمد");
            var rows = Flatten(result);
            Assert.Equal(5, rows.Count);
            Assert.Contains(rows, r => r.CustomerId == 1 && r.SaleId == 11);
            Assert.Contains(rows, r => r.CustomerId == 2 && r.SaleId == 101);
            Assert.Contains(rows, r => r.CustomerId == 2 && r.SaleId == 102);
            Assert.Contains(rows, r => r.CustomerId == 3 && r.SaleId is null);
            Assert.Contains(rows, r => r.CustomerId == 4 && r.SaleId is null);
            Assert.All(rows, r => Assert.Equal("حسين محمد", r.RequestedName));
        }

        [Fact]
        public async Task Search_DoesNotWriteAnyData()
        {
            var catalog = SeedNajaf();
            await SearchWith(catalog, "حسين محمد", "علي جاسم");
            Assert.Equal(0, catalog.WriteCount);
        }

        [Fact]
        public async Task TooManyNames_IsRejected()
        {
            var names = Enumerable.Range(1, SalesCustomerNameMatch.MaxNames + 1).Select(i => "اسم " + i).ToList();
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                new SalesExcelCustomerSearchService(SeedNajaf()).SearchAsync(names, CancellationToken.None));
            Assert.Equal(400, ex.StatusCode);
        }

        private static Task<SalesExcelSearchResponseDTO> Search(params string[] names) =>
            SearchWith(SeedNajaf(), names);

        private static Task<SalesExcelSearchResponseDTO> SearchWith(
            FakeExcelSearchCatalog catalog,
            params string[] names) =>
            new SalesExcelCustomerSearchService(catalog).SearchAsync(names, CancellationToken.None);

        private static FakeExcelSearchCatalog SeedNajaf()
        {
            var catalog = new FakeExcelSearchCatalog { CityValue = "najaf", CityName = "النجف" };
            catalog.Customers.AddRange(
            [
                Row(1, "حسين محمد", 1000000, 200000, 800000),
                Row(2, "حسين محمد علي", 1000000, 600000, 400000),
                Row(3, "حسين محمد حسن", 0, 0, 0),
                Row(4, "حسين محمد 2", 300000, 0, 300000),
                Row(5, "علي حسين محمد", 0, 0, 0)
            ]);
            catalog.Sales.AddRange(
            [
                new SalesExcelSearchSaleRow { CustomerId = 1, SaleId = 11, SaleDate = new DateTime(2026, 1, 2), SaleAmount = 1000000 },
                new SalesExcelSearchSaleRow { CustomerId = 2, SaleId = 102, SaleDate = new DateTime(2026, 3, 1), SaleAmount = 400000 },
                new SalesExcelSearchSaleRow { CustomerId = 2, SaleId = 101, SaleDate = new DateTime(2026, 2, 1), SaleAmount = 600000 }
            ]);
            return catalog;
        }

        private static FakeExcelSearchCatalog SeedKarbala()
        {
            var catalog = new FakeExcelSearchCatalog { CityValue = "karbala", CityName = "كربلاء" };
            catalog.Customers.Add(Row(20, "علي جاسم", 500000, 100000, 400000));
            catalog.Customers.Add(Row(21, "حسين محمد كاظم", 0, 0, 0));
            catalog.Sales.Add(new SalesExcelSearchSaleRow
            {
                CustomerId = 20,
                SaleId = 77,
                SaleDate = new DateTime(2026, 4, 4),
                SaleAmount = 500000
            });
            return catalog;
        }

        private static SalesExcelSearchCustomerRow Row(
            int id,
            string name,
            double total,
            double received,
            double remaining) => new()
        {
            CustomerId = id,
            FullName = name,
            Phone = "0770000000" + id,
            Province = "النجف",
            Address = "حي " + id,
            DelegateName = "قائمة " + id,
            DelegateId = id,
            AmountTotalSales = total,
            ReceiptsTotal = received,
            AmountRemaining = remaining
        };

        private static List<(string RequestedName, int CustomerId, int? SaleId)> Flatten(SalesExcelSearchResponseDTO result)
        {
            var rows = new List<(string RequestedName, int CustomerId, int? SaleId)>();
            foreach (var query in result.Queries)
            {
                foreach (var match in query.Matches)
                {
                    if (match.Sales.Count == 0)
                    {
                        rows.Add((query.RequestedName, match.CustomerId, null));
                        continue;
                    }

                    foreach (var sale in match.Sales)
                    {
                        rows.Add((query.RequestedName, match.CustomerId, sale.SaleId));
                    }
                }
            }

            return rows;
        }

        private sealed class FakeExcelSearchCatalog : ISalesExcelCustomerSearchCatalog
        {
            public string CityValue { get; set; } = "najaf";
            public string CityName { get; set; } = "النجف";
            public int WriteCount { get; private set; }
            public List<SalesExcelSearchCustomerRow> Customers { get; } = [];
            public List<SalesExcelSearchSaleRow> Sales { get; } = [];

            public Task<IReadOnlyList<SalesExcelSearchCustomerRow>> LoadCustomersAsync(CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<SalesExcelSearchCustomerRow>>(Customers.ToList());

            public Task<IReadOnlyList<SalesExcelSearchSaleRow>> LoadSalesAsync(
                IReadOnlyCollection<int> customerIds,
                CancellationToken ct) =>
                Task.FromResult<IReadOnlyList<SalesExcelSearchSaleRow>>(
                    Sales.Where(s => customerIds.Contains(s.CustomerId)).ToList());
        }
    }
}
