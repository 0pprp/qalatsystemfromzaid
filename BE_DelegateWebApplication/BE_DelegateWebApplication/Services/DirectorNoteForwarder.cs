using System.Text;
using System.Text.Json;

namespace BE_DelegateWebApplication.Services;

public sealed class DirectorNoteForwardRequest
{
    public string Message { get; init; } = "";
    public string? Subject { get; init; }
    public string SourceApp { get; init; } = "";
    public string SourceType { get; init; } = "manager_note";
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
    public string? SenderUserId { get; init; }
    public string? SenderUserName { get; init; }
    public string SenderDisplayName { get; init; } = "";
    public string SenderRole { get; init; } = "";
    public string? MetadataJson { get; init; }
}

public sealed class DirectorNoteForwardResult
{
    public bool Ok { get; init; }
    public int FailureStatusCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static DirectorNoteForwardResult Success() => new() { Ok = true };

    public static DirectorNoteForwardResult Failure(int failureStatusCode, string errorMessage) =>
        new() { Ok = false, FailureStatusCode = failureStatusCode, ErrorMessage = errorMessage };
}

public interface IDirectorNoteForwarder
{
    Task<DirectorNoteForwardResult> ForwardAsync(
        DirectorNoteForwardRequest request,
        CancellationToken ct = default);
}

/// <summary>
/// Server-to-server bridge into the Director (حيدر) notes inbox on BE_SalesEmployee.
/// </summary>
public sealed class DirectorNoteForwarder : IDirectorNoteForwarder
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
    public const string RelativePath = "internal/director-notes";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DirectorNoteForwarder> _logger;

    public DirectorNoteForwarder(
        HttpClient http,
        IConfiguration configuration,
        ILogger<DirectorNoteForwarder> logger)
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

    public async Task<DirectorNoteForwardResult> ForwardAsync(
        DirectorNoteForwardRequest request,
        CancellationToken ct = default)
    {
        var baseUrl = NormalizeBaseUrl(_configuration["SalesGateway:BaseUrl"]);
        var apiKey = _configuration["InternalApiKey"] ?? _configuration["SalesGateway:ApiKey"];

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError(
                "SalesGateway:BaseUrl or the internal gateway key is missing; director notes cannot be forwarded.");
            return DirectorNoteForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "خدمة ملاحظات المدير غير مهيأة، يرجى مراجعة الإدارة");
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
                return DirectorNoteForwardResult.Success();

            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "Director notes inbox rejected forward with {StatusCode}: {Body}",
                (int)response.StatusCode,
                body);
            return DirectorNoteForwardResult.Failure(
                StatusCodes.Status502BadGateway,
                "تعذر تسليم الملاحظة إلى المدير، يرجى المحاولة لاحقًا");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not reach the director notes inbox.");
            return DirectorNoteForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "تعذر الاتصال بخدمة المدير حاليًا، يرجى المحاولة لاحقًا");
        }
    }
}
