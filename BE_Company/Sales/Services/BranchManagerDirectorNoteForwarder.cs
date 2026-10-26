using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BE_Company.Sales.Services
{
    public sealed class BranchManagerDirectorNoteForwardRequest
    {
        public string Message { get; init; } = "";
        public string? Subject { get; init; }
        public string SourceApp { get; init; } = "branch_manager_application";
        public string SourceType { get; init; } = "manager_note";
        public string? CityValue { get; init; }
        public string? CityName { get; init; }
        public string? SenderUserId { get; init; }
        public string? SenderUserName { get; init; }
        public string SenderDisplayName { get; init; } = "";
        public string SenderRole { get; init; } = "مدير فرع";
        public string? MetadataJson { get; init; }
    }

    public sealed class BranchManagerDirectorNoteForwardResult
    {
        public bool Ok { get; init; }
        public int FailureStatusCode { get; init; }
        public string? ErrorMessage { get; init; }

        public static BranchManagerDirectorNoteForwardResult Success() => new() { Ok = true };

        public static BranchManagerDirectorNoteForwardResult Failure(int status, string message) =>
            new() { Ok = false, FailureStatusCode = status, ErrorMessage = message };
    }

    public interface IBranchManagerDirectorNoteForwarder
    {
        Task<BranchManagerDirectorNoteForwardResult> ForwardAsync(
            BranchManagerDirectorNoteForwardRequest request,
            CancellationToken ct = default);
    }

    /// <summary>BM → gateway internal/director-notes (المدير / حيدر).</summary>
    public sealed class BranchManagerDirectorNoteForwarder : IBranchManagerDirectorNoteForwarder
    {
        public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
        public const string RelativePath = "internal/director-notes";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BranchManagerDirectorNoteForwarder> _logger;

        public BranchManagerDirectorNoteForwarder(
            HttpClient http,
            IConfiguration configuration,
            ILogger<BranchManagerDirectorNoteForwarder> logger)
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

        public async Task<BranchManagerDirectorNoteForwardResult> ForwardAsync(
            BranchManagerDirectorNoteForwardRequest request,
            CancellationToken ct = default)
        {
            var baseUrl = NormalizeBaseUrl(_configuration["SalesGateway:BaseUrl"]);
            var apiKey = _configuration["SalesEmployee:GatewayKey"]
                         ?? _configuration["SalesGateway:ApiKey"];
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("SalesGateway BaseUrl or key missing; cannot forward BM director note.");
                return BranchManagerDirectorNoteForwardResult.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    "خدمة ملاحظات المدير غير مهيأة على هذا الفرع");
            }

            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl), RelativePath));
            message.Headers.TryAddWithoutValidation(GatewayKeyHeader, apiKey.Trim());
            message.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    message = request.Message,
                    subject = request.Subject,
                    sourceApp = request.SourceApp,
                    sourceType = request.SourceType,
                    cityValue = request.CityValue,
                    cityName = request.CityName,
                    senderUserId = request.SenderUserId,
                    senderUserName = request.SenderUserName,
                    senderDisplayName = request.SenderDisplayName,
                    senderRole = request.SenderRole,
                    metadataJson = request.MetadataJson
                }, JsonOptions),
                Encoding.UTF8,
                "application/json");

            try
            {
                using var response = await _http.SendAsync(message, ct);
                if (response.IsSuccessStatusCode)
                    return BranchManagerDirectorNoteForwardResult.Success();

                var body = await response.Content.ReadAsStringAsync(ct);
                var status = (int)response.StatusCode;
                if (status == StatusCodes.Status401Unauthorized)
                    status = StatusCodes.Status502BadGateway;

                _logger.LogWarning("BM director-note forward failed with {Status}: {Body}",
                    (int)response.StatusCode,
                    body.Length > 300 ? body[..300] : body);
                return BranchManagerDirectorNoteForwardResult.Failure(
                    status,
                    TryMessage(body) ?? "تعذر إرسال الملاحظة إلى المدير");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BM director-note forward threw");
                return BranchManagerDirectorNoteForwardResult.Failure(
                    StatusCodes.Status502BadGateway,
                    "تعذر الاتصال بخدمة المدير");
            }
        }

        private static string? TryMessage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m))
                    return m.GetString();
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
