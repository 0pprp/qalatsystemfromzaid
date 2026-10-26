using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BE_Company.Sales.Services;

public sealed class CrmSalesBoardStatusNotification
{
    public Guid CrmOrderUuid { get; init; }
    public int SalesRequestId { get; init; }
    public string? CityValue { get; init; }
    /// <summary>Processing | Completed | Cancelled</summary>
    public string BoardStatus { get; init; } = "";
    public string? RejectReason { get; init; }
}

public interface ICrmSalesBoardStatusNotifier
{
    Task NotifyAsync(CrmSalesBoardStatusNotification notification, CancellationToken ct);
}

/// <summary>
/// Branch → BE_SalesEmployee relay for CRM order status (never called from browsers).
/// Fails soft: logs and continues so sales workflows are not blocked by CRM outage.
/// </summary>
public sealed class CrmSalesBoardStatusForwarder : ICrmSalesBoardStatusNotifier
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
    public const string RelativePath = "internal/crm/sales-board-status";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CrmSalesBoardStatusForwarder> _logger;

    public CrmSalesBoardStatusForwarder(
        HttpClient http,
        IConfiguration configuration,
        ILogger<CrmSalesBoardStatusForwarder> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyAsync(CrmSalesBoardStatusNotification notification, CancellationToken ct)
    {
        if (notification.CrmOrderUuid == Guid.Empty)
        {
            return;
        }

        var baseUrl = (_configuration["SalesGateway:BaseUrl"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("Crm status notify skipped: SalesGateway:BaseUrl missing.");
            return;
        }

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        var key = _configuration["SalesEmployee:GatewayKey"]
                  ?? _configuration["InternalApiKey"]
                  ?? "";
        if (string.IsNullOrWhiteSpace(key))
        {
            _logger.LogWarning("Crm status notify skipped: gateway key missing.");
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + RelativePath);
            request.Headers.TryAddWithoutValidation(GatewayKeyHeader, key);
            request.Content = new StringContent(
                JsonSerializer.Serialize(notification, JsonOptions),
                Encoding.UTF8,
                "application/json");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Crm status notify failed {Status}: {Body}",
                    (int)response.StatusCode,
                    body.Length > 400 ? body[..400] : body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Crm status notify threw for order {CrmOrderUuid}", notification.CrmOrderUuid);
        }
    }
}
