using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BE_Company.Sales.Services
{
    public sealed class CustomerWaiverCreateForwardRequest
    {
        public string? CityValue { get; init; }
        public string? CityName { get; init; }
        public int CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public string? CustomerPhone { get; init; }
        public string? Note { get; init; }
        public string? RequesterUserId { get; init; }
        public string? RequesterUserName { get; init; }
        public string? RequesterDisplayName { get; init; }
        public string? Role { get; init; }
    }

    public sealed class CustomerWaiverListForwardRequest
    {
        public string? Status { get; init; }
        public string? CityValue { get; init; }
        public int? Page { get; init; }
        public int? PageSize { get; init; }
        public string? RequesterUserName { get; init; }
    }

    public sealed class CustomerWaiverGatewayForwardResult
    {
        public bool Ok { get; init; }
        public int FailureStatusCode { get; init; }
        public string? ErrorMessage { get; init; }
        public string? ResponseBody { get; init; }

        public static CustomerWaiverGatewayForwardResult Success(string? body = null) =>
            new() { Ok = true, ResponseBody = body };

        public static CustomerWaiverGatewayForwardResult Failure(int status, string message, string? body = null) =>
            new() { Ok = false, FailureStatusCode = status, ErrorMessage = message, ResponseBody = body };
    }

    public interface ICustomerWaiverGatewayForwarder
    {
        Task<CustomerWaiverGatewayForwardResult> CreateAsync(
            CustomerWaiverCreateForwardRequest request,
            CancellationToken ct = default);

        Task<CustomerWaiverGatewayForwardResult> ListAsync(
            CustomerWaiverListForwardRequest request,
            CancellationToken ct = default);
    }

    public sealed class CustomerWaiverGatewayForwarder : ICustomerWaiverGatewayForwarder
    {
        public const string GatewayKeyHeader = "X-Sales-Gateway-Key";
        public const string RelativePath = "internal/waivers";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _http;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomerWaiverGatewayForwarder> _logger;

        public CustomerWaiverGatewayForwarder(
            HttpClient http,
            IConfiguration configuration,
            ILogger<CustomerWaiverGatewayForwarder> logger)
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

        public Task<CustomerWaiverGatewayForwardResult> CreateAsync(
            CustomerWaiverCreateForwardRequest request,
            CancellationToken ct = default) =>
            SendAsync(HttpMethod.Post, RelativePath, request, ct);

        public Task<CustomerWaiverGatewayForwardResult> ListAsync(
            CustomerWaiverListForwardRequest request,
            CancellationToken ct = default)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.Status))
                query.Add($"status={Uri.EscapeDataString(request.Status.Trim())}");
            if (!string.IsNullOrWhiteSpace(request.CityValue))
                query.Add($"cityValue={Uri.EscapeDataString(request.CityValue.Trim())}");
            if (request.Page is int page)
                query.Add($"page={page}");
            if (request.PageSize is int pageSize)
                query.Add($"pageSize={pageSize}");
            if (!string.IsNullOrWhiteSpace(request.RequesterUserName))
                query.Add($"requesterUserName={Uri.EscapeDataString(request.RequesterUserName.Trim())}");

            var path = query.Count == 0 ? RelativePath : $"{RelativePath}?{string.Join("&", query)}";
            return SendAsync(HttpMethod.Get, path, body: null, ct);
        }

        private async Task<CustomerWaiverGatewayForwardResult> SendAsync(
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
                _logger.LogWarning("SalesGateway BaseUrl or key missing; cannot forward waiver.");
                return CustomerWaiverGatewayForwardResult.Failure(
                    StatusCodes.Status503ServiceUnavailable,
                    "خدمة طلبات التنازل غير مهيأة على هذا الفرع");
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
                    return CustomerWaiverGatewayForwardResult.Success(responseBody);
                }

                var status = (int)response.StatusCode;
                if (status == StatusCodes.Status401Unauthorized)
                {
                    status = StatusCodes.Status502BadGateway;
                }

                return CustomerWaiverGatewayForwardResult.Failure(
                    status,
                    TryMessage(responseBody) ?? "تعذر إرسال طلب التنازل",
                    responseBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Waiver forward threw");
                return CustomerWaiverGatewayForwardResult.Failure(
                    StatusCodes.Status502BadGateway,
                    "تعذر الاتصال بخدمة طلبات التنازل");
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
