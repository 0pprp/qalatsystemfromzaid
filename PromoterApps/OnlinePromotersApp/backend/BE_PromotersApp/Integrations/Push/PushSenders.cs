using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Apis.Auth.OAuth2;

namespace BE_PromotersApp.Integrations.Push;

public sealed record PushMessage(string Title, string Body, IReadOnlyDictionary<string, string> Data);

public enum PushResult { Sent, InvalidToken, Failed }

public interface IPushSender
{
    bool Enabled { get; }
    Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken ct);
}

/// <summary>Used when Firebase is not configured: notifications are still stored in the in-app inbox.</summary>
public sealed class NullPushSender : IPushSender
{
    public bool Enabled => false;
    public Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken ct) => Task.FromResult(PushResult.Sent);
}

/// <summary>
/// Firebase Cloud Messaging HTTP v1. Needs Push:Fcm:ProjectId and Push:Fcm:ServiceAccountFile
/// (service-account JSON from Firebase console → Project settings → Service accounts). iOS delivery
/// additionally needs the APNs auth key uploaded to Firebase.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    private readonly HttpClient _http;
    private readonly ILogger<FcmPushSender> _logger;
    private readonly string _projectId;
    private readonly ITokenAccess _credential;

    public FcmPushSender(HttpClient http, IConfiguration config, ILogger<FcmPushSender> logger)
    {
        _http = http;
        _logger = logger;
        _projectId = config["Push:Fcm:ProjectId"] ?? throw new InvalidOperationException("Push:Fcm:ProjectId missing");
        var file = config["Push:Fcm:ServiceAccountFile"] ?? throw new InvalidOperationException("Push:Fcm:ServiceAccountFile missing");
#pragma warning disable CS0618 // CredentialFactory is the replacement but FromFile remains the simplest supported path here.
        _credential = GoogleCredential.FromFile(file).CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
#pragma warning restore CS0618
    }

    public bool Enabled => true;

    public async Task<PushResult> SendAsync(string deviceToken, PushMessage message, CancellationToken ct)
    {
        var accessToken = await _credential.GetAccessTokenForRequestAsync(cancellationToken: ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title = message.Title, body = message.Body },
                data = message.Data,
                android = new { priority = "high", notification = new { channel_id = "qalaa_default", sound = "default" } },
                apns = new { payload = new { aps = new { sound = "default" } } },
            }
        });

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return PushResult.Sent;
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode is HttpStatusCode.NotFound || body.Contains("UNREGISTERED") || body.Contains("INVALID_ARGUMENT"))
                return PushResult.InvalidToken;
            _logger.LogWarning("FCM send failed {Status}: {Body}", (int)response.StatusCode, body);
            return PushResult.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "FCM send failed");
            return PushResult.Failed;
        }
    }
}
