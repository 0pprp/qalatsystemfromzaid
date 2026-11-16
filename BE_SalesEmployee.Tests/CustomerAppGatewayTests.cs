using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using BE_SalesEmployee.Controllers;
using BE_SalesEmployee.Sales.CustomerApp;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Customer store app (BE_CustomerApp) through the gateway: Qalaa-only branch scope, accounts fan-out
/// with partial results, order routing by cityValue, and status grouping by branch.
/// </summary>
public sealed class CustomerAppBranchScopeTests
{
    private static List<AdminCity> Catalog() =>
    [
        new() { Value = "1", Name = "النجف", Database = "DatabaseCompanyNajaf", Link = "http://najaf/api/" },
        new() { Value = "2", Name = "بغداد - الرصافة", Database = "DatabaseCompanyBaghdadRusafa", Link = "http://rusafa/api/" },
        new() { Value = "7", Name = "الكوت", Database = "DatabaseCompanyKot", Link = "http://kot/api/" },
        new() { Value = "8", Name = "الموصل", Database = "DatabaseCompanyMosul", Link = "http://mosul/api/" },
        new() { Value = "15", Name = "الرصافة - عقيل", Database = "DatabaseCompanyRusafaAqeel", Link = "http://aqeel-r/api/" },
        new() { Value = "16", Name = "ميسان", Database = "DatabaseCompanyMaysan", Link = "http://maysan/api/" },
        new() { Value = "18", Name = "الكرخ", Database = "DatabaseCompanyKarakAqeel", Link = "http://karkh/api/" },
        new() { Value = "19", Name = "فرع تجريبي", Database = "X", Link = "http://test/api/" },
        new() { Value = "najaf-demo", Name = "النجف - DEMO", Database = "DatabaseCompanyNajaf_DEMO", Link = "http://demo/api/" },
        new() { Value = "20", Name = "بلا رابط", Database = "Y", Link = "" },
    ];

    [Fact]
    public void Only_qalaa_branches_with_numeric_city_values_are_in_scope()
    {
        var scoped = CustomerAppBranchScope.Filter(Catalog());

        Assert.Equal([1, 2, 8], scoped.Select(b => b.CityValue).ToArray());
        Assert.Equal("النجف", scoped[0].Name);
        Assert.Equal("http://najaf/api/", scoped[0].City.Link);
    }

    [Theory]
    [InlineData("الكوت", "DatabaseCompanyKot")]
    [InlineData("ميسان", "DatabaseCompanyMaysan")]
    [InlineData("الكرخ - عقيل", "DatabaseCompanyKarakAqeel")]
    [InlineData("الكرخ", "DatabaseCompanyKarakAqeel")]
    public void Aqeel_kut_and_maysan_are_excluded(string name, string database)
    {
        Assert.False(CustomerAppBranchScope.IsInScope(new AdminCity
        {
            Value = "9", Name = name, Database = database, Link = "http://x/api/"
        }));
    }

    [Fact]
    public void Duplicate_city_values_keep_the_first_branch()
    {
        var scoped = CustomerAppBranchScope.Filter(
        [
            new() { Value = "1", Name = "النجف", Link = "http://a/api/" },
            new() { Value = "1", Name = "النجف 2", Link = "http://b/api/" },
        ]);

        Assert.Equal("النجف", Assert.Single(scoped).Name);
    }

    [Theory]
    [InlineData("07801234567", "7801234567")]
    [InlineData("+964 780 123 4567", "7801234567")]
    [InlineData("009647801234567", "7801234567")]
    [InlineData("7801234567", "7801234567")]
    [InlineData("0780-123-456", "")]
    [InlineData("06801234567", "")]
    [InlineData(null, "")]
    public void Phone_match_key_mirrors_branch_normalizer(string? input, string expected)
    {
        Assert.Equal(expected, CustomerAppPhone.ToMatchKey(input));
    }
}

public sealed class CustomerAppStatusGroupingTests
{
    private static readonly List<CustomerAppBranch> Branches =
    [
        new() { CityValue = 1, Name = "النجف", City = new AdminCity { Value = "1", Name = "النجف", Link = "http://najaf/api/" } },
        new() { CityValue = 2, Name = "كربلاء", City = new AdminCity { Value = "2", Name = "كربلاء", Link = "http://karbala/api/" } },
    ];

