using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BE_Company.Sales.Services
{
    public sealed class SalesExceptionCreateForwardRequest
    {
        public string? CityValue { get; init; }
        public string? CityName { get; init; }
        public int? CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public string? CustomerPhone { get; init; }
        public int? SalesRequestId { get; init; }
        public string? Reason { get; init; }
        public string? TargetApproverType { get; init; }
        public string? RequesterUserId { get; init; }
        public string? RequesterUserName { get; init; }
        public string? RequesterDisplayName { get; init; }
        public string? Role { get; init; }
    }

    public sealed class SalesExceptionListForwardRequest
    {
        public string? Status { get; init; }
        public string? CityValue { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public string? RequesterUserName { get; init; }
    }

    public sealed class SalesExceptionGatewayForwardResult
    {
        public bool Ok { get; init; }
        public int FailureStatusCode { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ResponseBody { get; init; }
        public string ContentType { get; init; } = "application/json";

        public static SalesExceptionGatewayForwardResult Success(string body, string contentType = "application/json") =>
            new() { Ok = true, ResponseBody = body, ContentType = contentType };

        public static SalesExceptionGatewayForwardResult Failure(int failureStatusCode, string errorMessage) =>
            new() { Ok = false, FailureStatusCode = failureStatusCode, ErrorMessage = errorMessage };
    }

    public interface ISalesExceptionGatewayForwarder
    {
        Task<SalesExceptionGatewayForwardResult> CreateAsync(
            SalesExceptionCreateForwardRequest request,
            CancellationToken ct = default);

        Task<SalesExceptionGatewayForwardResult> ListAsync(
            SalesExceptionListForwardRequest request,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Server-to-server bridge into BE_SalesEmployee internal sales-exceptions.
    /// The shared internal key lives only in branch configuration — never in the browser.
    /// Gateway 401 is mapped to 502 so FE auth interceptors do not log the user out.
    /// </summary>
    public sealed class SalesExceptionGatewayForwarder : ISalesExceptionGatewayForwarder
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
        private readonly ILogger<SalesExceptionGatewayForwarder> _logger;

        public SalesExceptionGatewayForwarder(
            HttpClient http,
            IConfiguration configuration,
            ILogger<SalesExceptionGatewayForwarder> logger)
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

        public Task<SalesExceptionGatewayForwardResult> CreateAsync(
            SalesExceptionCreateForwardRequest request,
            CancellationToken ct = default) =>
            SendAsync(HttpMethod.Post, RelativePath, request, ct);

        public Task<SalesExceptionGatewayForwardResult> ListAsync(
            SalesExceptionListForwardRequest request,
            CancellationToken ct = default)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                query.Add($"status={Uri.EscapeDataString(request.Status.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(request.CityValue))
            {
                query.Add($"cityValue={Uri.EscapeDataString(request.CityValue.Trim())}");
            }

            if (request.Page is int page)
            {
                query.Add($"page={page}");
            }

            if (request.PageSize is int pageSize)
            {
                query.Add($"pageSize={pageSize}");
            }

            if (!string.IsNullOrWhiteSpace(request.RequesterUserName))
            {
                query.Add($"requesterUserName={Uri.EscapeDataString(request.RequesterUserName.Trim())}");
            }

            var path = query.Count == 0 ? RelativePath : $"{RelativePath}?{string.Join("&", query)}";
            return SendAsync(HttpMethod.Get, path, body: null, ct);
        }

        private async Task<SalesExceptionGatewayForwardResult> SendAsync(
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
                _logger.LogError(
                    "SalesGateway:BaseUrl or the internal gateway key is missing; sales exceptions cannot reach the central store.");
                return SalesExceptionGatewayForwardResult.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    "خدمة طلبات الاستثناء غير مهيأة، يرجى مراجعة الإدارة");
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
                    var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
                    return SalesExceptionGatewayForwardResult.Success(responseBody, mediaType);
                }

                _logger.LogWarning(
                    "Central sales-exception intake rejected a forwarded request with {StatusCode}: {Body}",
                    (int)response.StatusCode,
                    responseBody);

                // Never pass gateway 401 through — FE would treat it as branch session failure.
                var mapped = response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                    or System.Net.HttpStatusCode.Forbidden
                    ? StatusCodes.Status502BadGateway
                    : StatusCodes.Status502BadGateway;

                return SalesExceptionGatewayForwardResult.Failure(
                    mapped,
                    "تعذر تسليم طلب الاستثناء إلى الإدارة المركزية، يرجى المحاولة لاحقًا");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not reach the central sales-exception intake.");
                return SalesExceptionGatewayForwardResult.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    "تعذر الاتصال بالإدارة المركزية حاليًا، يرجى المحاولة لاحقًا");
            }
        }
    }
}
