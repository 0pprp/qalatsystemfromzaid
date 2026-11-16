using System.Text;
using BE_CustomerApp.Domain.Users;

namespace BE_CustomerApp.Integrations.Sms;

public interface ISmsSender
{
    /// <summary>Sends an OTP. Returns false when the provider rejected/failed (caller reports a retryable error).</summary>
    Task<bool> SendOtpAsync(string canonicalPhone, string code, string message, CancellationToken ct);
}

/// <summary>Development sender: writes the code to the log. Never use in production.</summary>
public sealed class LogSmsSender : ISmsSender
{
    private readonly ILogger<LogSmsSender> _logger;
    public LogSmsSender(ILogger<LogSmsSender> logger) => _logger = logger;

    public Task<bool> SendOtpAsync(string canonicalPhone, string code, string message, CancellationToken ct)
    {
        _logger.LogWarning("[DEV SMS] to {Phone}: {Message}", canonicalPhone, message);
        return Task.FromResult(true);
    }
}

/// <summary>
/// Provider-agnostic HTTP sender, configured entirely from Sms:Http so any Iraqi/regional SMS or
/// WhatsApp-OTP gateway can be plugged in without code changes.
/// Placeholders usable in Url/Body/header values: {phone} (07…), {phoneIntl} (+9647…), {phoneIntlNoPlus} (9647…), {code}, {message}.
/// </summary>
public sealed class HttpSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<HttpSmsSender> _logger;

    public HttpSmsSender(HttpClient http, IConfiguration config, ILogger<HttpSmsSender> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendOtpAsync(string canonicalPhone, string code, string message, CancellationToken ct)
    {
        var section = _config.GetSection("Sms:Http");
        var intl = IraqiPhone.ToInternational(canonicalPhone);
        string Fill(string? template, bool urlEncode) => (template ?? "")
            .Replace("{phone}", canonicalPhone)
            .Replace("{phoneIntl}", urlEncode ? Uri.EscapeDataString(intl) : intl)
            .Replace("{phoneIntlNoPlus}", intl.TrimStart('+'))
            .Replace("{code}", code)
            .Replace("{message}", urlEncode ? Uri.EscapeDataString(message) : JsonEscape(message));

        var request = new HttpRequestMessage(
            new HttpMethod(section["Method"] ?? "POST"),
            Fill(section["Url"], urlEncode: true));
        foreach (var header in section.GetSection("Headers").GetChildren())
        {
            request.Headers.TryAddWithoutValidation(header.Key, Fill(header.Value, urlEncode: false));
        }
        var body = section["BodyTemplate"];
        if (!string.IsNullOrEmpty(body))
        {
            request.Content = new StringContent(Fill(body, urlEncode: false), Encoding.UTF8, section["ContentType"] ?? "application/json");
        }

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return true;
            _logger.LogError("SMS provider returned {Status} for {Phone}: {Body}",
                (int)response.StatusCode, IraqiPhone.Mask(canonicalPhone), await response.Content.ReadAsStringAsync(ct));
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "SMS provider call failed for {Phone}", IraqiPhone.Mask(canonicalPhone));
            return false;
        }
    }

    private static string JsonEscape(string s) =>
        System.Text.Json.JsonSerializer.Serialize(s)[1..^1];
}
