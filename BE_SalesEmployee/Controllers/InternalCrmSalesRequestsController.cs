using System.Text.Json;
using System.Text.Json.Serialization;
using BE_SalesEmployee.Sales.Crm;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// CRM → sales board intake. Authenticated only with X-Sales-Gateway-Key (CRM server secrets).
/// </summary>
[Route("api/internal/crm/sales-requests")]
[ApiController]
public sealed class InternalCrmSalesRequestsController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AdminCitiesService _cities;
    private readonly BranchProxyService _proxy;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalCrmSalesRequestsController> _logger;

    public InternalCrmSalesRequestsController(
        AdminCitiesService cities,
        BranchProxyService proxy,
        IConfiguration configuration,
        ILogger<InternalCrmSalesRequestsController> logger)
    {
        _cities = cities;
        _proxy = proxy;
        _configuration = configuration;
        _logger = logger;
    }

    public sealed class CrmIntakeBody
    {
        public Guid CrmOrderUuid { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerProvince { get; set; }
        public string? CustomerAddress { get; set; }
        public string? Notes { get; set; }
        public string? ProductName { get; set; }
    }

    public static bool HasValidGatewayKey(HttpRequest request, IConfiguration configuration)
    {
        if (!request.Headers.TryGetValue(GatewayKeyHeader, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expected = configuration["InternalApiKey"];
        return !string.IsNullOrWhiteSpace(expected)
               && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    [HttpPost]
    public async Task<IActionResult> Intake([FromBody] CrmIntakeBody? body, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        body ??= new CrmIntakeBody();
        if (body.CrmOrderUuid == Guid.Empty)
        {
            return BadRequest(new { message = "crmOrderUuid مطلوب." });
        }

        var branches = await _cities.GetSalesBranchesAsync(ct);
        var route = CrmProvinceRouter.Resolve(body.CustomerProvince, branches);
        if (!route.Ok || route.City is null)
        {
            return UnprocessableEntity(new { message = route.ErrorMessage ?? "تعذر توجيه المحافظة." });
        }

        var payload = JsonSerializer.Serialize(new
        {
            crmOrderUuid = body.CrmOrderUuid,
            customerName = body.CustomerName,
            customerPhone = body.CustomerPhone,
            customerProvince = body.CustomerProvince,
            customerAddress = body.CustomerAddress,
            notes = body.Notes,
            productName = body.ProductName
        }, JsonOptions);

        using var response = await _proxy.SendManagerAsync(
            route.City.Link,
            "internal/crm/sales-requests",
            HttpMethod.Post,
            payload,
            "CRM",
            ct);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "CRM intake branch {City} failed {Status}: {Body}",
                route.City.Value,
                (int)response.StatusCode,
                responseBody.Length > 400 ? responseBody[..400] : responseBody);
            return StatusCode((int)response.StatusCode, string.IsNullOrWhiteSpace(responseBody)
                ? new { message = "تعذر إنشاء طلب المبيعات في الفرع." }
                : JsonSerializer.Deserialize<object>(responseBody));
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseBody) ? "{}" : responseBody);
        var root = doc.RootElement.Clone();
        return Ok(new
        {
            cityValue = route.City.Value,
            cityName = route.City.Name,
            branch = root
        });
    }
}
