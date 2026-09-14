using System.Net;
using System.Text;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class GlobalSalesManagerOrchestratorTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Body)> Calls { get; } = [];
        public Func<HttpRequestMessage, string?, HttpResponseMessage> Responder { get; set; } =
            (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"NotFound"}""", Encoding.UTF8, "application/json")
            };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method.Method, request.RequestUri!.ToString(), body));
            Assert.True(request.Headers.Contains("X-Sales-Gateway-Key"));
            return Responder(request, body);
        }
    }

    private static (GlobalSalesManagerOrchestrator Orch, RecordingHandler Handler) Build(
        bool requireDemo,
        RecordingHandler? handler = null)
    {
        handler ??= new RecordingHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["InternalApiKey"] = "test-key",
            ["SalesManagement:RequireDemoDatabase"] = requireDemo ? "true" : "false",
            ["SalesManagement:AllowedDemoDatabase"] = "DatabaseCompanyNajaf_DEMO",
            ["LabCities:0:Value"] = "1",
            ["LabCities:0:Name"] = "النجف",
            ["LabCities:0:Database"] = "DatabaseCompanyNajaf",
            ["LabCities:0:Link"] = "http://127.0.0.1:19001/api/",
            ["LabCities:1:Value"] = "3",
            ["LabCities:1:Name"] = "الكرخ",
            ["LabCities:1:Database"] = "DatabaseCompanyBaghdadKarak",
            ["LabCities:1:Link"] = "http://127.0.0.1:19003/api/",
            ["LabCities:2:Value"] = "19",
            ["LabCities:2:Name"] = "تجريبي",
            ["LabCities:2:Database"] = "DatabaseCompany",
            ["LabCities:2:Link"] = "http://testapp.example/api/",
        }).Build();

        var http = new HttpClient(handler);
        var cities = new AdminCitiesService(http, config, NullLogger<AdminCitiesService>.Instance);
        var proxy = new BranchProxyService(http, config);
        var orch = new GlobalSalesManagerOrchestrator(cities, proxy, NullLogger<GlobalSalesManagerOrchestrator>.Instance);
        return (orch, handler);
    }

    [Fact]
    public async Task Production_Catalog_Excludes_Demo_And_Test_Names()
    {
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("preflight", StringComparison.OrdinalIgnoreCase))
            {
                return Json(HttpStatusCode.OK, """{"status":"NotFound"}""");
            }

            return Json(HttpStatusCode.OK, """{"ok":true,"code":"CREATED"}""");
        };

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "sm-global",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Success, result.Status);
        Assert.DoesNotContain(handler.Calls, c => c.Url.Contains("testapp", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(4, handler.Calls.Count(c => c.Url.Contains("preflight", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, handler.Calls.Count(c => c.Method == "POST" && !c.Url.Contains("preflight", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, result.SucceededBranches.Count);
    }

    [Fact]
    public async Task Demo_Mode_Only_Uses_Demo_Database_Branch()
    {
        var handler = new RecordingHandler();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["InternalApiKey"] = "test-key",
            ["SalesManagement:RequireDemoDatabase"] = "true",
            ["SalesManagement:AllowedDemoDatabase"] = "DatabaseCompanyNajaf_DEMO",
            ["LabCities:0:Value"] = "najaf-demo",
            ["LabCities:0:Name"] = "النجف - DEMO",
            ["LabCities:0:Database"] = "DatabaseCompanyNajaf_DEMO",
            ["LabCities:0:Link"] = "http://127.0.0.1:19099/api/",
            ["LabCities:1:Value"] = "1",
            ["LabCities:1:Name"] = "النجف",
            ["LabCities:1:Database"] = "DatabaseCompanyNajaf",
            ["LabCities:1:Link"] = "http://127.0.0.1:19001/api/",
        }).Build();
        var http = new HttpClient(handler);
        handler.Responder = (req, _) =>
            req.RequestUri!.AbsolutePath.Contains("preflight")
                ? Json(HttpStatusCode.OK, """{"status":"NotFound"}""")
                : Json(HttpStatusCode.OK, """{"ok":true,"code":"CREATED"}""");
        var cities = new AdminCitiesService(http, config, NullLogger<AdminCitiesService>.Instance);
        var orch = new GlobalSalesManagerOrchestrator(
            cities, new BranchProxyService(http, config), NullLogger<GlobalSalesManagerOrchestrator>.Instance);

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "sm-demo",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Success, result.Status);
        Assert.All(handler.Calls, c => Assert.Contains("19099", c.Url));
        Assert.DoesNotContain(handler.Calls, c => c.Url.Contains("19001"));
    }

    [Fact]
    public async Task Username_Conflict_Stops_Before_Writes()
    {
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (_, _) => Json(HttpStatusCode.OK, """{"status":"UsernameConflict"}""");

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "taken",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Conflict, result.Status);
        Assert.DoesNotContain(handler.Calls, c =>
            c.Method == "POST" && !c.Url.Contains("preflight", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Partial_Failure_When_One_Branch_Write_Fails()
    {
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (req, _) =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("preflight"))
                return Json(HttpStatusCode.OK, """{"status":"NotFound"}""");
            if (req.RequestUri!.ToString().Contains("19003"))
                return Json(HttpStatusCode.ServiceUnavailable, """{"ok":false}""");
            return Json(HttpStatusCode.OK, """{"ok":true,"code":"CREATED"}""");
        };

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "sm-partial",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.PartialFailure, result.Status);
        Assert.Single(result.SucceededBranches);
        Assert.Single(result.FailedBranches);
        Assert.NotEqual(Guid.Empty, result.OperationId);
    }

    [Fact]
    public async Task Same_GlobalAccountId_Sent_To_All_Branches()
    {
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (req, _) =>
            req.RequestUri!.AbsolutePath.Contains("preflight")
                ? Json(HttpStatusCode.OK, """{"status":"NotFound"}""")
                : Json(HttpStatusCode.OK, """{"ok":true,"code":"CREATED"}""");

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "sm-gid",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Success, result.Status);
        var writes = handler.Calls.Where(c => c.Method == "POST" && c.Body != null
            && !c.Url.Contains("preflight")).ToList();
        Assert.Equal(2, writes.Count);
        Assert.All(writes, w => Assert.Contains(result.GlobalAccountId!.Value.ToString("D"), w.Body!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Legacy_Adoptable_All_Branches_Uses_One_Guid_No_Conflict()
    {
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (req, _) =>
            req.RequestUri!.AbsolutePath.Contains("preflight")
                ? Json(HttpStatusCode.OK, """{"status":"LegacyAdoptable"}""")
                : Json(HttpStatusCode.OK, """{"ok":true,"code":"ADOPTED"}""");

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "legacy-sm",
            Password = "secret",
            Email = "a@x.com",
            PhoneNumber = "0700"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Success, result.Status);
        Assert.NotNull(result.GlobalAccountId);
        Assert.DoesNotContain(handler.Calls, c =>
            c.Method == "POST" && !c.Url.Contains("preflight") && c.Body != null
            && !c.Body.Contains(result.GlobalAccountId.Value.ToString("D"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LegacyConflict_On_One_Branch_Blocks_All_Writes()
    {
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (req, _) =>
        {
            if (req.RequestUri!.ToString().Contains("19003")
                && req.RequestUri!.AbsolutePath.Contains("preflight"))
            {
                return Json(HttpStatusCode.OK, """{"status":"LegacyConflict"}""");
            }

            return Json(HttpStatusCode.OK, """{"status":"LegacyAdoptable"}""");
        };

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "legacy-sm",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Conflict, result.Status);
        Assert.DoesNotContain(handler.Calls, c =>
            c.Method == "POST" && !c.Url.Contains("preflight", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Existing_Global_On_Subset_Reused_For_Adoption()
    {
        var existing = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (req, body) =>
        {
            if (!req.RequestUri!.AbsolutePath.Contains("preflight"))
            {
                return Json(HttpStatusCode.OK, """{"ok":true,"code":"ADOPTED"}""");
            }

            // Discovery (null global): najaf already bound, karkh legacy.
            if (body != null && body.Contains("\"globalAccountId\":null", StringComparison.Ordinal))
            {
                if (req.RequestUri!.ToString().Contains("19001"))
                {
                    return Json(HttpStatusCode.OK,
                        $$"""{"status":"ExistingSameGlobalAccount","globalAccountId":"{{existing:D}}"}""");
                }

                return Json(HttpStatusCode.OK, """{"status":"LegacyAdoptable"}""");
            }

            return Json(HttpStatusCode.OK, """{"status":"ExistingSameGlobalAccount"}""");
        };

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "legacy-sm",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Success, result.Status);
        Assert.Equal(existing, result.GlobalAccountId);
    }

    [Fact]
    public async Task Two_Different_Observed_Globals_Conflict()
    {
        var (orch, handler) = Build(requireDemo: false);
        handler.Responder = (req, _) =>
        {
            if (!req.RequestUri!.AbsolutePath.Contains("preflight"))
            {
                return Json(HttpStatusCode.OK, """{"ok":true}""");
            }

            if (req.RequestUri!.ToString().Contains("19001"))
            {
                return Json(HttpStatusCode.OK,
                    """{"status":"ExistingSameGlobalAccount","globalAccountId":"11111111-1111-1111-1111-111111111111"}""");
            }

            return Json(HttpStatusCode.OK,
                """{"status":"ExistingSameGlobalAccount","globalAccountId":"22222222-2222-2222-2222-222222222222"}""");
        };

        var result = await orch.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = "legacy-sm",
            Password = "secret"
        }, CancellationToken.None);

        Assert.Equal(GlobalOrchestrationStatus.Conflict, result.Status);
        Assert.DoesNotContain(handler.Calls, c =>
            c.Method == "POST" && !c.Url.Contains("preflight", StringComparison.OrdinalIgnoreCase));
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
