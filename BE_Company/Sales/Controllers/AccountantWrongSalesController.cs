using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>
/// Main accountant WrongSale inbox — same gateway bridge as Branch Manager, scoped to active city.
/// </summary>
[ApiController]
[Route("api/accountant")]
[Authorize(Policy = SalesPolicies.MainAccountant)]
public sealed class AccountantWrongSalesController : ControllerBase
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly SalesIdentityService _identity;
    private readonly IWrongSaleGatewayForwarder _wrongSales;
    private readonly IConfiguration _configuration;

    public AccountantWrongSalesController(
        SalesDevelopmentGuard guard,
        SalesIdentityService identity,
        IWrongSaleGatewayForwarder wrongSales,
        IConfiguration configuration)
    {
        _guard = guard;
        _identity = identity;
        _wrongSales = wrongSales;
        _configuration = configuration;
    }

    private async Task<IActionResult?> GateAsync(CancellationToken ct)
    {
        var check = await _guard.CanRunSalesModuleAsync(ct);
        if (!check.Ok)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = check.Reason });
        }

        // Policy already requires محاسب رئيسي. Do NOT call FromAuthenticatedUser() — accountants
        // are not sales-module identities, and a 401 here logs the FE session out.
        return null;
    }

    private string ResolveCityValue()
    {
        var identity = _identity.FromAuthenticatedUser();
        if (!string.IsNullOrWhiteSpace(identity?.BranchId))
        {
            return identity.BranchId.Trim();
        }

        var configured = _configuration["SalesManagement:BranchId"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        var cs = _configuration.GetConnectionString("DataBaseConnection");
        if (!string.IsNullOrWhiteSpace(cs))
        {
            return new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).InitialCatalog;
        }

        return string.Empty;
    }

    private string ResolveDisplayName()
    {
        var identity = _identity.FromAuthenticatedUser();
        if (!string.IsNullOrWhiteSpace(identity?.EmployeeName))
        {
            return identity.EmployeeName.Trim();
        }

        var claim = User.FindFirst("UserName")?.Value;
        return string.IsNullOrWhiteSpace(claim) ? "محاسب رئيسي" : claim.Trim();
    }

    [HttpGet("wrong-sales")]
    public async Task<IActionResult> ListWrongSales(
        [FromQuery] bool reviewed = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;

        var forwarded = await _wrongSales.ListAsync(new WrongSaleListForwardRequest
        {
            Reviewed = reviewed,
            CityValue = ResolveCityValue(),
            Page = page,
            PageSize = pageSize
        }, ct);

        if (!forwarded.Ok)
        {
            return StatusCode(
                forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                new { message = forwarded.ErrorMessage ?? "تعذر تحميل المبيعات الخاطئة" });
        }

        return Content(forwarded.ResponseBody ?? "{}", "application/json");
    }

    [HttpGet("wrong-sales/{id:guid}")]
    public async Task<IActionResult> GetWrongSale(Guid id, CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        var forwarded = await _wrongSales.GetAsync(id, ct);
        if (!forwarded.Ok)
        {
            return StatusCode(
                forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                new { message = forwarded.ErrorMessage ?? "تعذر تحميل تفاصيل المبيع الخاطئ" });
        }

        return Content(forwarded.ResponseBody ?? "{}", "application/json");
    }

    public sealed class WrongSaleReviewBody
    {
        public string? Note { get; set; }
    }

    [HttpPost("wrong-sales/{id:guid}/review")]
    public async Task<IActionResult> ReviewWrongSale(
        Guid id,
        [FromBody] WrongSaleReviewBody? body,
        CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;

        var note = (body?.Note ?? "").Trim();
        if (note.Length < 3)
        {
            return BadRequest(new { message = "ملاحظة المراجعة إجبارية" });
        }

        var display = ResolveDisplayName();
        var forwarded = await _wrongSales.ReviewAsync(id, new WrongSaleReviewForwardRequest
        {
            Note = note,
            ReviewerUserName = display,
            ReviewerDisplayName = display
        }, ct);

        if (!forwarded.Ok)
        {
            return StatusCode(
                forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                new { message = forwarded.ErrorMessage ?? "تعذر حفظ المراجعة" });
        }

        return Content(forwarded.ResponseBody ?? "{}", "application/json");
    }
}
