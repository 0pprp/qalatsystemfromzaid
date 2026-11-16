using System.Collections.Concurrent;
using System.Text.Json;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.CustomerApp;

/// <summary>
/// BE_CustomerApp → Qalaa branches. Reads fan out over every in-scope branch (per-branch timeout,
/// partial results); orders go to one branch's existing CRM intake with source="CustomerApp".
/// </summary>
public sealed class CustomerAppGatewayService
{
    public static readonly TimeSpan DefaultBranchTimeout = TimeSpan.FromSeconds(8);
    public const string Source = "CustomerApp";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly AdminCitiesService _cities;
    private readonly BranchProxyService _proxy;
    private readonly ILogger<CustomerAppGatewayService> _logger;

    public CustomerAppGatewayService(
        AdminCitiesService cities,
        BranchProxyService proxy,
        ILogger<CustomerAppGatewayService> logger)
    {
        _cities = cities;
        _proxy = proxy;
        _logger = logger;
    }

    /// <summary>Per-branch timeout for reads (accounts fan-out, status). Detail gets twice this.</summary>
    public TimeSpan PerBranchTimeout { get; init; } = DefaultBranchTimeout;

    public async Task<List<CustomerAppBranch>> GetBranchesAsync(CancellationToken ct) =>
        CustomerAppBranchScope.Filter(await _cities.GetSalesBranchesAsync(ct));

    public async Task<CustomerAppGatewayResult> GetAccountsByPhoneAsync(string? phone, CancellationToken ct)
    {
        var local = CustomerAppPhone.ToLocal(phone);
        if (local.Length == 0)
        {
            return BadRequest("رقم الهاتف غير صالح.");
        }

        var branches = await GetBranchesAsync(ct);
        var accounts = new ConcurrentBag<CustomerAppAccount>();
        var unavailable = new ConcurrentBag<string>();
        await Task.WhenAll(branches.Select(async branch =>
        {
            var (status, body) = await FetchAsync(
                branch,
                $"internal/customer-app/accounts-by-phone?phone={Uri.EscapeDataString(local)}",
                HttpMethod.Get,
                null,
                PerBranchTimeout,
                ct);
            var parsed = status is >= 200 and < 300 ? TryDeserialize<BranchAccountsEnvelope>(body) : null;
            if (parsed is null)
            {
                unavailable.Add(branch.Name);
                return;
            }

            foreach (var account in parsed.Accounts ?? [])
            {
                Stamp(account, branch);
                accounts.Add(account);
            }
        }));

        var names = unavailable.ToList();
        return new CustomerAppGatewayResult(200, new CustomerAppAccountsResult
        {
            Partial = names.Count > 0,
            UnavailableBranches = branches.Where(b => names.Contains(b.Name)).Select(b => b.Name).ToList(),
            Accounts = accounts
                .OrderByDescending(a => a.SaleDate ?? DateTime.MinValue)
                .ThenBy(a => a.CityValue)
                .ThenByDescending(a => a.CustomerId)
                .ToList()
        });
    }

    public async Task<CustomerAppGatewayResult> GetAccountDetailAsync(int cityValue, int customerId, string? phone, CancellationToken ct)
    {
        var local = CustomerAppPhone.ToLocal(phone);
        if (local.Length == 0)
        {
            return BadRequest("رقم الهاتف غير صالح.");
        }

        var branch = (await GetBranchesAsync(ct)).FirstOrDefault(b => b.CityValue == cityValue);
        if (branch is null || customerId <= 0)
        {
            return NotFound();
        }

        var (status, body) = await FetchAsync(
            branch,
            $"internal/customer-app/accounts/{customerId}?phone={Uri.EscapeDataString(local)}",
            HttpMethod.Get,
            null,
            PerBranchTimeout * 2,
            ct);
        if (status == 404)
        {
            return NotFound();
        }

        var detail = status is >= 200 and < 300 ? TryDeserialize<CustomerAppAccountDetail>(body) : null;
        if (detail is null)
        {
            return Unavailable(branch);
        }

        Stamp(detail.Account, branch);
        return new CustomerAppGatewayResult(200, detail);
    }

