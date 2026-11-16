using BE_SalesEmployee.Sales.CustomerApp;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// BE_CustomerApp (customer store app backend) → Qalaa branches. Authenticated only with
/// X-Sales-Gateway-Key (same check as <see cref="InternalCrmSalesRequestsController"/>).
/// </summary>
[Route("api/internal/customer-app")]
[ApiController]
public sealed class InternalCustomerAppController : ControllerBase
{
    private readonly CustomerAppGatewayService _service;
    private readonly IConfiguration _configuration;

    public InternalCustomerAppController(CustomerAppGatewayService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    private bool HasValidKey() => InternalCrmSalesRequestsController.HasValidGatewayKey(Request, _configuration);

    private IActionResult KeyRejected() => Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });

    private IActionResult From(CustomerAppGatewayResult result) => StatusCode(result.Status, result.Body);

    [HttpGet("branches")]
    public async Task<IActionResult> Branches(CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return KeyRejected();
        }

        var branches = await _service.GetBranchesAsync(ct);
        return Ok(branches.Select(b => new { cityValue = b.CityValue, name = b.Name }));
    }

    [HttpGet("accounts-by-phone")]
    public async Task<IActionResult> AccountsByPhone([FromQuery] string? phone, CancellationToken ct) =>
        HasValidKey() ? From(await _service.GetAccountsByPhoneAsync(phone, ct)) : KeyRejected();

    [HttpGet("accounts/{cityValue:int}/{customerId:int}")]
    public async Task<IActionResult> AccountDetail(int cityValue, int customerId, [FromQuery] string? phone, CancellationToken ct) =>
        HasValidKey() ? From(await _service.GetAccountDetailAsync(cityValue, customerId, phone, ct)) : KeyRejected();

    [HttpGet("sales/{cityValue:int}/{saleId:int}/progress")]
    public async Task<IActionResult> SaleProgress(int cityValue, int saleId, CancellationToken ct) =>
        HasValidKey() ? From(await _service.GetSaleProgressAsync(cityValue, saleId, ct)) : KeyRejected();

    [HttpPost("sales-requests")]
    public async Task<IActionResult> CreateSalesRequest([FromBody] CustomerAppSalesRequestBody? body, CancellationToken ct) =>
        HasValidKey() ? From(await _service.CreateSalesRequestAsync(body, ct)) : KeyRejected();

    [HttpPost("sales-requests/status")]
    public async Task<IActionResult> Status([FromBody] CustomerAppStatusBody? body, CancellationToken ct) =>
        HasValidKey() ? From(await _service.GetStatusesAsync(body, ct)) : KeyRejected();
}
