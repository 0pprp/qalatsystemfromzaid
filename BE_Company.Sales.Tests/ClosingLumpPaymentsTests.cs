using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class ClosingLumpPaymentsTests
{
    private static CustomerPaymentSlice Pay(DateTime day, double amount, int customerId = 11) =>
        new() { CustomerId = customerId, PaymentDate = day, Amount = amount };

    [Fact]
    public void Includes_Payment_On_Last_Day_At_Least_20_Percent()
    {
        var notes = ClosingLumpPayments.Find(
        [
            Pay(new DateTime(2026, 6, 1), 500_000),
            Pay(new DateTime(2026, 11, 2), 1_000_000)
        ], 5_000_000, 0);

        Assert.Single(notes);
        Assert.Equal(1_000_000, notes[0].Amount);
        Assert.Equal(new DateTime(2026, 11, 2), notes[0].PaymentDate);
        Assert.Equal("في يوم 2/11 دفع 1,000,000 دينار", notes[0].Text);
    }

    [Fact]
    public void Includes_The_Day_Exactly_30_Days_Before_Last_Payment()
    {
        var last = new DateTime(2026, 11, 2);
        var notes = ClosingLumpPayments.Find(
        [
            Pay(last.AddDays(-30), 1_000_000),
            Pay(last.AddDays(-31), 2_000_000),
            Pay(last, 100_000)
        ], 5_000_000, 0);

        Assert.Single(notes);
        Assert.Equal(last.AddDays(-30), notes[0].PaymentDate);
    }

    [Fact]
    public void Skips_Open_Accounts_And_Payments_Under_20_Percent()
    {
        var day = new DateTime(2026, 11, 2);
        Assert.Empty(ClosingLumpPayments.Find([Pay(day, 2_000_000)], 5_000_000, 1_000));
        Assert.Empty(ClosingLumpPayments.Find([Pay(day, 999_999)], 5_000_000, 0));
        Assert.Empty(ClosingLumpPayments.Find([Pay(day, 1_000_000)], 0, 0));
    }

    [Fact]
    public void Returns_Every_Qualifying_Payment_Newest_First()
    {
        var notes = ClosingLumpPayments.Find(
        [
            Pay(new DateTime(2026, 10, 20), 1_500_000),
            Pay(new DateTime(2026, 11, 2), 1_000_000),
            Pay(new DateTime(2026, 10, 28), 400_000)
        ], 5_000_000, 0);

        Assert.Equal(2, notes.Count);
        Assert.Equal(new DateTime(2026, 11, 2), notes[0].PaymentDate);
        Assert.Equal(new DateTime(2026, 10, 20), notes[1].PaymentDate);
    }

    [Fact]
    public void Name_And_Phone_Categories_Only()
    {
        Assert.True(ClosingLumpPayments.AppliesToCategory(SalesRequestEvaluationService.CategoryPhone));
        Assert.True(ClosingLumpPayments.AppliesToCategory(SalesRequestEvaluationService.CategoryTriple));
        Assert.False(ClosingLumpPayments.AppliesToCategory(SalesRequestEvaluationService.CategoryKinship));
        Assert.False(ClosingLumpPayments.AppliesToCategory(SalesRequestEvaluationService.CategorySonFather));
    }

    [Fact]
    public async Task Phone_Hit_Carries_The_Closing_Lump_Note()
    {
        var (repo, catalog, rating, row) = await SeedAsync();
        catalog.Customers.Add(new() { CustomerId = 11, FullName = row.CustomerName, Phone = row.CustomerPhone });
        rating.Facts.Add(new CustomerRatingFacts(
            11, 1, false, false,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 11, 2),
            5_000_000, 5_000_000, 0, 3));
        var payments = new FakePayments
        {
            Rows =
            [
                Pay(new DateTime(2026, 6, 1), 500_000),
                Pay(new DateTime(2026, 11, 2), 1_000_000)
            ]
        };
        var svc = new SalesRequestEvaluationService(
            repo, catalog, rating, new FakeClock { UtcNow = new DateTime(2026, 11, 3) }, payments);

        var page = await svc.ListHitsAsync(
            Manager(), row.Id, SalesRequestEvaluationService.CategoryPhone, 1, 30, default);

        var note = Assert.Single(page.Items[0].LargeClosingPayments);
        Assert.Equal("في يوم 2/11 دفع 1,000,000 دينار", note.Text);
    }

    [Fact]
    public async Task Kinship_Hit_And_Open_Account_Stay_Unmarked()
    {
        var (repo, catalog, rating, row) = await SeedAsync();
        catalog.Customers.Add(new()
        {
            CustomerId = 21,
            FullName = "كريم منتظر سرحان",
            Phone = "07809990000"
        });
        catalog.Customers.Add(new() { CustomerId = 22, FullName = row.CustomerName, Phone = row.CustomerPhone });
        rating.Facts.Add(new CustomerRatingFacts(
            21, 1, false, false,
            new DateTime(2026, 1, 1), new DateTime(2026, 11, 2),
            5_000_000, 5_000_000, 0, 2));
        rating.Facts.Add(new CustomerRatingFacts(
            22, 1, false, false,
            new DateTime(2026, 1, 1), new DateTime(2026, 11, 2),
            5_000_000, 2_000_000, 3_000_000, 2));
        var payments = new FakePayments
        {
            Rows =
            [
                Pay(new DateTime(2026, 11, 2), 2_000_000, 21),
                Pay(new DateTime(2026, 11, 2), 2_000_000, 22)
            ]
        };
        var svc = new SalesRequestEvaluationService(
            repo, catalog, rating, new FakeClock { UtcNow = new DateTime(2026, 11, 3) }, payments);

        var kinship = await svc.ListHitsAsync(
            Manager(), row.Id, SalesRequestEvaluationService.CategoryKinship, 1, 30, default);
        var phone = await svc.ListHitsAsync(
            Manager(), row.Id, SalesRequestEvaluationService.CategoryPhone, 1, 30, default);

        Assert.Contains(kinship.Items, item => item.CustomerId == 21);
        Assert.All(kinship.Items, item => Assert.Empty(item.LargeClosingPayments));
        var open = Assert.Single(phone.Items, item => item.CustomerId == 22);
        Assert.Empty(open.LargeClosingPayments);
    }

    private static SalesIdentity Manager() => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = "najaf-demo",
        BranchName = "النجف",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

    private static async Task<(FakeRequestRepository Repo, FakeCatalog Catalog, FakeRating Rating, SalesRequestDTO Row)>
        SeedAsync()
    {
        var repo = new FakeRequestRepository();
        var catalog = new FakeCatalog();
        var rating = new FakeRating();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc) };
        var requests = new SalesRequestService(repo, clock);
        var row = await requests.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "أحمد منتظر سرحان", Phone = "07804924373" }
        }, default);
        return (repo, catalog, rating, row);
    }

    private sealed class FakeCatalog : ISalesExcelCustomerSearchCatalog
    {
        public string CityValue { get; set; } = "najaf-demo";
        public string CityName { get; set; } = "النجف";
        public int WriteCount => 0;
        public List<SalesExcelSearchCustomerRow> Customers { get; } = [];

        public Task<IReadOnlyList<SalesExcelSearchCustomerRow>> LoadCustomersAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesExcelSearchCustomerRow>>(Customers);

        public Task<IReadOnlyList<SalesExcelSearchSaleRow>> LoadSalesAsync(
            IReadOnlyCollection<int> customerIds,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesExcelSearchSaleRow>>([]);
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

    private sealed class FakePayments : ICustomerPaymentLookup
    {
        public List<CustomerPaymentSlice> Rows { get; init; } = [];

        public Task<IReadOnlyList<CustomerPaymentSlice>> GetByCustomerIdsAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CustomerPaymentSlice>>(
                Rows.Where(row => customerIds.Contains(row.CustomerId)).ToList());
    }
}