    [Fact]
    public void Items_are_grouped_per_branch_and_out_of_scope_cities_are_separated()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), d = Guid.NewGuid();

        var groups = CustomerAppStatusGrouping.Group(
        [
            new() { CityValue = 1, OrderUuid = a },
            new() { CityValue = 2, OrderUuid = b },
            new() { CityValue = 1, OrderUuid = c },
            new() { CityValue = 7, OrderUuid = d },
            new() { CityValue = 1, OrderUuid = a },          // duplicate
            new() { CityValue = 2, OrderUuid = Guid.Empty }, // ignored
        ], Branches);

        Assert.Equal(2, groups.ByBranch.Count);
        Assert.Equal([a, c], groups.ByBranch[1].OrderUuids);
        Assert.Equal([b], groups.ByBranch[2].OrderUuids);
        var outOfScope = Assert.Single(groups.OutOfScope);
        Assert.Equal(d, outOfScope.OrderUuid);
    }

    [Fact]
    public void Branch_statuses_are_stamped_with_city_and_missing_uuids_become_not_found()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();

        var merged = CustomerAppGatewayService.MergeBranchStatuses(Branches[0], [a, b],
        [
            new CustomerAppStatusItem { OrderUuid = a, Found = true, SalesRequestId = 5, Status = "Completed" }
        ]);

        Assert.Collection(merged,
            i => { Assert.Equal(a, i.OrderUuid); Assert.True(i.Found); Assert.Equal(1, i.CityValue); Assert.Equal("Completed", i.Status); },
            i => { Assert.Equal(b, i.OrderUuid); Assert.False(i.Found); Assert.Equal(1, i.CityValue); });
    }
}

public sealed class CustomerAppGatewayServiceTests
{
    private const string Key = "test-key";

    /// <summary>Routes requests by host; records every request (path + body).</summary>
    private sealed class FakeBranches : HttpMessageHandler
    {
        public ConcurrentDictionary<string, Func<HttpRequestMessage, string?, CancellationToken, Task<HttpResponseMessage>>> Hosts { get; } = new();
        public ConcurrentBag<(string Host, string PathAndQuery, string? Body, string? Key)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            var host = request.RequestUri!.Host;
            request.Headers.TryGetValues("X-Sales-Gateway-Key", out var keys);
            Calls.Add((host, request.RequestUri.PathAndQuery, body, keys?.FirstOrDefault()));
            if (!Hosts.TryGetValue(host, out var handler))
            {
                throw new HttpRequestException("connection refused");
            }