    /// <summary>Payment progress of a sale in one branch; 404 until the branch has posted it.</summary>
    public async Task<CustomerAppGatewayResult> GetSaleProgressAsync(int cityValue, int saleId, CancellationToken ct)
    {
        var branch = (await GetBranchesAsync(ct)).FirstOrDefault(b => b.CityValue == cityValue);
        if (branch is null || saleId <= 0)
        {
            return NotFound();
        }

        var (status, body) = await FetchAsync(
            branch,
            $"internal/customer-app/sales/{saleId}/progress",
            HttpMethod.Get,
            null,
            PerBranchTimeout * 2,
            ct);
        if (status == 404)
        {
            return NotFound();
        }

        var progress = status is >= 200 and < 300 ? TryDeserialize<CustomerAppSaleProgress>(body) : null;
        if (progress is null)
        {
            return Unavailable(branch);
        }

        progress.CityValue = cityValue;
        return new CustomerAppGatewayResult(200, progress);
    }

    public async Task<CustomerAppGatewayResult> CreateSalesRequestAsync(CustomerAppSalesRequestBody? body, CancellationToken ct)
    {
        body ??= new CustomerAppSalesRequestBody();
        if (body.OrderUuid == Guid.Empty)
        {
            return BadRequest("orderUuid مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(body.CustomerName))
        {
            return BadRequest("اسم الزبون مطلوب.");
        }

        var branch = (await GetBranchesAsync(ct)).FirstOrDefault(b => b.CityValue == body.CityValue);
        if (branch is null)
        {
            return new CustomerAppGatewayResult(422, Error("الفرع المختار غير متاح لطلبات تطبيق المروّجين."));
        }

        var payload = JsonSerializer.Serialize(new
        {
            crmOrderUuid = body.OrderUuid,
            customerName = body.CustomerName,
            customerPhone = body.CustomerPhone,
            customerProvince = body.CustomerProvince,
            customerAddress = body.CustomerAddress,
            notes = body.Notes,
            productName = body.ProductName,
            submittedByName = string.IsNullOrWhiteSpace(body.SubmittedByName) ? null : body.SubmittedByName.Trim(),
            source = Source
        }, JsonOptions);

        // No CancelAfter: intake runs evaluation + auto-assign (same as the CRM intake proxy).
        var (status, responseBody) = await FetchAsync(
            branch, "internal/crm/sales-requests", HttpMethod.Post, payload, null, ct);
        if (status is < 200 or >= 300)
        {
            if (status is 400 or 409 or 422)
            {
                return new CustomerAppGatewayResult(status, Error(ReadMessage(responseBody) ?? "رفض الفرع الطلب."));
            }

            return Unavailable(branch);
        }

        var created = TryDeserialize<BranchIntakeResult>(responseBody);
        if (created is null || created.SalesRequestId <= 0)
        {
            return Unavailable(branch);
        }

        return new CustomerAppGatewayResult(200, new CustomerAppSalesRequestCreated
        {
            CityValue = branch.CityValue,
            CityName = branch.Name,
            SalesRequestId = created.SalesRequestId,
            Status = created.Status,
            IdempotentReplay = created.IdempotentReplay
        });
    }

    public async Task<CustomerAppGatewayResult> GetStatusesAsync(CustomerAppStatusBody? body, CancellationToken ct)
    {
        var items = body?.Items ?? [];
        if (items.Count > CustomerAppStatusGrouping.MaxItems)
        {
            return BadRequest($"الحد الأقصى {CustomerAppStatusGrouping.MaxItems} طلباً في كل استعلام.");
        }

        var groups = CustomerAppStatusGrouping.Group(items, await GetBranchesAsync(ct));
        var result = new ConcurrentBag<CustomerAppStatusItem>();
        var unavailable = new ConcurrentBag<string>();
        await Task.WhenAll(groups.ByBranch.Values.Select(async group =>
        {
            var payload = JsonSerializer.Serialize(new { orderUuids = group.OrderUuids }, JsonOptions);
            var (status, responseBody) = await FetchAsync(
                group.Branch,
                "internal/customer-app/sales-requests/status",
                HttpMethod.Post,
                payload,
                PerBranchTimeout,
                ct);
            var parsed = status is >= 200 and < 300 ? TryDeserialize<BranchStatusEnvelope>(responseBody) : null;
            if (parsed is null)
            {
                unavailable.Add(group.Branch.Name);
                return;
            }

            foreach (var item in MergeBranchStatuses(group.Branch, group.OrderUuids, parsed.Items ?? []))
            {
                result.Add(item);
            }
        }));

        var merged = result.ToList();
        merged.AddRange(groups.OutOfScope.Select(i => new CustomerAppStatusItem
        {
            OrderUuid = i.OrderUuid,
            CityValue = i.CityValue,
            Found = false
        }));

        // Keep request order.
        var order = items
            .Select((i, idx) => (Key: (i.CityValue, i.OrderUuid), idx))
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().idx);
        return new CustomerAppGatewayResult(200, new CustomerAppStatusResult
        {
            Items = merged
                .OrderBy(i => order.TryGetValue((i.CityValue, i.OrderUuid), out var idx) ? idx : int.MaxValue)
                .ToList(),
            UnavailableBranches = unavailable.Distinct().ToList()
        });
    }

    /// <summary>One item per requested uuid of that branch; uuids the branch did not return → found=false.</summary>
    public static List<CustomerAppStatusItem> MergeBranchStatuses(
        CustomerAppBranch branch,
        IReadOnlyList<Guid> requested,
        IEnumerable<CustomerAppStatusItem> branchItems)
    {
        var byUuid = branchItems
            .Where(i => i.OrderUuid != Guid.Empty)
            .GroupBy(i => i.OrderUuid)
            .ToDictionary(g => g.Key, g => g.First());
        return requested.Select(uuid =>
        {
            if (byUuid.TryGetValue(uuid, out var item))
            {
                item.CityValue = branch.CityValue;
                return item;
            }

            return new CustomerAppStatusItem { OrderUuid = uuid, CityValue = branch.CityValue, Found = false };
        }).ToList();
    }

    private async Task<(int Status, string Body)> FetchAsync(
        CustomerAppBranch branch,
        string path,
        HttpMethod method,
        string? jsonBody,
        TimeSpan? timeout,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t)
        {
            cts.CancelAfter(t);
        }

        try
        {
            using var response = await _proxy.SendManagerAsync(branch.City.Link, path, method, jsonBody, null, cts.Token);
            var text = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 404)
            {
                _logger.LogWarning(
                    "Customer-app {Path} on branch {City} failed {Status}: {Body}",
                    path.Split('?')[0],
                    branch.CityValue,
                    (int)response.StatusCode,
                    text.Length > 300 ? text[..300] : text);
            }

            return ((int)response.StatusCode, text);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested
                                   && ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogWarning("Customer-app {Path} on branch {City} unavailable: {Error}",
                path.Split('?')[0], branch.CityValue, ex.Message);
            return (0, "");
        }
    }

    private static void Stamp(CustomerAppAccount account, CustomerAppBranch branch)
    {
        account.CityValue = branch.CityValue;
        account.CityName = branch.Name;
    }

    private static T? TryDeserialize<T>(string body) where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadMessage(string body)
    {
        var root = BranchProxyService.TryParseJson(body);
        if (root is { ValueKind: JsonValueKind.Object } obj
            && obj.TryGetProperty("message", out var m)
            && m.ValueKind == JsonValueKind.String)
        {
            return m.GetString();
        }

        return null;
    }

    // Consumers read "error"; existing gateway endpoints use "message" — send both.
    private static object Error(string message) => new { message, error = message };

    private static CustomerAppGatewayResult BadRequest(string message) => new(400, Error(message));

    private static CustomerAppGatewayResult NotFound() => new(404, Error("الحساب غير موجود."));

    private static CustomerAppGatewayResult Unavailable(CustomerAppBranch branch) =>
        new(502, Error($"تعذر الاتصال بفرع {branch.Name} حالياً."));

    private sealed class BranchAccountsEnvelope
    {
        public List<CustomerAppAccount>? Accounts { get; set; }
    }

    private sealed class BranchStatusEnvelope
    {
        public List<CustomerAppStatusItem>? Items { get; set; }
    }

    private sealed class BranchIntakeResult
    {
        public int SalesRequestId { get; set; }
        public string? Status { get; set; }
        public bool IdempotentReplay { get; set; }
    }
}
