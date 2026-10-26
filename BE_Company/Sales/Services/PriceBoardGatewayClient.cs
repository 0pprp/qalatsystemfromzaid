using System.Net.Http.Headers;
using System.Text.Json;

namespace BE_Company.Sales.Services;

public interface IPriceBoardGatewayClient
{
    Task<(int Status, string? Body)> ListByCityAsync(string cityValue, CancellationToken ct);
}

/// <summary>Branch → gateway internal CRM price-board read (filtered by city).</summary>
public sealed class PriceBoardGatewayClient : IPriceBoardGatewayClient
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PriceBoardGatewayClient> _logger;

    public PriceBoardGatewayClient(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<PriceBoardGatewayClient> logger)
    {
        _httpFactory = httpFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(int Status, string? Body)> ListByCityAsync(string cityValue, CancellationToken ct)
    {
        var baseUrl = NormalizeBaseUrl(_configuration["SalesGateway:BaseUrl"]);
        var apiKey = _configuration["InternalApiKey"]
                     ?? _configuration["SalesEmployee:GatewayKey"]
                     ?? _configuration["SalesGateway:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("SalesGateway BaseUrl or key missing; cannot load price board.");
            return (StatusCodes.Status503ServiceUnavailable,
                JsonSerializer.Serialize(new { message = "لوحة الأسعار غير مهيأة على هذا الفرع" }));
        }

        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return (StatusCodes.Status400BadRequest,
                JsonSerializer.Serialize(new { message = "محافظة الفرع غير معرّفة" }));
        }

        var url = $"{baseUrl}internal/crm/price-board?cityValue={Uri.EscapeDataString(cityValue.Trim())}";
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.TryAddWithoutValidation(GatewayKeyHeader, apiKey.Trim());
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            var client = _httpFactory.CreateClient();
            using var response = await client.SendAsync(message, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Price board gateway call failed");
            return (StatusCodes.Status502BadGateway,
                JsonSerializer.Serialize(new { message = "تعذر الاتصال بلوحة الأسعار" }));
        }
    }

    private static string NormalizeBaseUrl(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(s))
            return "";
        if (!s.EndsWith('/'))
            s += "/";
        if (!s.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            s += "api/";
        return s;
    }
}
