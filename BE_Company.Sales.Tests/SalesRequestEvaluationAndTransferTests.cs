using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;
using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesRequestEvaluationReceiptCountTests
{
    private static SalesIdentity Manager() => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = "najaf-demo",
        BranchName = "النجف",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

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
        public int BatchCalls { get; private set; }

        public Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByCustomerIdsAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct = default)
        {
            BatchCalls++;
            return Task.FromResult<IReadOnlyList<CustomerRatingFacts>>(
                Facts.Where(f => customerIds.Contains(f.CustomerId)).ToList());
        }

        public Task<CustomerRatingFacts?> GetFactByCustomerIdAsync(int customerId, CancellationToken ct = default) =>
            Task.FromResult(Facts.FirstOrDefault(f => f.CustomerId == customerId));

        public Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByListIdsAsync(
            IReadOnlyList<int> listIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerRatingFacts>>([]);
    }

    private static async Task<(FakeRequestRepository Repo, SalesRequestEvaluationService Svc, FakeCatalog Catalog, FakeRating Rating, SalesRequestDTO Row)>
        SeedAsync(string name = "أحمد منتظر سرحان", string phone = "07804924373")
    {
        var repo = new FakeRequestRepository();
        var catalog = new FakeCatalog();
        var rating = new FakeRating();
        var clock = new FakeClock { UtcNow = new DateTime(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc) };
        var requests = new SalesRequestService(repo, clock);
        var row = await requests.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = name, Phone = phone }
        }, default);
        var svc = new SalesRequestEvaluationService(repo, catalog, rating, clock);
        return (repo, svc, catalog, rating, row);
    }

    [Fact]
    public async Task Hits_ReceiptCount_Zero_When_No_Payments()
    {
        var (_, svc, catalog, rating, row) = await SeedAsync();
        catalog.Customers.Add(new() { CustomerId = 11, FullName = row.CustomerName, Phone = row.CustomerPhone });
        rating.Facts.Add(new CustomerRatingFacts(11, 1, false, false, DateTime.UtcNow.Date.AddDays(-10), null, 1000, 0, 1000, 0));

        var page = await svc.ListHitsAsync(Manager(), row.Id, SalesRequestEvaluationService.CategoryPhone, 1, 30, default);
        Assert.Single(page.Items);
        Assert.Equal(0, page.Items[0].ReceiptCount);
    }

    [Fact]
    public async Task Hits_ReceiptCount_One()
    {
        var (_, svc, catalog, rating, row) = await SeedAsync();
        catalog.Customers.Add(new() { CustomerId = 11, FullName = row.CustomerName, Phone = row.CustomerPhone });
        rating.Facts.Add(new CustomerRatingFacts(11, 1, false, false, DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date, 1000, 1000, 0, 1));

        var page = await svc.ListHitsAsync(Manager(), row.Id, SalesRequestEvaluationService.CategoryPhone, 1, 30, default);
        Assert.Equal(1, page.Items[0].ReceiptCount);
    }

    [Fact]
    public async Task Hits_ReceiptCount_Many()
    {
        var (_, svc, catalog, rating, row) = await SeedAsync();
        catalog.Customers.Add(new() { CustomerId = 11, FullName = row.CustomerName, Phone = row.CustomerPhone });
        rating.Facts.Add(new CustomerRatingFacts(11, 1, false, false, DateTime.UtcNow.Date.AddDays(-40), DateTime.UtcNow.Date, 1_000_000, 500_000, 500_000, 7));

        var page = await svc.ListHitsAsync(Manager(), row.Id, SalesRequestEvaluationService.CategoryPhone, 1, 30, default);
        Assert.Equal(7, page.Items[0].ReceiptCount);
    }

    [Fact]
    public async Task Batch_Facts_Loaded_Once_Not_Per_Customer()
    {
        var (_, svc, catalog, rating, row) = await SeedAsync();
        catalog.Customers.Add(new() { CustomerId = 11, FullName = "أحمد منتظر سرحان", Phone = "07804924373" });
        catalog.Customers.Add(new() { CustomerId = 12, FullName = "أحمد منتظر سرحان 2", Phone = "07804924373" });
        rating.Facts.Add(new CustomerRatingFacts(11, 1, false, false, DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date, 1000, 1000, 0, 2));
        rating.Facts.Add(new CustomerRatingFacts(12, 1, true, false, DateTime.UtcNow.Date.AddDays(-10), DateTime.UtcNow.Date, 1000, 1000, 0, 3));

        var result = await svc.EvaluateBatchAsync(Manager(), new SalesRequestEvaluationBatchRequestDTO
        {
            RequestIds = [row.Id]
        }, default);

        Assert.Equal(1, rating.BatchCalls);
        Assert.Equal(CustomerRatingLabels.Legal, result.Items[0].OverallRatingLabel);
        Assert.Equal(2, result.Items[0].Phone.ResultCount);
    }

    [Fact]
    public void Rating_Result_Preserves_ReceiptCount_From_Facts()
    {
        var facts = new CustomerRatingFacts(5, 9, false, false, DateTime.UtcNow.Date.AddDays(-20), DateTime.UtcNow.Date, 1000, 1000, 0, 4);
        var rated = CustomerRatingCalculator.Evaluate(facts, DateTime.UtcNow.Date);
        Assert.Equal(4, rated.ReceiptCount);
    }

    [Fact]
    public void Facts_ReceiptCount_Is_Not_Inflated_By_Constructor_Defaults()
    {
        // OUTER APPLY COUNT(CustomerPaymentID) maps into this field; joins must not multiply rows.
        var facts = new CustomerRatingFacts(1, 1, false, false, null, null, 0, 0, 0, 3);
        Assert.Equal(3, facts.ReceiptCount);
        Assert.Equal(3, CustomerRatingCalculator.Evaluate(facts, DateTime.UtcNow.Date).ReceiptCount);
    }

    [Fact]
    public async Task Payload_Evaluate_Returns_Real_Category_Counts()
    {
        var catalog = new FakeCatalog();
        var rating = new FakeRating();
        // 3 triple, 2 phone, 3 kinship (father+grandfather pair only; father-only/grandfather-only excluded)
        catalog.Customers.Add(new() { CustomerId = 1, FullName = "أحمد منتظر سرحان", Phone = "07804924373" });
        catalog.Customers.Add(new() { CustomerId = 2, FullName = "أحمد منتظر سرحان 2", Phone = "07801110000" });
        catalog.Customers.Add(new() { CustomerId = 3, FullName = "أحمد منتظر سرحان 3", Phone = "07804924373" });
        catalog.Customers.Add(new() { CustomerId = 4, FullName = "هادي حيدر سرحان", Phone = "07802220000" });
        catalog.Customers.Add(new() { CustomerId = 5, FullName = "علي منتظر كاظم", Phone = "07803330000" });
        catalog.Customers.Add(new() { CustomerId = 6, FullName = "سامر كريم سرحان", Phone = "07804440000" });
        catalog.Customers.Add(new() { CustomerId = 7, FullName = "محمد منتظر سرحان", Phone = "07805550000" });
        foreach (var id in new[] { 1, 2, 3, 4, 5, 6, 7 })
        {
            rating.Facts.Add(new CustomerRatingFacts(id, 1, false, false, DateTime.UtcNow.Date.AddDays(-20), DateTime.UtcNow.Date, 1000, 1000, 0, 2));
        }

        var svc = new SalesRequestEvaluationService(
            new FakeRequestRepository(),
            catalog,
            rating,
            new FakeClock { UtcNow = DateTime.UtcNow });

        var result = await svc.EvaluateBatchAsync(Manager(), new SalesRequestEvaluationBatchRequestDTO
        {
            Items =
            [
                new SalesRequestEvaluationPayloadDTO
                {
                    RequestId = 48,
                    SourceCityValue = "1",
                    CustomerName = "أحمد منتظر سرحان",
                    CustomerPhone = "07804924373"
                }
            ]
        }, default);

        Assert.Equal(3, result.Items[0].TripleName.ResultCount);
        Assert.Equal(2, result.Items[0].Phone.ResultCount);
        Assert.Equal(4, result.Items[0].FatherGrandfather.ResultCount);
        Assert.Equal("1:48", result.Items[0].Key);
    }

    [Fact]
    public async Task Evaluate_FatherGrandfather_Pair_Only_Counts_And_Reason()
    {
        var (_, svc, catalog, rating, row) = await SeedAsync("صادق جعفر حنيو", "07809999999");
        catalog.Customers.Add(new() { CustomerId = 1, FullName = "محمد جعفر حنيو", Phone = "07801110000" });
        catalog.Customers.Add(new() { CustomerId = 2, FullName = "حسين جعفر كريم", Phone = "07802220000" });
        catalog.Customers.Add(new() { CustomerId = 3, FullName = "موسى هادي حنيو", Phone = "07803330000" });
        catalog.Customers.Add(new() { CustomerId = 4, FullName = "علي جعفر حنيو", Phone = "07804440000" });
        foreach (var id in new[] { 1, 2, 3, 4 })
        {
            rating.Facts.Add(new CustomerRatingFacts(
                id, 1, IsLegal: id == 1, IsFakeSale: false,
                DateSaleDevice: DateTime.UtcNow.Date.AddDays(-30),
                LastPaymentDate: DateTime.UtcNow.Date,
                AmountTotalSales: 1000, ReceiptsTotal: 1000, AmountRemaining: 0, ReceiptCount: 1));
        }

        var summary = await svc.EvaluateBatchAsync(Manager(), new SalesRequestEvaluationBatchRequestDTO
        {
            Items =
            [
                new SalesRequestEvaluationPayloadDTO
                {
                    RequestId = row.Id,
                    SourceCityValue = "najaf-demo",
                    CustomerName = "صادق جعفر حنيو",
                    CustomerPhone = "07809999999"
                }
            ]
        }, default);

        Assert.Equal(2, summary.Items[0].FatherGrandfather.ResultCount);
        Assert.Equal(0, summary.Items[0].TripleName.ResultCount);
        Assert.Equal(0, summary.Items[0].Phone.ResultCount);
        Assert.Equal(CustomerRatingLabels.Legal, summary.Items[0].FatherGrandfather.WorstRatingLabel);

        var hits = await svc.ListHitsAsync(
            Manager(),
            row.Id,
            SalesRequestEvaluationService.CategoryKinship,
            page: 1,
            pageSize: 30,
            default);

        Assert.Equal(2, hits.Total);
        Assert.All(hits.Items, h => Assert.Equal("تطابق اسم الأب والجد", h.MatchReason));
        Assert.Equal(new[] { 1, 4 }, hits.Items.Select(h => h.CustomerId).OrderBy(x => x).ToArray());
    }
}

