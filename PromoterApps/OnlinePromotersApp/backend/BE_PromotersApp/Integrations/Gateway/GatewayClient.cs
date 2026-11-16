using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Integrations.Gateway;

// Shapes returned by BE_SalesEmployee api/internal/customer-app/* (see docs/INTEGRATION.md).

public sealed record GatewayBranch(int CityValue, string Name);

public sealed record GatewaySalesRequestCreate(
    Guid OrderUuid, int CityValue, string CustomerName, string CustomerPhone,
    string CustomerProvince, string CustomerAddress, string Notes, string ProductName, string? SubmittedByName = null);

public sealed record GatewaySalesRequestCreated(int CityValue, string? CityName, int SalesRequestId, string? Status, bool IdempotentReplay);

public sealed record GatewayStatusQuery(int CityValue, Guid OrderUuid);

/// <summary>Payment progress of a company-system sale (installments paid so far). Dates are branch-local.</summary>
public sealed record GatewaySaleProgress(
    int CityValue, int SaleId, long TotalAmount, long PaidAmount, long RemainingAmount, int PaymentsCount, DateTime? LastPaymentDate);

public sealed record GatewayRequestStatus(
    Guid OrderUuid, int CityValue, bool Found, int? SalesRequestId, string? Status,
    bool IsDeleted, string? RejectionReason, int? ConvertedToSaleId, DateTime? UpdatedAtUtc);

public interface IGatewayClient
{
    Task<IReadOnlyList<GatewayBranch>> GetBranchesAsync(CancellationToken ct);
    Task<GatewaySalesRequestCreated> CreateSalesRequestAsync(GatewaySalesRequestCreate request, CancellationToken ct);
    Task<IReadOnlyList<GatewayRequestStatus>> GetStatusesAsync(IReadOnlyList<GatewayStatusQuery> items, CancellationToken ct);
    /// <summary>Null when the branch has no such sale.</summary>
    Task<GatewaySaleProgress?> GetSaleProgressAsync(int cityValue, int saleId, CancellationToken ct);
}

public sealed class GatewayUnavailableException : ApiException
{
    public GatewayUnavailableException(string detail)
        : base(502, "gateway_unavailable", "تعذر الاتصال بمنظومة الشركة حالياً، يرجى المحاولة لاحقاً") => Detail = detail;

    public string Detail { get; }
}

/// <summary>Calls the Sales Gateway with X-Sales-Gateway-Key. Base URL: Gateway:BaseUrl (…/sales-gw/api/).</summary>
public sealed class HttpGatewayClient : IGatewayClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ILogger<HttpGatewayClient> _logger;

    public HttpGatewayClient(HttpClient http, IConfiguration config, ILogger<HttpGatewayClient> logger)
    {
        _http = http;
        _logger = logger;
        var baseUrl = NonEmpty(config["Gateway:BaseUrl"]) ?? throw new InvalidOperationException("Gateway:BaseUrl missing");
        _http.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
        _http.DefaultRequestHeaders.Add("X-Sales-Gateway-Key", NonEmpty(config["Gateway:Key"]) ?? throw new InvalidOperationException("Gateway:Key missing"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<IReadOnlyList<GatewayBranch>> GetBranchesAsync(CancellationToken ct) =>
        await SendAsync<List<GatewayBranch>>(HttpMethod.Get, "internal/customer-app/branches", null, ct) ?? [];

    public async Task<GatewaySalesRequestCreated> CreateSalesRequestAsync(GatewaySalesRequestCreate request, CancellationToken ct) =>
        await SendAsync<GatewaySalesRequestCreated>(HttpMethod.Post, "internal/customer-app/sales-requests", request, ct)
        ?? throw new GatewayUnavailableException("empty create response");

    public async Task<IReadOnlyList<GatewayRequestStatus>> GetStatusesAsync(IReadOnlyList<GatewayStatusQuery> items, CancellationToken ct)
    {
        if (items.Count == 0) return [];
        var result = await SendAsync<StatusEnvelope>(HttpMethod.Post, "internal/customer-app/sales-requests/status", new { items }, ct);
        return result?.Items ?? [];
    }

    public Task<GatewaySaleProgress?> GetSaleProgressAsync(int cityValue, int saleId, CancellationToken ct) =>
        SendAsync<GatewaySaleProgress>(HttpMethod.Get, $"internal/customer-app/sales/{cityValue}/{saleId}/progress", null, ct, allowNotFound: true);

    private static string? NonEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private sealed record StatusEnvelope(List<GatewayRequestStatus>? Items);

    private async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct, bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Gateway {Method} {Path} failed", method, path);
            throw new GatewayUnavailableException(ex.Message);
        }

        using (response)
        {
            if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound) return default;
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gateway {Method} {Path} returned {Status}: {Body}", method, path, (int)response.StatusCode, text);
                if ((int)response.StatusCode is 400 or 422) throw new ApiException(422, "gateway_rejected", ExtractError(text) ?? "رفضت منظومة الشركة الطلب");
                throw new GatewayUnavailableException($"{(int)response.StatusCode}");
            }
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
    }

    private static string? ExtractError(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