            return await handler(request, body, ct);
        }
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private static (CustomerAppGatewayService Service, FakeBranches Branches, IConfiguration Config) Create()
    {
        var lab = new (string Value, string Name, string Db, string Host)[]
        {
            ("1", "النجف", "DatabaseCompanyNajaf", "najaf"),
            ("2", "كربلاء", "DatabaseCompanyKarbala", "karbala"),
            ("5", "البصرة", "DatabaseCompanyBasra", "basra"),
            ("15", "الرصافة - عقيل", "DatabaseCompanyRusafaAqeel", "aqeel"),
        };
        var settings = new Dictionary<string, string?>
        {
            ["InternalApiKey"] = Key,
            ["SalesManagement:RequireDemoDatabase"] = "false",
        };
        for (var i = 0; i < lab.Length; i++)
        {
            settings[$"LabCities:{i}:Value"] = lab[i].Value;
            settings[$"LabCities:{i}:Name"] = lab[i].Name;
            settings[$"LabCities:{i}:Database"] = lab[i].Db;
            settings[$"LabCities:{i}:Link"] = $"http://{lab[i].Host}/api/";
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var handler = new FakeBranches();
        var cities = new AdminCitiesService(
            new HttpClient(), config, NullLogger<AdminCitiesService>.Instance, new MemoryCache(new MemoryCacheOptions()));
        var proxy = new BranchProxyService(new HttpClient(handler), config);
        var service = new CustomerAppGatewayService(cities, proxy, NullLogger<CustomerAppGatewayService>.Instance)
        {
            PerBranchTimeout = TimeSpan.FromMilliseconds(300)
        };
        return (service, handler, config);
    }

    private static object Account(int customerId, string saleDate) => new
    {
        cityValue = "branch-config-id",
        cityName = "اسم داخلي",
        customerId,
        customerName = "علي حسين",
        saleDate,
        items = new[] { new { name = "ثلاجة", quantity = 1 } },
        totalAmount = 780000,
        dailyInstallment = 6500,
        paidAmount = 455000,
        remainingAmount = 325000,
        paymentsCount = 70,
        lastPaymentDate = "2026-11-01T00:00:00"
    };

    [Fact]
    public async Task Branches_lists_only_in_scope_branches()
    {
        var (svc, _, _) = Create();

        var branches = await svc.GetBranchesAsync(CancellationToken.None);

        Assert.Equal([1, 2, 5], branches.Select(b => b.CityValue).ToArray());
    }

    [Fact]
    public async Task Accounts_fan_out_returns_partial_when_a_branch_is_slow_or_down()
    {
        var (svc, branches, _) = Create();
        branches.Hosts["najaf"] = (_, _, _) => Task.FromResult(Json(new { accounts = new[] { Account(5531, "2026-03-01T00:00:00") } }));
        branches.Hosts["karbala"] = async (_, _, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct); // longer than PerBranchTimeout
            return Json(new { accounts = Array.Empty<object>() });
        };
        // basra: not registered → connection refused

        var result = await svc.GetAccountsByPhoneAsync("+964 780 123 4567", CancellationToken.None);

        Assert.Equal(200, result.Status);
        var body = Assert.IsType<CustomerAppAccountsResult>(result.Body);
        Assert.True(body.Partial);
        Assert.Equal(["كربلاء", "البصرة"], body.UnavailableBranches);
        var account = Assert.Single(body.Accounts);
        Assert.Equal(1, account.CityValue);
        Assert.Equal("النجف", account.CityName);
        Assert.Equal(325000, account.RemainingAmount);
        Assert.DoesNotContain(branches.Calls, c => c.Host == "aqeel");
        var najafCall = branches.Calls.Single(c => c.Host == "najaf");
        Assert.Equal("/api/internal/customer-app/accounts-by-phone?phone=07801234567", najafCall.PathAndQuery);
        Assert.Equal(Key, najafCall.Key);
    }

    [Fact]
    public async Task Accounts_with_invalid_phone_is_400_without_calling_branches()
    {
        var (svc, branches, _) = Create();

        var result = await svc.GetAccountsByPhoneAsync("123", CancellationToken.None);

        Assert.Equal(400, result.Status);
        Assert.Empty(branches.Calls);
    }

    [Fact]
    public async Task Detail_passes_branch_404_through_and_rejects_out_of_scope_city()
    {
        var (svc, branches, _) = Create();
        branches.Hosts["najaf"] = (_, _, _) => Task.FromResult(Json(new { message = "x" }, HttpStatusCode.NotFound));

        Assert.Equal(404, (await svc.GetAccountDetailAsync(1, 5531, "07801234567", CancellationToken.None)).Status);
        Assert.Equal(404, (await svc.GetAccountDetailAsync(15, 5531, "07801234567", CancellationToken.None)).Status);
        Assert.DoesNotContain(branches.Calls, c => c.Host == "aqeel");
    }

    [Fact]
    public async Task Detail_is_stamped_with_gateway_city()
    {
        var (svc, branches, _) = Create();
        branches.Hosts["karbala"] = (_, _, _) => Task.FromResult(Json(new
        {
            account = Account(77, "2026-03-01T00:00:00"),
            sales = new[] { new { saleId = 991, date = "2026-03-01T00:00:00", items = new[] { new { name = "ثلاجة", quantity = 1 } }, totalAmount = 780000, dailyInstallment = 6500 } },
            payments = new[] { new { paymentId = 1, date = "2026-11-01T00:00:00", amount = 6500 } }
        }));

        var result = await svc.GetAccountDetailAsync(2, 77, "07801234567", CancellationToken.None);

        Assert.Equal(200, result.Status);
        var detail = Assert.IsType<CustomerAppAccountDetail>(result.Body);
        Assert.Equal(2, detail.Account.CityValue);
        Assert.Equal("كربلاء", detail.Account.CityName);
        Assert.Equal(991, Assert.Single(detail.Sales).SaleId);
        Assert.Equal(6500, Assert.Single(detail.Payments).Amount);
        Assert.Contains(branches.Calls, c => c.PathAndQuery == "/api/internal/customer-app/accounts/77?phone=07801234567");
    }

