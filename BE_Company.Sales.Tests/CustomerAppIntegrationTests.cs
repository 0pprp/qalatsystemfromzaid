using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// Customer store app (BE_CustomerApp) on the branch: phone matching, status polling,
/// and the Source="CustomerApp" path through the existing CRM intake.
/// </summary>
public class CustomerAppPhoneMatchTests
{
    [Theory]
    [InlineData("07801234567", "7801234567")]
    [InlineData("7801234567", "7801234567")]
    [InlineData("+964 780 123 4567", "7801234567")]
    [InlineData("00964-780-123-4567", "7801234567")]
    [InlineData("0780 123 4567", "7801234567")]
    [InlineData("06801234567", "")]
    [InlineData("0780123456", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Match_key_is_ten_digits_starting_with_7(string? input, string expected)
    {
        Assert.Equal(expected, CustomerAppPhoneMatch.ToMatchKey(input));
    }

    [Theory]
    [InlineData("07801234567")]
    [InlineData("7801234567")]
    [InlineData("0780 123 4567")]
    [InlineData("0780-123-4567")]
    [InlineData("+964 780 123 4567")]
    [InlineData("+964-780-123-4567")]
    [InlineData("009647801234567")]
    [InlineData("(0780) 123.4567")]
    [InlineData("0780 1234567")]
    [InlineData(" 07801234567\r\n")]
    public void Legacy_stored_formats_match_the_app_phone(string stored)
    {
        Assert.True(CustomerAppPhoneMatch.StoredMatches(stored, "7801234567"));
    }

    [Theory]
    [InlineData("07801234568")]
    [InlineData("07701234567")]
    [InlineData("780123456")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("07801234567 / 07709999999")] // two numbers: only the last 10 digits count
    public void Other_numbers_do_not_match(string? stored)
    {
        Assert.False(CustomerAppPhoneMatch.StoredMatches(stored, "7801234567"));
    }

    [Fact]
    public void Invalid_key_never_matches()
    {
        Assert.False(CustomerAppPhoneMatch.StoredMatches("07801234567", ""));
        Assert.False(CustomerAppPhoneMatch.StoredMatches("07801234567", "07801234567"));
    }

    [Fact]
    public void Sql_strips_exactly_the_characters_the_csharp_mirror_strips()
    {
        var sql = CustomerAppPhoneMatch.StrippedSql("C.PhoneNumber");

        Assert.StartsWith("REPLACE(", sql);
        Assert.Contains("ISNULL(C.PhoneNumber, N'')", sql);
        var replaceCount = sql.Split("REPLACE(").Length - 1;
        Assert.Equal(CustomerAppPhoneMatch.StrippedCharacters.Count, replaceCount);
        Assert.Contains("N'-'", sql);
        Assert.Contains("N'+'", sql);
        Assert.Contains("NCHAR(160)", sql);
    }

    [Fact]
    public void Predicate_compares_last_ten_characters_with_the_parameter()
    {
        var sql = CustomerAppPhoneMatch.MatchPredicateSql("C.PhoneNumber", "@Phone");

        Assert.StartsWith("RIGHT(REPLACE(", sql);
        Assert.EndsWith(", 10) = @Phone", sql);
    }
}

public class CustomerAppServiceTests
{
    private sealed class FakeCustomerAppRepository : ICustomerAppRepository
    {
        public List<CustomerAppAccountRow> Accounts { get; } = [];
        public Dictionary<string, List<int>> PhoneOwners { get; } = [];
        public List<CustomerAppItemRow> Items { get; } = [];
        public List<CustomerAppSaleRow> Sales { get; } = [];
        public List<CustomerAppPaymentDTO> Payments { get; } = [];
        public List<CustomerAppStatusRow> Statuses { get; } = [];
        public string? LastKey { get; private set; }

        public Task<IReadOnlyList<CustomerAppAccountRow>> ListAccountsByPhoneAsync(string matchKey, CancellationToken ct)
        {
            LastKey = matchKey;
            var owners = PhoneOwners.GetValueOrDefault(matchKey) ?? [];
            return Task.FromResult<IReadOnlyList<CustomerAppAccountRow>>(
                Accounts.Where(a => owners.Contains(a.CustomerID)).ToList());
        }

        public Task<CustomerAppAccountRow?> GetAccountByIdAndPhoneAsync(int customerId, string matchKey, CancellationToken ct)
        {
            var owners = PhoneOwners.GetValueOrDefault(matchKey) ?? [];
            return Task.FromResult(owners.Contains(customerId)
                ? Accounts.FirstOrDefault(a => a.CustomerID == customerId)
                : null);
        }

        public Task<IReadOnlyList<CustomerAppItemRow>> ListItemsAsync(IReadOnlyCollection<int> customerIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CustomerAppItemRow>>(Items.Where(i => customerIds.Contains(i.CustomerID)).ToList());

        public Task<IReadOnlyList<CustomerAppSaleRow>> ListSalesAsync(int customerId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CustomerAppSaleRow>>(Sales);

        public Task<IReadOnlyList<CustomerAppPaymentDTO>> ListPaymentsAsync(int customerId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CustomerAppPaymentDTO>>(Payments);

        public Task<IReadOnlyList<CustomerAppStatusRow>> ListStatusesAsync(IReadOnlyCollection<Guid> orderUuids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CustomerAppStatusRow>>(Statuses.Where(s => orderUuids.Contains(s.ExternalCrmOrderUuid)).ToList());

        /// <summary>SalesDrafts.SaleId → posted CustomerID.</summary>
        public Dictionary<int, int> PostedDrafts { get; } = [];

        public Task<CustomerAppAccountRow?> GetAccountBySaleDraftAsync(int saleId, CancellationToken ct) =>
            Task.FromResult(PostedDrafts.TryGetValue(saleId, out var customerId)
                ? Accounts.FirstOrDefault(a => a.CustomerID == customerId)
                : null);
    }

    [Fact]
    public async Task Sale_progress_reads_the_account_posted_from_the_draft()
    {
        var (service, repo) = Create();
        repo.Accounts.Add(new CustomerAppAccountRow
        {
            CustomerID = 77, TotalAmount = 780000, PaidAmount = 273000, RemainingAmount = 507000, PaymentsCount = 42,
            LastPaymentDate = new DateTime(2026, 11, 2)
        });
        repo.PostedDrafts[5012] = 77;

        var progress = await service.GetSaleProgressAsync(5012, CancellationToken.None);
        Assert.NotNull(progress);
        Assert.Equal(77, progress!.CustomerId);
        Assert.Equal(507000, progress.RemainingAmount);
        Assert.Equal(42, progress.PaymentsCount);

        Assert.Null(await service.GetSaleProgressAsync(9999, CancellationToken.None)); // not posted yet
        Assert.Null(await service.GetSaleProgressAsync(0, CancellationToken.None));
    }

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SalesManagement:BranchId"] = "najaf",
            ["SalesManagement:BranchName"] = "النجف"
        })
        .Build();

    private static (CustomerAppService Service, FakeCustomerAppRepository Repo) Create()
    {
        var repo = new FakeCustomerAppRepository();
        return (new CustomerAppService(repo, new FakeRequestRepository(), Config()), repo);
    }

    private static CustomerAppAccountRow Row(int id, long total = 780000, long paid = 455000) => new()
    {
        CustomerID = id,
        CustomerName = " علي حسين ",
        SaleDate = new DateTime(2026, 3, 1),
        TotalAmount = total,
        DailyInstallment = 6500,
        PaidAmount = paid,
        RemainingAmount = total - paid,
        PaymentsCount = 70,
        LastPaymentDate = new DateTime(2026, 11, 1)
    };

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("06801234567")]
    public async Task Invalid_phone_is_400(string phone)
    {
        var (svc, _) = Create();

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.GetAccountsByPhoneAsync(phone, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Accounts_are_one_per_customer_with_items_and_branch_city()
    {
        var (svc, repo) = Create();
        repo.Accounts.AddRange([Row(5531), Row(5531), Row(77, 300000, 300000)]);
        repo.PhoneOwners["7801234567"] = [5531, 77];
        repo.Items.AddRange(
        [
            new() { CustomerID = 5531, CustomerSaleID = 1, ItemName = "ثلاجة", Quantity = 1 },
            new() { CustomerID = 5531, CustomerSaleID = 2, ItemName = "ثلاجة", Quantity = 1 },
            new() { CustomerID = 5531, CustomerSaleID = 2, ItemName = "مبردة", Quantity = 2 },
            new() { CustomerID = 77, CustomerSaleID = 3, ItemName = " ", Quantity = 5 },
        ]);

        var result = await svc.GetAccountsByPhoneAsync("+964 780 123 4567", CancellationToken.None);

        Assert.Equal("7801234567", repo.LastKey);
        Assert.Equal("najaf", result.CityValue);
        Assert.Equal(2, result.Accounts.Count);
        var first = result.Accounts.Single(a => a.CustomerId == 5531);
        Assert.Equal("علي حسين", first.CustomerName);
        Assert.Equal("النجف", first.CityName);
        Assert.Equal(325000, first.RemainingAmount);
        Assert.Collection(first.Items,
            i => { Assert.Equal("ثلاجة", i.Name); Assert.Equal(2, i.Quantity); },
            i => { Assert.Equal("مبردة", i.Name); Assert.Equal(2, i.Quantity); });
        Assert.Empty(result.Accounts.Single(a => a.CustomerId == 77).Items);
    }

    [Fact]
    public async Task Detail_is_null_when_phone_does_not_own_the_customer()
    {
        var (svc, repo) = Create();
        repo.Accounts.Add(Row(5531));
        repo.PhoneOwners["7801234567"] = [5531];

        Assert.Null(await svc.GetAccountDetailAsync(5531, "07709999999", CancellationToken.None));
        Assert.Null(await svc.GetAccountDetailAsync(0, "07801234567", CancellationToken.None));
    }

    [Fact]
    public async Task Detail_returns_sales_with_their_own_items_and_payments()
    {
        var (svc, repo) = Create();
        repo.Accounts.Add(Row(5531));
        repo.PhoneOwners["7801234567"] = [5531];
        repo.Items.AddRange(
        [
            new() { CustomerID = 5531, CustomerSaleID = 991, ItemName = "ثلاجة", Quantity = 1 },
            new() { CustomerID = 5531, CustomerSaleID = 992, ItemName = "غسالة", Quantity = 1 },
        ]);
        repo.Sales.Add(new CustomerAppSaleRow { CustomerSaleID = 991, DateCreate = new DateTime(2026, 3, 1), TotalAmount = 780000, DailyInstallment = 6500 });
        repo.Payments.Add(new CustomerAppPaymentDTO { PaymentId = 1, Date = new DateTime(2026, 11, 1), Amount = 6500 });

        var detail = await svc.GetAccountDetailAsync(5531, "07801234567", CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(5531, detail!.Account.CustomerId);
        var sale = Assert.Single(detail.Sales);
        Assert.Equal(991, sale.SaleId);
        Assert.Equal("ثلاجة", Assert.Single(sale.Items).Name);
        Assert.Equal(6500, Assert.Single(detail.Payments).Amount);
    }

    [Fact]
    public async Task Status_over_200_uuids_is_400()
    {
        var (svc, _) = Create();
        var body = new CustomerAppStatusRequestDTO { OrderUuids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList() };

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.GetStatusesAsync(body, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Status_returns_one_item_per_uuid_in_request_order()
    {
        var (svc, repo) = Create();
        var known = Guid.NewGuid();
        var deleted = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        var created = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        repo.Statuses.Add(new CustomerAppStatusRow
        {
            Id = 10, ExternalCrmOrderUuid = known, Status = "Completed", ConvertedToSaleId = 991,
            CreatedAtUtc = created, CompletedAtUtc = created.AddDays(2), LastHistoryAtUtc = created.AddDays(1)
        });
        repo.Statuses.Add(new CustomerAppStatusRow
        {
            Id = 11, ExternalCrmOrderUuid = deleted, Status = "New", IsDeleted = true, DeleteReason = " مكرر ",
            CreatedAtUtc = created, DeletedAtUtc = created.AddHours(3)
        });

        var result = await svc.GetStatusesAsync(
            new CustomerAppStatusRequestDTO { OrderUuids = [unknown, known, deleted, known, Guid.Empty] },
            CancellationToken.None);

        Assert.Collection(result.Items,
            i => { Assert.Equal(unknown, i.OrderUuid); Assert.False(i.Found); Assert.Null(i.Status); },
            i =>
            {
                Assert.Equal(known, i.OrderUuid);
                Assert.True(i.Found);
                Assert.Equal(10, i.SalesRequestId);
                Assert.Equal("Completed", i.Status);
                Assert.Equal(991, i.ConvertedToSaleId);
                Assert.Equal(created.AddDays(2), i.UpdatedAtUtc);
            },
            i =>
            {
                Assert.True(i.IsDeleted);
                Assert.Equal("مكرر", i.RejectionReason);
                Assert.Equal(created.AddHours(3), i.UpdatedAtUtc);
            });
    }

    [Fact]
    public void Rejection_reason_wins_over_delete_reason()
    {
        var uuid = Guid.NewGuid();
        var result = CustomerAppService.BuildStatusResponse([uuid],
        [
            new CustomerAppStatusRow { Id = 1, ExternalCrmOrderUuid = uuid, Status = "Rejected", RejectionReason = "لا يرغب", IsDeleted = true, DeleteReason = "x" }
        ]);

        Assert.Equal("لا يرغب", Assert.Single(result.Items).RejectionReason);
    }
}

public class CustomerAppIntakeSourceTests
{
    private sealed class RecordingNotifier : ICrmSalesBoardStatusNotifier
    {
        public List<CrmSalesBoardStatusNotification> Sent { get; } = [];

        public Task NotifyAsync(CrmSalesBoardStatusNotification notification, CancellationToken ct)
        {
            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class NoMatchEvaluation : ISalesRequestEvaluationService
    {
        public Task<SalesRequestEvaluationBatchResultDTO> EvaluateBatchAsync(
            SalesIdentity actor, SalesRequestEvaluationBatchRequestDTO body, CancellationToken ct) =>
            Task.FromResult(new SalesRequestEvaluationBatchResultDTO());

        public Task<SalesRequestEvaluationHitsPageDTO> ListHitsAsync(
            SalesIdentity actor, int requestId, string category, int page, int pageSize, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<SalesRequestEvaluationHitsPageDTO> ListHitsByPayloadAsync(
            SalesIdentity actor, SalesRequestEvaluationHitsRequestDTO body, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private static (CrmSalesRequestIntakeService Intake, FakeRequestRepository Repo, RecordingNotifier Notifier) Create()
    {
        var repo = new FakeRequestRepository();
        var notifier = new RecordingNotifier();
        var employees = new FakeManagerRead();
        var clock = new FakeClock();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesManagement:BranchId"] = "najaf",
                ["SalesManagement:BranchName"] = "النجف"
            })
            .Build();
        var requests = new SalesRequestService(repo, clock, employees, crmStatus: notifier);
        var intake = new CrmSalesRequestIntakeService(repo, requests, new NoMatchEvaluation(), employees, clock, config, notifier);
        return (intake, repo, notifier);
    }

    [Theory]
    [InlineData("CustomerApp")]
    [InlineData("customerapp")]
    [InlineData(" CustomerApp ")]
    public void CustomerApp_source_maps_to_its_own_profile(string source)
    {
        var profile = CrmIntakeSourceProfile.Resolve(source);

        Assert.Equal("CustomerApp", profile.CustomerSourceType);
        Assert.Equal("المروج", profile.CreatedByName);
        Assert.Equal("CustomerApp", profile.CreatedByUserType);
        Assert.False(profile.NotifyCrmBoard);
        Assert.False(profile.AutoAssign);
        Assert.Equal("أحمد علي حسن", profile.CreatedByNameFor(" أحمد علي حسن "));
        Assert.Equal("المروج", profile.CreatedByNameFor(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Crm")]
    [InlineData("something-else")]
    public void Missing_or_unknown_source_stays_crm(string? source)
    {
        var profile = CrmIntakeSourceProfile.Resolve(source);

        Assert.Equal(SalesRequestSources.Crm, profile.CustomerSourceType);
        Assert.Equal("قسم التسويق", profile.CreatedByName);
        Assert.True(profile.NotifyCrmBoard);
        Assert.False(profile.AutoAssign);
        Assert.Equal("قسم التسويق", profile.CreatedByNameFor("someone"));
    }

    [Fact]
    public void CustomerApp_is_not_treated_as_crm_by_the_board_status_forwarder_gate()
    {
        Assert.False(SalesRequestSources.IsCrm(SalesRequestSources.CustomerApp));
        Assert.True(SalesRequestSources.IsCustomerApp("CustomerApp"));
    }

    [Fact]
    public void Exception_review_source_label_is_customer_app_arabic()
    {
        var src = ExceptionReviewSourceMapper.FromRequest(new SalesRequestDTO
        {
            CustomerSourceType = SalesRequestSources.CustomerApp,
            CreatedByName = "أحمد علي حسن",
            CityName = "النجف"
        });

        Assert.Equal("المروج", src.DisplayLabel);
        Assert.Equal("أحمد علي حسن", src.PersonName);
    }

    [Fact]
    public async Task CustomerApp_intake_creates_row_with_customer_app_source_and_never_notifies_crm()
    {
        var (intake, repo, notifier) = Create();
        var uuid = Guid.NewGuid();

        var result = await intake.IntakeAsync(new CrmSalesRequestIntakeRequest
        {
            CrmOrderUuid = uuid,
            CustomerName = "علي حسين محمد",
            CustomerPhone = "07801234567",
            CustomerProvince = "النجف",
            ProductName = "ثلاجة",
            Source = "CustomerApp",
            SubmittedByName = "أحمد علي حسن"
        }, CancellationToken.None);

        Assert.False(result.IdempotentReplay);
        var row = Assert.Single(repo.Rows);
        Assert.Equal("CustomerApp", row.CustomerSourceType);
        Assert.Equal("أحمد علي حسن", row.CreatedByName);
        Assert.Equal("CustomerApp", row.CreatedByUserType);
        Assert.Equal(uuid, row.ExternalCrmOrderUuid);
        Assert.Contains("ثلاجة", row.Notes);
        // Stays in «غير المسندة» for the sales manager.
        Assert.False(result.AutoAssigned);
        Assert.Equal(0, row.TargetEmployeeId);
        Assert.Empty(notifier.Sent);

        var replay = await intake.IntakeAsync(new CrmSalesRequestIntakeRequest
        {
            CrmOrderUuid = uuid,
            CustomerName = "علي حسين محمد",
            Source = "CustomerApp"
        }, CancellationToken.None);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(result.SalesRequestId, replay.SalesRequestId);
        Assert.Single(repo.Rows);
    }

    [Fact]
    public async Task Crm_intake_without_source_is_unchanged_and_notifies_crm()
    {
        var (intake, repo, notifier) = Create();

        await intake.IntakeAsync(new CrmSalesRequestIntakeRequest
        {
            CrmOrderUuid = Guid.NewGuid(),
            CustomerName = "زبون CRM",
            ProductName = "مبردة"
        }, CancellationToken.None);

        var row = Assert.Single(repo.Rows);
        Assert.Equal(SalesRequestSources.Crm, row.CustomerSourceType);
        Assert.Equal("قسم التسويق", row.CreatedByName);
        Assert.Equal("منتج CRM: مبردة", row.Notes);
        // قسم التسويق requests land in «غير المسندة», not on a sales person.
        Assert.Equal(0, row.TargetEmployeeId);
        Assert.Contains(notifier.Sent, n => n.BoardStatus == "Processing");
    }
}
