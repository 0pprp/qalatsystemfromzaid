using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BE_Company.Sales.Services
{
    public sealed class WrongSaleListForwardRequest
    {
        public string? CityValue { get; init; }
        public bool Reviewed { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
    }

    public sealed class WrongSaleReviewForwardRequest
    {
        public string? Note { get; init; }
        public string? ReviewerUserName { get; init; }
        public string? ReviewerDisplayName { get; init; }
    }

    public sealed class WrongSaleGatewayForwardResult
    {
        public bool Ok { get; init; }
        public int FailureStatusCode { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ResponseBody { get; init; }

        public static WrongSaleGatewayForwardResult Success(string? body = null) =>
            new() { Ok = true, ResponseBody = body };

        public static WrongSaleGatewayForwardResult Failure(int status, string message, string? body = null) =>
            new() { Ok = false, FailureStatusCode = status, ErrorMessage = message, ResponseBody = body };
    }

    public interface IWrongSaleGatewayForwarder
    {
        Task<WrongSaleGatewayForwardResult> ListAsync(
            WrongSaleListForwardRequest request,
            CancellationToken ct = default);

        Task<WrongSaleGatewayForwardResult> GetAsync(Guid id, CancellationToken ct = default);

        Task<WrongSaleGatewayForwardResult> ReviewAsync(
            Guid id,
            WrongSaleReviewForwardRequest request,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Server-to-server bridge: Branch Manager JWT → BE_SalesEmployee internal/wrong-sales.
    /// </summary>
    public sealed class WrongSaleGatewayForwarder : IWrongSaleGatewayForwarder
    {
        public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
        public const string RelativePath = "internal/wrong-sales";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WrongSaleGatewayForwarder> _logger;

        public WrongSaleGatewayForwarder(
            HttpClient http,
            IConfiguration configuration,
            ILogger<WrongSaleGatewayForwarder> logger)
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

        public Task<WrongSaleGatewayForwardResult> ListAsync(
            WrongSaleListForwardRequest request,
            CancellationToken ct = default)
        {
            var query = new List<string>
            {
                $"reviewed={(request.Reviewed ? "true" : "false")}"
            };
            if (!string.IsNullOrWhiteSpace(request.CityValue))
                query.Add($"cityValue={Uri.EscapeDataString(request.CityValue.Trim())}");
            if (request.Page is int page)
                query.Add($"page={page}");
            if (request.PageSize is int pageSize)
                query.Add($"pageSize={pageSize}");

            return SendAsync(HttpMethod.Get, $"{RelativePath}?{string.Join("&", query)}", body: null, ct);
        }

        public Task<WrongSaleGatewayForwardResult> GetAsync(Guid id, CancellationToken ct = default) =>
            SendAsync(HttpMethod.Get, $"{RelativePath}/{id:D}", body: null, ct);

        public Task<WrongSaleGatewayForwardResult> ReviewAsync(
            Guid id,
            WrongSaleReviewForwardRequest request,
            CancellationToken ct = default) =>
            SendAsync(HttpMethod.Post, $"{RelativePath}/{id:D}/review", request, ct);

        private async Task<WrongSaleGatewayForwardResult> SendAsync(
            HttpMethod method,
            string relativePath,
            object? body,
            CancellationToken ct)
        {
            var baseUrl = NormalizeBaseUrl(_configuration["SalesGateway:BaseUrl"]);
            var apiKey = _configuration["InternalApiKey"]
                         ?? _configuration["SalesEmployee:GatewayKey"]
                         ?? _configuration["SalesGateway:ApiKey"];

            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("SalesGateway BaseUrl or key missing; cannot forward wrong-sales.");
                return WrongSaleGatewayForwardResult.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    "خدمة المبيعات الخاطئة غير مهيأة على هذا الفرع");
            }

            using var message = new HttpRequestMessage(method, baseUrl + relativePath);
            message.Headers.TryAddWithoutValidation(GatewayKeyHeader, apiKey.Trim());
            if (body != null)
            {
                message.Content = new StringContent(
                    JsonSerializer.Serialize(body, JsonOptions),
                    Encoding.UTF8,
                    "application/json");
            }

            try
            {
                using var response = await _http.SendAsync(message, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    return WrongSaleGatewayForwardResult.Success(responseBody);
                }

                var status = (int)response.StatusCode;
                if (status == StatusCodes.Status401Unauthorized)
                {
                    status = StatusCodes.Status502BadGateway;
                }

                return WrongSaleGatewayForwardResult.Failure(
                    status,
                    TryMessage(responseBody) ?? "تعذر تحميل المبيعات الخاطئة",
                    responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wrong-sale forward threw");
                return WrongSaleGatewayForwardResult.Failure(
                    StatusCodes.Status502BadGateway,
                    "تعذر الاتصال بخدمة المبيعات الخاطئة");
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