    [Fact]
    public async Task Sale_progress_is_proxied_to_the_city_branch_and_stamped()
    {
        var (svc, branches, _) = Create();
        branches.Hosts["karbala"] = (_, _, _) => Task.FromResult(Json(new
        {
            saleId = 5012, customerId = 77, totalAmount = 780000, paidAmount = 273000, remainingAmount = 507000,
            paymentsCount = 42, lastPaymentDate = "2026-11-02T00:00:00"
        }));

        var result = await svc.GetSaleProgressAsync(2, 5012, CancellationToken.None);

        Assert.Equal(200, result.Status);
        var progress = Assert.IsType<CustomerAppSaleProgress>(result.Body);
        Assert.Equal(2, progress.CityValue);
        Assert.Equal(507000, progress.RemainingAmount);
        Assert.Contains(branches.Calls, c => c.PathAndQuery == "/api/internal/customer-app/sales/5012/progress");
    }

    [Fact]
    public async Task Sale_progress_passes_404_through_and_rejects_out_of_scope_city()
    {
        var (svc, branches, _) = Create();
        branches.Hosts["najaf"] = (_, _, _) => Task.FromResult(Json(new { message = "x" }, HttpStatusCode.NotFound));

        Assert.Equal(404, (await svc.GetSaleProgressAsync(1, 5012, CancellationToken.None)).Status);
        Assert.Equal(404, (await svc.GetSaleProgressAsync(15, 5012, CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Create_routes_by_city_value_to_crm_intake_with_customer_app_source()
    {
        var (svc, branches, _) = Create();
        branches.Hosts["basra"] = (_, _, _) => Task.FromResult(Json(new
        {
            salesRequestId = 42, status = "Assigned", idempotentReplay = false, cityValue = "basra", cityName = "البصرة"
        }));
        var uuid = Guid.NewGuid();

        var result = await svc.CreateSalesRequestAsync(new CustomerAppSalesRequestBody
        {
            OrderUuid = uuid,
            CityValue = 5,
            CustomerName = "علي حسين محمد",
            CustomerPhone = "07801234567",
            CustomerProvince = "البصرة",
            CustomerAddress = "حي الجزائر",
            Notes = "كاش",
            ProductName = "ثلاجة"
        }, CancellationToken.None);

        Assert.Equal(200, result.Status);
        var created = Assert.IsType<CustomerAppSalesRequestCreated>(result.Body);
        Assert.Equal(5, created.CityValue);
        Assert.Equal("البصرة", created.CityName);
        Assert.Equal(42, created.SalesRequestId);
        Assert.Equal("Assigned", created.Status);
        Assert.False(created.IdempotentReplay);

        var call = Assert.Single(branches.Calls);
        Assert.Equal("basra", call.Host);
        Assert.Equal("/api/internal/crm/sales-requests", call.PathAndQuery);
        using var sent = JsonDocument.Parse(call.Body!);
        Assert.Equal(uuid, sent.RootElement.GetProperty("crmOrderUuid").GetGuid());
        Assert.Equal("CustomerApp", sent.RootElement.GetProperty("source").GetString());
        Assert.Equal("ثلاجة", sent.RootElement.GetProperty("productName").GetString());
    }

    [Theory]
    [InlineData(15)] // عقيل
    [InlineData(7)]  // not in catalog
    [InlineData(0)]
    public async Task Create_with_out_of_scope_city_is_422(int cityValue)
    {
        var (svc, branches, _) = Create();

        var result = await svc.CreateSalesRequestAsync(new CustomerAppSalesRequestBody
        {
            OrderUuid = Guid.NewGuid(), CityValue = cityValue, CustomerName = "علي"
        }, CancellationToken.None);

        Assert.Equal(422, result.Status);
        Assert.Empty(branches.Calls);
    }

    [Fact]
    public async Task Create_passes_branch_validation_errors_through()
    {
        var (svc, branches, _) = Create();
        branches.Hosts["najaf"] = (_, _, _) => Task.FromResult(Json(new { message = "رقم الهاتف غير صالح" }, HttpStatusCode.BadRequest));

        var result = await svc.CreateSalesRequestAsync(new CustomerAppSalesRequestBody
        {
            OrderUuid = Guid.NewGuid(), CityValue = 1, CustomerName = "علي", CustomerPhone = "123"
        }, CancellationToken.None);

        Assert.Equal(400, result.Status);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result.Body));
        Assert.Equal("رقم الهاتف غير صالح", doc.RootElement.GetProperty("message").GetString());
        Assert.Equal("رقم الهاتف غير صالح", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Status_groups_by_city_and_omits_unavailable_branches()
    {
        var (svc, branches, _) = Create();
        Guid n1 = Guid.NewGuid(), n2 = Guid.NewGuid(), k1 = Guid.NewGuid(), a1 = Guid.NewGuid();
        branches.Hosts["najaf"] = (_, body, _) =>
        {
            using var doc = JsonDocument.Parse(body!);
            var asked = doc.RootElement.GetProperty("orderUuids").EnumerateArray().Select(e => e.GetGuid()).ToList();
            Assert.Equal([n1, n2], asked);
            return Task.FromResult(Json(new
            {
                items = new object[]
                {
                    new { orderUuid = n1, found = true, salesRequestId = 9, status = "Completed", isDeleted = false, convertedToSaleId = 991, updatedAtUtc = "2026-11-02T10:00:00Z" },
                    new { orderUuid = n2, found = false }
                }
            }));
        };
        branches.Hosts["karbala"] = (_, _, _) => Task.FromResult(Json(new { message = "boom" }, HttpStatusCode.InternalServerError));

        var result = await svc.GetStatusesAsync(new CustomerAppStatusBody
        {
            Items =
            [
                new() { CityValue = 1, OrderUuid = n1 },
                new() { CityValue = 2, OrderUuid = k1 },
                new() { CityValue = 15, OrderUuid = a1 },
                new() { CityValue = 1, OrderUuid = n2 },
            ]
        }, CancellationToken.None);

        Assert.Equal(200, result.Status);
        var body = Assert.IsType<CustomerAppStatusResult>(result.Body);
        Assert.Collection(body.Items,
            i => { Assert.Equal(n1, i.OrderUuid); Assert.Equal(1, i.CityValue); Assert.True(i.Found); Assert.Equal("Completed", i.Status); Assert.Equal(991, i.ConvertedToSaleId); },
            i => { Assert.Equal(a1, i.OrderUuid); Assert.Equal(15, i.CityValue); Assert.False(i.Found); },
            i => { Assert.Equal(n2, i.OrderUuid); Assert.False(i.Found); });
        Assert.Equal(["كربلاء"], body.UnavailableBranches);
        Assert.Equal(2, branches.Calls.Count); // one call per branch, aqeel never called
    }

    [Fact]
    public async Task Status_over_200_items_is_400()
    {
        var (svc, _, _) = Create();

        var result = await svc.GetStatusesAsync(new CustomerAppStatusBody
        {
            Items = Enumerable.Range(0, 201).Select(_ => new CustomerAppStatusQueryItem { CityValue = 1, OrderUuid = Guid.NewGuid() }).ToList()
        }, CancellationToken.None);

        Assert.Equal(400, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong")]
    public async Task Controller_rejects_missing_or_wrong_gateway_key(string? key)
    {
        var (svc, branches, config) = Create();
        var controller = new InternalCustomerAppController(svc, config)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (key != null)
        {
            controller.Request.Headers[InternalCrmSalesRequestsController.GatewayKeyHeader] = key;
        }

        Assert.IsType<UnauthorizedObjectResult>(await controller.Branches(CancellationToken.None));
        Assert.IsType<UnauthorizedObjectResult>(await controller.AccountsByPhone("07801234567", CancellationToken.None));
        Assert.Empty(branches.Calls);
    }

    [Fact]
    public async Task Controller_branches_shape_is_city_value_and_name()
    {
        var (svc, _, config) = Create();
        var controller = new InternalCustomerAppController(svc, config)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers[InternalCrmSalesRequestsController.GatewayKeyHeader] = Key;

        var ok = Assert.IsType<OkObjectResult>(await controller.Branches(CancellationToken.None));
        var json = JsonSerializer.Serialize(ok.Value);

        Assert.Contains("\"cityValue\":1", json);
        Assert.Contains("\"name\":", json);
        Assert.DoesNotContain("Link", json);
    }
}
