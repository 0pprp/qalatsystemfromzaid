using System.Text;
using System.Text.Json;

namespace BE_DelegateWebApplication.Services;

/// <summary>
/// Sender identity resolved by this branch. The delegate app never supplies any of it.
/// </summary>
public sealed class CentralComplaintForwardRequest
{
    public string Message { get; init; } = "";
    public string? Subject { get; init; }
    public string SourceApp { get; init; } = "";
    public string SourceType { get; init; } = "";
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
    public string? SenderUserId { get; init; }
    public string? SenderUserName { get; init; }
    public string SenderDisplayName { get; init; } = "";
    public string SenderRole { get; init; } = "";
    public string? MetadataJson { get; init; }
}

public sealed class CentralComplaintForwardResult
{
    public bool Ok { get; init; }

    /// <summary>Status this branch should return to the caller when <see cref="Ok"/> is false.</summary>
    public int FailureStatusCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static CentralComplaintForwardResult Success() => new() { Ok = true };

    public static CentralComplaintForwardResult Failure(int failureStatusCode, string errorMessage) =>
        new() { Ok = false, FailureStatusCode = failureStatusCode, ErrorMessage = errorMessage };
}

public interface ICentralComplaintForwarder
{
    Task<CentralComplaintForwardResult> ForwardAsync(
        CentralComplaintForwardRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Server-to-server bridge into the central complaint inbox on BE_SalesEmployee.
/// The shared internal key lives only in branch configuration — never in the mobile app.
/// </summary>
public sealed class CentralComplaintForwarder : ICentralComplaintForwarder
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
    public const string RelativePath = "internal/complaints";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CentralComplaintForwarder> _logger;

    public CentralComplaintForwarder(
        HttpClient http,
        IConfiguration configuration,
        ILogger<CentralComplaintForwarder> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Always ends with a single slash so the relative path appends cleanly.</summary>
    public static string NormalizeBaseUrl(string? baseUrl)
    {
        var value = (baseUrl ?? "").Trim();
        return value.Length == 0 || value.EndsWith('/') ? value : value + "/";
    }

    public async Task<CentralComplaintForwardResult> ForwardAsync(
        CentralComplaintForwardRequest request,
        CancellationToken ct = default)
    {
        var baseUrl = NormalizeBaseUrl(_configuration["SalesGateway:BaseUrl"]);
        var apiKey = _configuration["InternalApiKey"] ?? _configuration["SalesGateway:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError(
                "SalesGateway:BaseUrl or the internal gateway key is missing; delegate complaints cannot reach the central inbox.");
            return CentralComplaintForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "خدمة الشكاوى المركزية غير مهيأة، يرجى مراجعة الإدارة");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, baseUrl + RelativePath)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        message.Headers.TryAddWithoutValidation(GatewayKeyHeader, apiKey.Trim());

        try
        {
            using var response = await _http.SendAsync(message, ct);
            if (response.IsSuccessStatusCode)
            {
                return CentralComplaintForwardResult.Success();
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Central complaint inbox rejected a forwarded delegate complaint with {StatusCode}: {Body}",
                (int)response.StatusCode,
                body);
            return CentralComplaintForwardResult.Failure(
                StatusCodes.Status502BadGateway,
                "تعذر تسليم الشكوى إلى الإدارة المركزية، يرجى المحاولة لاحقًا");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not reach the central complaint inbox.");
            return CentralComplaintForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "تعذر الاتصال بالإدارة المركزية حاليًا، يرجى المحاولة لاحقًا");
        }
    }
}
