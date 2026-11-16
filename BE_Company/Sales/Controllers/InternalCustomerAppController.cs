using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>
/// Gateway-only customer store app endpoints (X-Sales-Gateway-Key). Never exposed to browsers:
/// BE_CustomerApp → BE_SalesEmployee api/internal/customer-app/* → here.
/// </summary>
[ApiController]
[Route("api/internal/customer-app")]
public sealed class InternalCustomerAppController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly ICustomerAppService _service;
    private readonly IConfiguration _configuration;

    public InternalCustomerAppController(ICustomerAppService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    // Same key rule as BE_Company.Controllers.InternalCrmSalesRequestsController.
    private bool HasValidKey()
    {
        if (!Request.Headers.TryGetValue(GatewayKeyHeader, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expected = _configuration["SalesEmployee:GatewayKey"]
                       ?? _configuration["InternalApiKey"];
        return !string.IsNullOrWhiteSpace(expected)
               && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    [HttpGet("accounts-by-phone")]
    public Task<IActionResult> AccountsByPhone([FromQuery] string? phone, CancellationToken ct) =>
        Run(async () => Ok(await _service.GetAccountsByPhoneAsync(phone, ct)));

    [HttpGet("accounts/{customerId:int}")]
    public Task<IActionResult> AccountDetail(int customerId, [FromQuery] string? phone, CancellationToken ct) =>
        Run(async () =>
        {
            var detail = await _service.GetAccountDetailAsync(customerId, phone, ct);
            return detail is null
                ? NotFound(new { message = "الحساب غير موجود." })
                : Ok(detail);
        });

    /// <summary>Payment progress of a sale (SalesRequests.ConvertedToSaleId). 404 until the sale is posted.</summary>
    [HttpGet("sales/{saleId:int}/progress")]
    public Task<IActionResult> SaleProgress(int saleId, CancellationToken ct) =>
        Run(async () =>
        {
            var progress = await _service.GetSaleProgressAsync(saleId, ct);
            return progress is null
                ? NotFound(new { message = "المبيعة غير مرحّلة بعد." })
                : Ok(progress);
        });

    [HttpPost("sales-requests/status")]
    public Task<IActionResult> Status([FromBody] CustomerAppStatusRequestDTO? body, CancellationToken ct) =>
        Run(async () => Ok(await _service.GetStatusesAsync(body, ct)));

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        try
        {
            return await action();
        }
        catch (SalesCompleteException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
