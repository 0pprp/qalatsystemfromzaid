using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers;

/// <summary>
/// Gateway-only CRM sales-request intake (X-Sales-Gateway-Key). Never exposed to browsers.
/// </summary>
[Route("api/internal/crm/sales-requests")]
[ApiController]
public sealed class InternalCrmSalesRequestsController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly ICrmSalesRequestIntakeService _intake;
    private readonly IConfiguration _configuration;

    public InternalCrmSalesRequestsController(
        ICrmSalesRequestIntakeService intake,
        IConfiguration configuration)
    {
        _intake = intake;
        _configuration = configuration;
    }

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

    [HttpPost]
    public async Task<IActionResult> Intake([FromBody] CrmSalesRequestIntakeRequest? body, CancellationToken ct)
    {
        if (!HasValidKey())
        {
            return Unauthorized(new { message = "مفتاح البوابة غير صالح" });
        }

        try
        {
            var result = await _intake.IntakeAsync(body ?? new CrmSalesRequestIntakeRequest(), ct);
            return Ok(result);
        }
        catch (SalesCompleteException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
