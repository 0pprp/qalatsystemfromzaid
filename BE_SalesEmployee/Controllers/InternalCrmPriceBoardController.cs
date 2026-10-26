using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>CRM → price board read (all provinces or filtered). X-Sales-Gateway-Key only.</summary>
[Route("api/internal/crm/price-board")]
[ApiController]
public sealed class InternalCrmPriceBoardController : ControllerBase
{
    public const string GatewayKeyHeader = InternalCrmSalesRequestsController.GatewayKeyHeader;

    private readonly PriceBoardService _service;
    private readonly IConfiguration _configuration;

    public InternalCrmPriceBoardController(PriceBoardService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? cityValue, CancellationToken ct)
    {
        if (!InternalCrmSalesRequestsController.HasValidGatewayKey(Request, _configuration))
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });

        var rows = await _service.ListAsync(cityValue, ct);
        return Ok(rows.Select(ToDto));
    }

    private static object ToDto(PriceBoardEntry r) => new
    {
        id = r.Id,
        productName = r.ProductName,
        salePrice = r.SalePrice,
        dailyInstallment = r.DailyInstallment,
        downPayment = r.DownPayment,
        targetCityValue = r.TargetCityValue,
        targetCityName = r.TargetCityName,
        createdAtUtc = r.CreatedAtUtc,
        updatedAtUtc = r.UpdatedAtUtc,
        createdByName = r.CreatedByName,
        updatedByName = r.UpdatedByName,
    };
}
