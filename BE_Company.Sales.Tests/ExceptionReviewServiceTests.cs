using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class ExceptionReviewServiceTests
{
    private static SalesIdentity Manager(bool gateway = true) => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = "basra-demo",
        BranchName = "البصرة",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager,
        IsGateway = gateway
    };

    private sealed class FakeCatalog : ISalesExcelCustomerSearchCatalog
    {
        public string CityValue { get; set; } = "basra-demo";
        public string CityName { get; set; } = "البصرة";
        public int WriteCount => 0;
        public List<SalesExcelSearchCustomerRow> Customers { get; } = [];
        public List<SalesExcelSearchSaleRow> Sales { get; } = [];

        public Task<IReadOnlyList<SalesExcelSearchCustomerRow>> LoadCustomersAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesExcelSearchCustomerRow>>(Customers);

        public Task<IReadOnlyList<SalesExcelSearchSaleRow>> LoadSalesAsync(
            IReadOnlyCollection<int> customerIds,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesExcelSearchSaleRow>>(
                Sales.Where(s => customerIds.Contains(s.CustomerId)).ToList());
    }

    private sealed class FakeRating : IRatingDataSource
    {
        public List<CustomerRatingFacts> Facts { get; } = [];

        public Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByCustomerIdsAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerRatingFacts>>(
                Facts.Where(f => customerIds.Contains(f.CustomerId)).ToList());

        public Task<CustomerRatingFacts?> GetFactByCustomerIdAsync(int customerId, CancellationToken ct = default) =>
            Task.FromResult(Facts.FirstOrDefault(f => f.CustomerId == customerId));

        public Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByListIdsAsync(
            IReadOnlyList<int> listIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerRatingFacts>>([]);
    }

    private static async Task<(FakeRequestRepository Repo, ExceptionReviewService Svc, FakeCatalog Catalog, FakeRating Rating, SalesRequestDTO Row)>
        SeedAsync(
            string name = "محمد علي حسن",
            string phone = "07801111111",
            string source = SalesRequestSources.Delegate,
            string? createdBy = "أحمد علي",
            int? listId = 7)
    {
        var repo = new FakeRequestRepository();
        var catalog = new FakeCatalog();
        var rating = new FakeRating();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc) };
        var requests = new SalesRequestService(repo, clock);
        var row = await requests.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = name, Phone = phone, Province = "البصرة", Address = "المعقل" }
        }, default);
        row.CityValue = "basra-demo";
        row.CityName = "البصرة";
        row.CustomerSourceType = source;
        row.CreatedByName = createdBy;
        row.SourceListId = listId;
        row.Notes = "نوع المبيع: ثلاجة";
        await repo.UpdateAsync(row, default);

        var svc = new ExceptionReviewService(repo, catalog, rating, clock);
        return (repo, svc, catalog, rating, row);
    }

    [Fact]
    public async Task Loads_Original_SalesRequest_And_Source()
    {
        var (_, svc, _, _, row) = await SeedAsync();
        var ctx = await svc.BuildContextAsync(Manager(), row.Id, default);
        Assert.NotNull(ctx.CurrentRequest);
        Assert.Equal(row.Id, ctx.CurrentRequest!.SalesRequestId);
        Assert.Equal("محمد علي حسن", ctx.CurrentRequest.CustomerName);
        Assert.Equal("ثلاجة", ctx.CurrentRequest.SaleTypeOrProduct);
        Assert.Equal("مندوب", ctx.Source.DisplayLabel);
        Assert.Equal("أحمد علي", ctx.Source.PersonName);
        Assert.Equal(ExceptionReviewSourceMapper.Unavailable, ctx.Source.ListName);
    }

    [Fact]
    public async Task Missing_SalesRequest_Returns_Clear_Error()
    {
        var repo = new FakeRequestRepository();
        var svc = new ExceptionReviewService(
            repo,
            new FakeCatalog(),
            new FakeRating(),
            new FakeClock { UtcNow = DateTime.UtcNow });
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(
            () => svc.BuildContextAsync(Manager(), 99999, default));
        Assert.Equal(404, ex.StatusCode);
        Assert.Contains("طلب البيع الأصلي", ex.Message);
    }

    [Fact]
    public async Task Returns_History_Rating_Newest_First_And_Legal()
    {
        var (_, svc, catalog, rating, row) = await SeedAsync();
        catalog.Customers.Add(new()
        {
            CustomerId = 42,
            FullName = row.CustomerName,
            Phone = row.CustomerPhone,
            Province = "البصرة"
        });
        rating.Facts.Add(new CustomerRatingFacts(
            42, 1, IsLegal: true, IsFakeSale: false,
            DateSaleDevice: new DateTime(2026, 1, 1),
            LastPaymentDate: new DateTime(2026, 2, 1),
            AmountTotalSales: 5000,
            ReceiptsTotal: 2000,
            AmountRemaining: 3000,
            ReceiptCount: 2));
        catalog.Sales.Add(new() { CustomerId = 42, SaleId = 1, SaleDate = new DateTime(2025, 1, 1), SaleAmount = 1000, AccountZero = true });
        catalog.Sales.Add(new() { CustomerId = 42, SaleId = 2, SaleDate = new DateTime(2026, 3, 1), SaleAmount = 4000, AccountZero = false });

        var ctx = await svc.BuildContextAsync(Manager(), row.Id, default);
        Assert.Equal("Existing", ctx.CustomerClassification.Type);
        Assert.Single(ctx.MatchingCustomers);
        var m = ctx.MatchingCustomers[0];
        Assert.Equal(CustomerRatingLabels.Legal, m.RatingLabel);
        Assert.True(m.IsLegal);
        Assert.Equal("قانونية", m.FinancialSummary.LegalStatus);
        Assert.Equal(2, m.PreviousSales[0].SaleId); // newest first
        Assert.Equal(1, m.PreviousSales[1].SaleId);
    }

    [Fact]
    public async Task New_When_Only_Kinship_In_Catalog()
    {
        var (_, svc, catalog, _, row) = await SeedAsync(name: "صادق جعفر حنيو", phone: "07801111111");
        catalog.Customers.Add(new()
        {
            CustomerId = 55,
            FullName = "محمد جعفر حنيو",
            Phone = "07802222222",
            Province = "البصرة"
        });
        var ctx = await svc.BuildContextAsync(Manager(), row.Id, default);
        Assert.Equal("New", ctx.CustomerClassification.Type);
        Assert.Empty(ctx.MatchingCustomers);
    }

    [Fact]
    public async Task PreviousSales_Have_Independent_PaymentCount_And_RepaymentDays()
    {
        var (_, svc, catalog, _, row) = await SeedAsync();
        catalog.CityName = "DatabaseCompanyBasra";
        catalog.CityValue = "basra-demo";
        catalog.Customers.Add(new()
        {
            CustomerId = 77,
            FullName = row.CustomerName,
            Phone = row.CustomerPhone,
            Province = "البصرة"
        });
        catalog.Sales.Add(new()
        {
            CustomerId = 77,
            SaleId = 10,
            SaleDate = new DateTime(2026, 1, 1),
            SaleAmount = 1_250_000,
            PaidAmount = 1_250_000,
            RemainingAmount = 0,
            AccountZero = true,
            PaymentCount = 8,
            LastPaymentDate = new DateTime(2026, 5, 22),
            ItemsNames = "ثلاجة"
        });
        catalog.Sales.Add(new()
        {
            CustomerId = 77,
            SaleId = 11,
            SaleDate = new DateTime(2026, 6, 1),
            SaleAmount = 500_000,
            PaidAmount = 100_000,
            RemainingAmount = 400_000,
            AccountZero = false,
            PaymentCount = 2,
            LastPaymentDate = new DateTime(2026, 7, 1),
            ItemsNames = "مكيف"
        });

        var ctx = await svc.BuildContextAsync(Manager(), row.Id, default);
        var sales = ctx.MatchingCustomers.Single().PreviousSales;
        Assert.Equal(2, sales.Count);

        var paid = sales.Single(s => s.SaleId == 10);
        Assert.Equal(8, paid.PaymentCount);
        Assert.Equal(142, paid.RepaymentDays); // 1 Jan → 22 May inclusive
        Assert.Equal("مصفر", paid.AccountStatusArabic);
        Assert.Equal("البصرة", paid.FriendlyCityName);
        Assert.DoesNotContain("DatabaseCompany", paid.FriendlyCityName ?? "");

        var open = sales.Single(s => s.SaleId == 11);
        Assert.Equal(2, open.PaymentCount);
        Assert.Equal(108, open.RepaymentDays); // 1 Jun → 16 Sep (asOf) inclusive
        Assert.Equal("مفتوح", open.AccountStatusArabic);
        Assert.NotEqual(paid.PaymentCount, open.PaymentCount);
        Assert.NotEqual(paid.RepaymentDays, open.RepaymentDays);
    }

    [Fact]
    public async Task FriendlyCity_Never_Exposes_DatabaseCompany_Keys()
    {
        var (repo, svc, catalog, _, row) = await SeedAsync();
        catalog.CityName = "DatabaseCompanyNajaf";
        catalog.CityValue = "najaf-demo";
        row.CityName = "DatabaseCompanyNajaf";
        row.CityValue = "najaf-demo";
        row.CustomerProvince = "DatabaseCompanyNajaf";
        await repo.UpdateAsync(row, default);

        var ctx = await svc.BuildContextAsync(Manager(), row.Id, default);
        Assert.Equal("النجف", ctx.CurrentRequest!.CityName);
        Assert.Equal("النجف", ctx.Source.BranchName);
        Assert.DoesNotContain("DatabaseCompany", ctx.CurrentRequest.CityName ?? "");
    }
}
