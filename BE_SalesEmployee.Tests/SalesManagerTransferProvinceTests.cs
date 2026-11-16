using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// «تعديل طلب البيع» with a new province goes through TransferProvinceAsync. Values copied from the
/// edit body / source request used to be re-attached JsonNodes → "node already has a parent" → empty 500
/// (shown as «تعذر التعديل»).
/// </summary>
public sealed class SalesManagerTransferProvinceTests
{
    private sealed class FakeBranches : HttpMessageHandler
    {
        public ConcurrentDictionary<string, Func<HttpRequestMessage, string?, HttpResponseMessage>> Routes { get; } = new();
        public ConcurrentBag<(string Host, string Path, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            var key = $"{request.Method} {request.RequestUri!.Host}{request.RequestUri.AbsolutePath}";
            Calls.Add((request.RequestUri.Host, request.RequestUri.AbsolutePath, body));
            return Routes.TryGetValue(key, out var route)
                ? route(request, body)
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private static (SalesManagerBranchAggregator Aggregator, FakeBranches Branches) Create()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["InternalApiKey"] = "test-key",
            ["SalesManagement:RequireDemoDatabase"] = "false",
            ["LabCities:0:Value"] = "2",
            ["LabCities:0:Name"] = "الرصافة",
            ["LabCities:0:Database"] = "DatabaseCompanyBaghdadRusafa",
            ["LabCities:0:Link"] = "http://rusafa/api/",
            ["LabCities:1:Value"] = "3",
            ["LabCities:1:Name"] = "الكرخ",
            ["LabCities:1:Database"] = "DatabaseCompanyBaghdadKarak",
            ["LabCities:1:Link"] = "http://karkh/api/",
        }).Build();

        var handler = new FakeBranches();
        var cities = new AdminCitiesService(
            new HttpClient(), config, NullLogger<AdminCitiesService>.Instance, new MemoryCache(new MemoryCacheOptions()));
        var proxy = new BranchProxyService(new HttpClient(handler), config);
        return (new SalesManagerBranchAggregator(cities, proxy), handler);
    }

    [Fact]
    public async Task Changing_branch_from_the_edit_dialog_creates_the_request_on_the_target_and_archives_the_source()
    {
        var (aggregator, branches) = Create();
        branches.Routes["GET rusafa/api/sales-manager/sales-requests/662"] = (_, _) => Json(new
        {
            id = 662,
            status = "New",
            customerName = "أحمد أكرم والي",
            customerPhone = "07741346956",
            customerAddress = "بغداد – حي البنوك",
            notes = "قديم",
            customerSourceType = "Crm",
            createdByName = "مدير المبيعات",
            createdByUserType = "SalesManager",
            existingCustomerId = (int?)null
        });
        JsonObject? accepted = null;
        branches.Routes["POST karkh/api/sales-manager/sales-requests/accept-province-transfer"] = (_, body) =>
        {
            accepted = JsonNode.Parse(body!)!.AsObject();
            return Json(new { id = 901, status = "New" });
        };
        branches.Routes["POST rusafa/api/sales-manager/sales-requests/662/mark-province-transferred-out"] =
            (_, _) => Json(new { ok = true });

        var editBody = JsonSerializer.Serialize(new
        {
            toCityValue = "3",
            toCityName = "الكرخ",
            toEmployeeId = (int?)null,
            customerName = "أحمد أكرم والي",
            customerPhone = "07741346956",
            customerAddress = "بغداد – حي البنوك – مشويات الكعبي",
            notes = ""
        });

        var (status, payload) = await aggregator.TransferProvinceAsync(
            new GatewayUser { UserName = "manager" }, "2", 662, editBody, CancellationToken.None);

        Assert.Equal(200, status);
        var result = Assert.IsType<JsonObject>(payload);
        Assert.Equal(901, result["toRequestId"]!.GetValue<int>());
        Assert.True(result["assigneeCleared"]!.GetValue<bool>());

        Assert.NotNull(accepted);
        Assert.Equal(662, accepted!["fromRequestId"]!.GetValue<int>());
        // Edited values from the dialog win over the source row.
        Assert.Equal("بغداد – حي البنوك – مشويات الكعبي", accepted["customerAddress"]!.GetValue<string>());
        Assert.Equal("", accepted["notes"]!.GetValue<string>());
        // Source-only fields are carried over.
        Assert.Equal("Crm", accepted["customerSourceType"]!.GetValue<string>());
        Assert.Equal("مدير المبيعات", accepted["createdByName"]!.GetValue<string>());
        Assert.Equal("الكرخ", accepted["customerProvince"]!.GetValue<string>());
        Assert.Contains(branches.Calls, c => c.Path.EndsWith("/662/mark-province-transferred-out"));
    }
}
