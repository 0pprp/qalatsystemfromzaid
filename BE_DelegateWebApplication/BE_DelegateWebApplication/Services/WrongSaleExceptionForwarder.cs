using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BE_DelegateWebApplication.Services;

public sealed class WrongSaleExceptionForwardRequest
{
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
    public int? CustomerId { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public int? SalesRequestId { get; init; }
    public string? Reason { get; init; }
    public string? RequesterUserId { get; init; }
    public string? RequesterUserName { get; init; }
    public string? RequesterDisplayName { get; init; }
}

public sealed class WrongSaleExceptionForwardResult
{
    public bool Ok { get; init; }
    public int FailureStatusCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static WrongSaleExceptionForwardResult Success() => new() { Ok = true };

    public static WrongSaleExceptionForwardResult Failure(int status, string message) =>
        new() { Ok = false, FailureStatusCode = status, ErrorMessage = message };
}

public interface IWrongSaleExceptionForwarder
{
    Task<WrongSaleExceptionForwardResult> ForwardAsync(
        WrongSaleExceptionForwardRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Best-effort bridge: WrongSale from delegate new-sale → gateway SalesExceptionRequests
/// (طلبات الاستثناء / مبيعات خاطئة) via internal/sales-exceptions.
/// </summary>
public sealed class WrongSaleExceptionForwarder : IWrongSaleExceptionForwarder
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
    public const string RelativePath = "internal/sales-exceptions";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WrongSaleExceptionForwarder> _logger;

    public WrongSaleExceptionForwarder(
        HttpClient http,
        IConfiguration configuration,
        ILogger<WrongSaleExceptionForwarder> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = (baseUrl ?? "").Trim();
        return value.Length == 0 || value.EndsWith('/') ? value : value + "/";
    }

    public async Task<WrongSaleExceptionForwardResult> ForwardAsync(
        WrongSaleExceptionForwardRequest request,
        CancellationToken ct = default)
    {
        var baseUrl = NormalizeBaseUrl(_configuration["SalesGateway:BaseUrl"]);
        var apiKey = _configuration["InternalApiKey"] ?? _configuration["SalesGateway:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "SalesGateway not configured; WrongSale stayed on branch WrongSaleReports only.");
            return WrongSaleExceptionForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "بوابة الاستثناء غير مهيأة");
        }

        // Complaints use Branch:CityValue (e.g. DatabaseCompanyNajaf). WrongSale used to send
        // Arabic province/district ("الكوفة") or null → gateway reject «المحافظة مطلوبة».
        var cityValue = ResolveCityValue(request.CityValue);
        var cityName = string.IsNullOrWhiteSpace(request.CityName)
            ? (_configuration["Branch:CityName"] ?? cityValue)
            : request.CityName.Trim();
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            _logger.LogWarning(
                "WrongSale forward aborted: CityValue missing (customer/province empty and Branch:CityValue unset).");
            return WrongSaleExceptionForwardResult.Failure(
                StatusCodes.Status400BadRequest,
                "محافظة الفرع غير مهيأة لإرسال المبيع الخاطئ");
        }

        var payload = new
        {
            CityValue = cityValue,
            CityName = cityName,
            request.CustomerId,
            request.CustomerName,
            request.CustomerPhone,
            request.SalesRequestId,
            request.Reason,
            TargetApproverType = "DelegatedManager",
            request.RequesterUserId,
            request.RequesterUserName,
            request.RequesterDisplayName,
            Role = "SalesManager"
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, baseUrl + RelativePath)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        message.Headers.TryAddWithoutValidation(GatewayKeyHeader, apiKey.Trim());

        try
        {
            using var response = await _http.SendAsync(message, ct);
            if (response.IsSuccessStatusCode)
            {
                return WrongSaleExceptionForwardResult.Success();
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "WrongSale exception forward rejected with {StatusCode}: {Body}",
                (int)response.StatusCode,
                body);
            return WrongSaleExceptionForwardResult.Failure(
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(body) ? "تعذر إرسال المبيعات الخاطئة للبوابة" : body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WrongSale exception forward failed");
            return WrongSaleExceptionForwardResult.Failure(
                StatusCodes.Status502BadGateway,
                "تعذر الاتصال ببوابة طلبات الاستثناء");
        }
    }

    private string ResolveCityValue(string? raw)
    {
        var configured = (_configuration["Branch:CityValue"]
                          ?? _configuration["SalesGateway:CityValue"]
                          ?? "").Trim();
        var value = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return configured;
        }

        // Arabic district/province labels from customer scope → branch catalog key.
        if (IsNajafFamilyLabel(value))
        {
            return string.IsNullOrWhiteSpace(configured) ? value : configured;
        }

        return value;
    }

    private static bool IsNajafFamilyLabel(string value) =>
        value.Contains("نجف", StringComparison.Ordinal)
        || value.Contains("الكوفة", StringComparison.Ordinal)
        || value.Contains("كوفة", StringComparison.Ordinal)
        || value.Equals("najaf", StringComparison.OrdinalIgnoreCase)
        || value.Equals("najaf-demo", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DatabaseCompanyNajaf", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DatabaseCompanyNajaf_DEMO", StringComparison.OrdinalIgnoreCase);
}