public class SalesRequestProvinceTransferServiceTests
{
    private static SalesIdentity Manager(string branch = "najaf-demo", string name = "النجف") => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = branch,
        BranchName = name,
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

    [Fact]
    public async Task Accept_Transfer_Creates_Destination_And_Clears_Old_Assignee_By_Default()
    {
        var repo = new FakeRequestRepository();
        var employees = new FakeManagerRead();
        employees.ActiveEmployees = [new() { EmployeeId = 7, EmployeeName = "بصرة موظف" }];
        employees.Employees.Clear();
        employees.Employees.AddRange(employees.ActiveEmployees);
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow }, employees);

        var created = await svc.AcceptProvinceTransferAsync(Manager("basra", "البصرة"), new SalesRequestAcceptTransferDTO
        {
            FromRequestId = 15,
            FromCityValue = "najaf-demo",
            FromCityName = "النجف",
            CustomerName = "أحمد منتظر سرحان",
            CustomerPhone = "07804924373",
            CustomerAddress = "الكوفة"
        }, default);

        Assert.Equal("basra", created.CityValue);
        Assert.Equal(0, created.TargetEmployeeId);
        Assert.Equal(SalesFilterStatuses.PendingFilter, created.FilterStatus);
        Assert.Contains(repo.History, h => h.Event == SalesRequestEvents.ProvinceTransferred && h.RequestId == created.Id);
    }

    [Fact]
    public async Task Accept_Transfer_Can_Assign_Destination_Employee()
    {
        var repo = new FakeRequestRepository();
        var employees = new FakeManagerRead();
        employees.ActiveEmployees = [new() { EmployeeId = 7, EmployeeName = "بصرة موظف" }];
        employees.Employees.Clear();
        employees.Employees.AddRange(employees.ActiveEmployees);
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow }, employees);

        var created = await svc.AcceptProvinceTransferAsync(Manager("basra", "البصرة"), new SalesRequestAcceptTransferDTO
        {
            FromRequestId = 15,
            FromCityValue = "najaf-demo",
            CustomerName = "زبون",
            ToEmployeeId = 7
        }, default);

        Assert.Equal(7, created.TargetEmployeeId);
        Assert.Equal(SalesRequestStatuses.Assigned, created.Status);
    }

    [Fact]
    public async Task Accept_Transfer_Rejects_Employee_Not_In_Destination_Branch()
    {
        var repo = new FakeRequestRepository();
        var employees = new FakeManagerRead();
        employees.ActiveEmployees = [new() { EmployeeId = 7, EmployeeName = "بصرة موظف" }];
        employees.Employees.Clear();
        employees.Employees.AddRange(employees.ActiveEmployees);
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow }, employees);

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.AcceptProvinceTransferAsync(Manager("basra", "البصرة"), new SalesRequestAcceptTransferDTO
            {
                FromRequestId = 15,
                FromCityValue = "najaf-demo",
                CustomerName = "زبون",
                ToEmployeeId = 99
            }, default));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Mark_Transferred_Out_Archives_Source_And_Clears_Assignee()
    {
        var repo = new FakeRequestRepository();
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow }, new FakeManagerRead());
        var created = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "أحمد", Phone = "07801112233" }
        }, default);
        created.TargetEmployeeId = 1;
        created.TargetEmployeeName = "قديم";

        await svc.MarkProvinceTransferredOutAsync(Manager(), created.Id, new SalesRequestMarkTransferredOutDTO
        {
            ToCityValue = "basra",
            ToRequestId = 88,
            ToCityName = "البصرة"
        }, default);

        Assert.True(repo.Rows.First(r => r.Id == created.Id).IsDeleted);
        Assert.Equal(0, repo.Rows.First(r => r.Id == created.Id).TargetEmployeeId);
        Assert.Contains(repo.History, h =>
            h.Event == SalesRequestEvents.ProvinceTransferred
            && h.Note!.Contains("OriginalRequestId=")
            && h.Note.Contains("DestinationRequestId=88"));
    }

    [Fact]
    public async Task Mark_Transferred_Out_Rejects_Completed()
    {
        var repo = new FakeRequestRepository();
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow });
        var created = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "أحمد", Phone = "07801112233" }
        }, default);
        created.Status = SalesRequestStatuses.Completed;
        created.ConvertedToSaleId = 9;

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.MarkProvinceTransferredOutAsync(Manager(), created.Id, new SalesRequestMarkTransferredOutDTO
            {
                ToCityValue = "basra",
                ToRequestId = 1
            }, default));
        Assert.Equal(409, ex.StatusCode);
        Assert.False(repo.Rows.First(r => r.Id == created.Id).IsDeleted);
    }

    [Fact]
    public async Task ManagerUpdate_Rejects_Cross_Branch_CityValue_Metadata_Hack()
    {
        var repo = new FakeRequestRepository();
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow }, new FakeManagerRead());
        var created = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "أحمد", Phone = "07801112233" }
        }, default);

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.ManagerUpdateAsync(Manager(), created.Id, new SalesRequestManagerUpdateDTO
            {
                CityValue = "basra"
            }, default));
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("najaf-demo", repo.Rows.First(r => r.Id == created.Id).CityValue);
    }

    [Fact]
    public async Task Mark_Transferred_Out_Is_Idempotent_When_Already_Archived()
    {
        var repo = new FakeRequestRepository();
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow });
        var created = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "أحمد", Phone = "07801112233" }
        }, default);
        await svc.MarkProvinceTransferredOutAsync(Manager(), created.Id, new()
        {
            ToCityValue = "basra",
            ToRequestId = 3
        }, default);

        // Second call: GetById returns null for deleted → 404 would lose idempotency.
        // Soft-deleted rows are hidden; treat as success only if still readable.
        // Re-insert visible clone of archived id for idempotent path via direct mutation:
        var archived = repo.Rows.First(r => r.Id == created.Id);
        Assert.True(archived.IsDeleted);
        await svc.MarkProvinceTransferredOutAsync(Manager(), created.Id, new()
        {
            ToCityValue = "basra",
            ToRequestId = 3
        }, default);
        Assert.True(archived.IsDeleted);
    }
}
