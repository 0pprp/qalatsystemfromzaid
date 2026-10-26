using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Branch → gateway → CRM status relay. Both hops require X-Sales-Gateway-Key.
/// </summary>
[Route("api/internal/crm/sales-board-status")]
[ApiController]
public sealed class InternalCrmSalesBoardStatusController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalCrmSalesBoardStatusController> _logger;

    public InternalCrmSalesBoardStatusController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<InternalCrmSalesBoardStatusController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public sealed class StatusBody
    {
        public Guid CrmOrderUuid { get; set; }
        public int SalesRequestId { get; set; }
        public string? CityValue { get; set; }
        public string? BoardStatus { get; set; }
        public string? RejectReason { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Relay([FromBody] StatusBody? body, CancellationToken ct)
    {
        if (!InternalCrmSalesRequestsController.HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        body ??= new StatusBody();
        if (body.CrmOrderUuid == Guid.Empty || string.IsNullOrWhiteSpace(body.BoardStatus))
        {
            return BadRequest(new { message = "crmOrderUuid و boardStatus مطلوبان." });
        }

        var crmBase = (_configuration["CrmBridge:BaseUrl"] ?? "").Trim();
        var crmKey = _configuration["CrmBridge:ApiKey"] ?? "";
        if (string.IsNullOrWhiteSpace(crmBase) || string.IsNullOrWhiteSpace(crmKey))
        {
            _logger.LogWarning("CrmBridge not configured; status relay skipped for {Order}", body.CrmOrderUuid);
            return Accepted(new { message = "CrmBridge غير مهيأ؛ تم تجاهل المزامنة." });
        }

        if (!crmBase.EndsWith('/'))
        {
            crmBase += "/";
        }

        var client = _httpClientFactory.CreateClient("crm-bridge");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            crmBase + "api/internal/sales-board/status");
        request.Headers.TryAddWithoutValidation(GatewayKeyHeader, crmKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                crmOrderUuid = body.CrmOrderUuid,
                salesRequestId = body.SalesRequestId,
                cityValue = body.CityValue,
                boardStatus = body.BoardStatus,
                rejectReason = body.RejectReason
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "CRM status relay failed {Status}: {Body}",
                (int)response.StatusCode,
                responseBody.Length > 400 ? responseBody[..400] : responseBody);
            return StatusCode((int)response.StatusCode, new { message = "فشل تحديث حالة الطلب في CRM." });
        }

        return Ok(new { ok = true });
    }
}
