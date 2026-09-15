using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SalesExceptionServiceTests
{
    private static (SalesExceptionService Service, IntentRecordingBranchNotePoster Notes) CreateService()
    {
        var notes = new IntentRecordingBranchNotePoster();
        return (new SalesExceptionService(new InMemorySalesExceptionStore(), notes), notes);
    }

    private static GatewayUser Manager(string userName = "mgr-najaf", string cityValue = "najaf-demo") => new()
    {
        UserID = "11",
        UserName = userName,
        UserType = SalesRoles.UserTypeSalesManager,
        CityValue = cityValue,
        CityName = "النجف - DEMO"
    };

    private static GatewayUser DelegatedManager(string userName = "المدير المفوض") => new()
    {
        UserID = "0",
        UserName = userName,
        UserType = SalesRoles.UserTypeDelegatedManager,
        IsCentral = true
    };

    private static GatewayUser SalesEmployee() => new()
    {
        UserID = "44",
        UserName = "emp",
        UserType = SalesRoles.UserTypeSalesEmployee,
        CityValue = "najaf-demo"
    };

    private static CreateSalesExceptionInput Input(string reason = "العميل متعاون ويستحق استثناء", int? salesRequestId = null) => new()
    {
        Reason = reason,
        CustomerId = 501,
        CustomerName = "عميل تجريبي",
        SalesRequestId = salesRequestId
    };

    private static async Task<Guid> CreatePendingAsync(SalesExceptionService service, GatewayUser? manager = null)
    {
        var created = await service.CreateAsync(manager ?? Manager(), Input());
        Assert.True(created.Ok);
        return created.Request!.Id;
    }

    [Fact]
    public async Task Create_StartsPending_WithRequesterIdentityAndAuditRow()
    {
        var (service, _) = CreateService();

        var result = await service.CreateAsync(Manager(), Input());

        Assert.True(result.Ok);
        var request = result.Request!;
        Assert.Equal(ExceptionStatuses.Pending, request.Status);
        Assert.Equal(TargetApproverTypes.DelegatedManager, request.TargetApproverType);
        Assert.Equal("mgr-najaf", request.RequestingManagerUserName);
        Assert.Equal("najaf-demo", request.CityValue);
        Assert.Null(request.DecidedAtUtc);
        Assert.False(request.BranchCustomerNotePosted);

        var detail = await service.GetAsync(request.Id);
        var audit = Assert.Single(detail!.Audit);
        Assert.Null(audit.PreviousStatus);
        Assert.Equal(ExceptionStatuses.Pending, audit.NewStatus);
        Assert.Equal(SalesRoles.SalesManager, audit.ActorRole);
    }

    [Fact]
    public async Task Create_RequiresSalesManagerRole()
    {
        var (service, _) = CreateService();

        var asEmployee = await service.CreateAsync(SalesEmployee(), Input());
        var asDelegatedManager = await service.CreateAsync(DelegatedManager(), Input());

        Assert.Equal(SalesExceptionOutcome.Forbidden, asEmployee.Outcome);
        Assert.Equal(SalesExceptionOutcome.Forbidden, asDelegatedManager.Outcome);
    }

    [Fact]
    public async Task Create_RequiresReasonAndCity()
    {
        var (service, _) = CreateService();

        var noReason = await service.CreateAsync(Manager(), new CreateSalesExceptionInput { Reason = "   " });
        var noCity = await service.CreateAsync(
            new GatewayUser { UserName = "mgr", UserType = SalesRoles.UserTypeSalesManager },
            new CreateSalesExceptionInput { Reason = "سبب" });

        Assert.Equal(SalesExceptionOutcome.Invalid, noReason.Outcome);
        Assert.Equal(SalesExceptionOutcome.Invalid, noCity.Outcome);
    }

    [Fact]
    public async Task Create_RejectsUnknownTargetApproverType()
    {
        var (service, _) = CreateService();

        var result = await service.CreateAsync(Manager(), new CreateSalesExceptionInput
        {
            Reason = "سبب",
            TargetApproverType = "CEO"
        });

        Assert.Equal(SalesExceptionOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Approve_MovesToApproved_RecordsDecisionAndBranchNoteIntent()
    {
        var (service, notes) = CreateService();
        var id = await CreatePendingAsync(service);

        var result = await service.DecideAsync(DelegatedManager(), id, "approve", "موافق استثنائياً");

        Assert.True(result.Ok);
        var request = result.Request!;
        Assert.Equal(ExceptionStatuses.Approved, request.Status);
        Assert.Equal("موافق استثنائياً", request.DecisionNote);
        Assert.Equal("المدير المفوض", request.DecisionMakerDisplayName);
        Assert.NotNull(request.DecidedAtUtc);
        Assert.True(request.BranchCustomerNotePosted);

        var intent = Assert.Single(notes.Intents);
        Assert.Equal(id, intent.ExceptionRequestId);
        Assert.Equal("najaf-demo", intent.CityValue);
        Assert.Equal(501, intent.CustomerId);
    }

    [Fact]
    public async Task Reject_MovesToRejected_AndPostsNoBranchNote()
    {
        var (service, notes) = CreateService();
        var id = await CreatePendingAsync(service);

        var result = await service.DecideAsync(DelegatedManager(), id, "reject", "غير مقبول");

        Assert.True(result.Ok);
        Assert.Equal(ExceptionStatuses.Rejected, result.Request!.Status);
        Assert.False(result.Request.BranchCustomerNotePosted);
        Assert.Empty(notes.Intents);
    }

    [Fact]
    public async Task DoubleDecide_ReturnsConflict_AndKeepsFirstDecision()
    {
        var (service, _) = CreateService();
        var id = await CreatePendingAsync(service);

        var first = await service.DecideAsync(DelegatedManager(), id, "approve", "الأولى");
        var second = await service.DecideAsync(DelegatedManager(), id, "reject", "الثانية");

        Assert.True(first.Ok);
        Assert.Equal(SalesExceptionOutcome.Conflict, second.Outcome);

        var detail = await service.GetAsync(id);
        Assert.Equal(ExceptionStatuses.Approved, detail!.Request.Status);
        Assert.Equal("الأولى", detail.Request.DecisionNote);
        Assert.Equal(2, detail.Audit.Count);
    }

    [Fact]
    public async Task ConcurrentDecisions_OnlyOneWins()
    {
        var (service, _) = CreateService();
        var id = await CreatePendingAsync(service);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => service.DecideAsync(DelegatedManager(), id, i % 2 == 0 ? "approve" : "reject")));

        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.Equal(7, results.Count(r => r.Outcome == SalesExceptionOutcome.Conflict));
    }

    [Fact]
    public async Task Decide_RejectsUnknownDecisionVerb()
    {
        var (service, _) = CreateService();
        var id = await CreatePendingAsync(service);

        var result = await service.DecideAsync(DelegatedManager(), id, "maybe", null);

        Assert.Equal(SalesExceptionOutcome.Invalid, result.Outcome);
        Assert.Equal(ExceptionStatuses.Pending, (await service.GetAsync(id))!.Request.Status);
    }

    [Fact]
    public async Task Decide_ReturnsNotFoundForUnknownId()
    {
        var (service, _) = CreateService();

        var result = await service.DecideAsync(DelegatedManager(), Guid.NewGuid(), "approve", null);

        Assert.Equal(SalesExceptionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task SalesManager_CannotApproveOrReject_ButCanCancelOwnRequest()
    {
        var (service, _) = CreateService();
        var manager = Manager();
        var id = await CreatePendingAsync(service, manager);

        var approve = await service.DecideAsync(manager, id, "approve", null);
        var cancel = await service.DecideAsync(manager, id, "cancel", "تم الحل محلياً");

        Assert.Equal(SalesExceptionOutcome.Forbidden, approve.Outcome);
        Assert.True(cancel.Ok);
        Assert.Equal(ExceptionStatuses.Cancelled, cancel.Request!.Status);
    }

    [Fact]
    public async Task SalesManager_CannotCancelAnotherManagersRequest()
    {
        var (service, _) = CreateService();
        var id = await CreatePendingAsync(service, Manager("mgr-najaf"));

        var result = await service.DecideAsync(Manager("mgr-basra", "basra-demo"), id, "cancel", null);

        Assert.Equal(SalesExceptionOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task Decide_RejectsCallersWithoutASalesRole()
    {
        var (service, _) = CreateService();
        var id = await CreatePendingAsync(service);

        var result = await service.DecideAsync(SalesEmployee(), id, "approve", null);

        Assert.Equal(SalesExceptionOutcome.Forbidden, result.Outcome);
    }

    [Fact]
    public async Task List_FiltersByStatusCityAndRequestingManager()
    {
        var (service, _) = CreateService();
        var najaf = await CreatePendingAsync(service, Manager("mgr-najaf", "najaf-demo"));
        await CreatePendingAsync(service, Manager("mgr-basra", "basra-demo"));
        await service.DecideAsync(DelegatedManager(), najaf, "approve", null);

        var pending = await service.ListAsync(new SalesExceptionQuery { Status = ExceptionStatuses.Pending });
        var byCity = await service.ListAsync(new SalesExceptionQuery { CityValue = "najaf-demo" });
        var byManager = await service.ListAsync(new SalesExceptionQuery { RequestingManagerUserName = "mgr-basra" });

        Assert.Equal("basra-demo", Assert.Single(pending.Items).CityValue);
        Assert.Equal(ExceptionStatuses.Approved, Assert.Single(byCity.Items).Status);
        Assert.Equal("mgr-basra", Assert.Single(byManager.Items).RequestingManagerUserName);
    }

    [Fact]
    public async Task List_PagesNewestFirst()
    {
        var (service, _) = CreateService();
        for (var i = 1; i <= 12; i++)
        {
            await service.CreateAsync(Manager(), Input($"سبب {i}"));
        }

        var page = await service.ListAsync(new SalesExceptionQuery { Page = 1, PageSize = 5 });

        Assert.Equal(12, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal("سبب 12", page.Items[0].Reason);
    }

    [Fact]
    public async Task DashboardCounts_GroupByStatusAndHonourCityFilter()
    {
        var (service, _) = CreateService();
        var approved = await CreatePendingAsync(service, Manager("mgr-najaf", "najaf-demo"));
        var rejected = await CreatePendingAsync(service, Manager("mgr-najaf", "najaf-demo"));
        await CreatePendingAsync(service, Manager("mgr-basra", "basra-demo"));
        await service.DecideAsync(DelegatedManager(), approved, "approve", null);
        await service.DecideAsync(DelegatedManager(), rejected, "reject", null);

        var all = await service.DashboardCountsAsync(null);
        var najafOnly = await service.DashboardCountsAsync("najaf-demo");

        Assert.Equal(1, all[ExceptionStatuses.Pending]);
        Assert.Equal(1, all[ExceptionStatuses.Approved]);
        Assert.Equal(1, all[ExceptionStatuses.Rejected]);
        Assert.False(najafOnly.ContainsKey(ExceptionStatuses.Pending));
        Assert.Equal(2, najafOnly.Values.Sum());
    }

    [Fact]
    public async Task GetById_ReturnsNullForUnknownId()
    {
        var (service, _) = CreateService();

        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EnrichedDetail_Missing_SalesRequestId_Returns_Clear_Error()
    {
        var (service, _) = CreateService();
        var id = await CreatePendingAsync(service);
        var (outcome, detail, error) = await service.GetEnrichedDetailAsync(id);
        Assert.Equal(SalesExceptionOutcome.Invalid, outcome);
        Assert.NotNull(detail);
        Assert.Contains("طلب البيع الأصلي", error);
        Assert.Null(detail!.Review);
    }

    [Fact]
    public async Task EnrichedDetail_With_Fake_Aggregator_Returns_Review()
    {
        var notes = new IntentRecordingBranchNotePoster();
        var store = new InMemorySalesExceptionStore();
        var aggregator = new FakeReviewAggregator
        {
            Body = new
            {
                customerClassification = new { type = "New", labelArabic = "زبون جديد", matchCount = 0 },
                matchingCustomers = Array.Empty<object>(),
                source = new { displayLabel = "مندوب", personName = "أحمد" }
            }
        };
        var service = new SalesExceptionService(store, notes, aggregator: aggregator);
        var created = await service.CreateAsync(Manager(), Input(salesRequestId: 42));
        Assert.True(created.Ok);

        var (outcome, detail, error) = await service.GetEnrichedDetailAsync(created.Request!.Id);
        Assert.Equal(SalesExceptionOutcome.Success, outcome);
        Assert.Null(error);
        Assert.NotNull(detail!.Review);
        Assert.Equal(1, aggregator.Calls);
        Assert.Contains("exception-review-context", aggregator.LastPath);
        Assert.Equal("najaf-demo", aggregator.LastCity);
    }

    [Fact]
    public async Task EnrichedDetail_Branch_404_Is_Business_Error()
    {
        var notes = new IntentRecordingBranchNotePoster();
        var aggregator = new FakeReviewAggregator { Status = 404 };
        var service = new SalesExceptionService(new InMemorySalesExceptionStore(), notes, aggregator: aggregator);
        var created = await service.CreateAsync(Manager(), Input(salesRequestId: 99));
        var (outcome, _, error) = await service.GetEnrichedDetailAsync(created.Request!.Id);
        Assert.Equal(SalesExceptionOutcome.Invalid, outcome);
        Assert.Contains("طلب البيع الأصلي", error);
    }

    private sealed class FakeReviewAggregator : ISalesManagerBranchAggregator
    {
        public int Status { get; set; } = 200;
        public object? Body { get; set; }
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastCity { get; private set; }

        public Task<IReadOnlyList<AdminCity>> BranchesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AdminCity>>([]);

        public Task<(int Status, object? Body)> GetOneAsync(
            GatewayUser user, string cityValue, string companyPath, CancellationToken ct)
        {
            Calls++;
            LastPath = companyPath;
            LastCity = cityValue;
            return Task.FromResult((Status, Body));
        }

        public Task<(int Status, object? Body)> GetAsync(GatewayUser user, string? cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> GetExactBranchArrayAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> SumCountAsync(GatewayUser user, string? cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> PostFanoutAsync(GatewayUser user, string? cityValue, string companyPath, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> GetFileAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> PostAsync(GatewayUser user, string cityValue, string companyPath, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> SendContentAsync(GatewayUser user, string cityValue, string companyPath, HttpMethod method, HttpContent? content, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> SearchCustomersAsync(GatewayUser user, string? query, string? cityValue, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> ExcelSearchAsync(GatewayUser user, string? cityValue, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> DashboardAsync(GatewayUser user, string? cityValue, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> EvaluateAcrossBranchesAsync(GatewayUser user, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> EvaluationHitsAcrossBranchesAsync(GatewayUser user, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> TransferProvinceAsync(GatewayUser user, string fromCityValue, int requestId, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
    }
}
