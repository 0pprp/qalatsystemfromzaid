using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BE_Company.Sales.Services
{
    public sealed class BranchManagerComplaintForwardRequest
    {
        public string Message { get; init; } = "";
        public string? Subject { get; init; }
        public string SourceApp { get; init; } = "branch_manager_application";
        public string SourceType { get; init; } = "delegated_manager_request";
        public string? CityValue { get; init; }
        public string? CityName { get; init; }
        public string? SenderUserId { get; init; }
        public string? SenderUserName { get; init; }
        public string SenderDisplayName { get; init; } = "";
        public string SenderRole { get; init; } = "مدير فرع";
        public string? MetadataJson { get; init; }
    }

    public sealed class BranchManagerComplaintForwardResult
    {
        public bool Ok { get; init; }
        public int FailureStatusCode { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ResponseBody { get; init; }

        public static BranchManagerComplaintForwardResult Success(string? body = null) =>
            new() { Ok = true, ResponseBody = body };

        public static BranchManagerComplaintForwardResult Failure(int status, string message) =>
            new() { Ok = false, FailureStatusCode = status, ErrorMessage = message };
    }

    public interface IBranchManagerComplaintForwarder
    {
        Task<BranchManagerComplaintForwardResult> ForwardAsync(
            BranchManagerComplaintForwardRequest request,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Server-to-server bridge: Branch Manager JWT → BE_SalesEmployee internal/complaints.
    /// Renamed in the app as «طلب من المدير المفوض» (same inbox path as complaints).
    /// </summary>
    public sealed class BranchManagerComplaintForwarder : IBranchManagerComplaintForwarder
    {
        public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
        public const string RelativePath = "internal/complaints";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BranchManagerComplaintForwarder> _logger;

        public BranchManagerComplaintForwarder(
            HttpClient http,
            IConfiguration configuration,
            ILogger<BranchManagerComplaintForwarder> logger)
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

        public async Task<BranchManagerComplaintForwardResult> ForwardAsync(
            BranchManagerComplaintForwardRequest request,
            CancellationToken ct = default)
        {
            var baseUrl = NormalizeBaseUrl(_configuration["SalesGateway:BaseUrl"]);
            var apiKey = _configuration["SalesEmployee:GatewayKey"]
                         ?? _configuration["SalesGateway:ApiKey"];
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("SalesGateway BaseUrl or key missing; cannot forward BM delegated-manager request.");
                return BranchManagerComplaintForwardResult.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    "خدمة المدير المفوض غير مهيأة على هذا الفرع");
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
                var body = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    return BranchManagerComplaintForwardResult.Success(body);
                }

                var status = (int)response.StatusCode;
                if (status == StatusCodes.Status401Unauthorized)
                {
                    status = StatusCodes.Status502BadGateway;
                }

                _logger.LogWarning(
                    "BM complaint forward failed with {Status}: {Body}",
                    (int)response.StatusCode,
                    body.Length > 300 ? body[..300] : body);
                return BranchManagerComplaintForwardResult.Failure(
                    status,
                    TryMessage(body) ?? "تعذر إرسال الطلب إلى المدير المفوض");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BM complaint forward threw");
                return BranchManagerComplaintForwardResult.Failure(
                    StatusCodes.Status502BadGateway,
                    "تعذر الاتصال بخدمة المدير المفوض");
            }
        }

        private static string? TryMessage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m))
                {
                    return m.GetString();
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
