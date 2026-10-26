using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>متابعة المبيعات — كروت موظفي المبيعات للمدير/المراقب.</summary>
[Authorize(Policy = SalesPolicies.SalesManager)]
[Route("api/sales-manager/director-sales-followup")]
[ApiController]
public sealed class DirectorSalesFollowupController : ControllerBase
{
    private readonly IDirectorSalesFollowupService _svc;
    private readonly SalesIdentityService _identity;

    public DirectorSalesFollowupController(
        IDirectorSalesFollowupService svc,
        SalesIdentityService identity)
    {
        _svc = svc;
        _identity = identity;
    }

    [HttpGet("employees")]
    public async Task<IActionResult> ListEmployees(CancellationToken ct)
    {
        var items = await _svc.ListEmployeesAsync(ct);
        return Ok(new { items, marker = DirectorSalesFollowupRules.DeployMarker });
    }

    [HttpGet("employees/{employeeId:int}")]
    public async Task<IActionResult> Detail(int employeeId, CancellationToken ct)
    {
        var detail = await _svc.GetDetailAsync(employeeId, ct);
        if (detail is null) return NotFound(new { message = "الموظف غير موجود." });
        return Ok(detail);
    }

    [HttpGet("employees/{employeeId:int}/customers")]
    public async Task<IActionResult> Customers(int employeeId, CancellationToken ct)
    {
        if (employeeId <= 0) return BadRequest(new { message = "معرّف الموظف مطلوب." });
        var rows = await _svc.GetEmployeeCustomersAsync(employeeId, ct);
        return Ok(rows);
    }

    [HttpPost("employees/{employeeId:int}/manager-rating")]
    public async Task<IActionResult> UpsertManagerRating(
        int employeeId,
        [FromBody] DirectorSalesFollowupUpsertManagerRatingRequest? body,
        CancellationToken ct)
    {
        body ??= new DirectorSalesFollowupUpsertManagerRatingRequest();
        var actor = _identity.FromAuthenticatedUser();
        var (ok, error, row) = await _svc.UpsertManagerRatingAsync(
            employeeId,
            actor?.EmployeeName,
            body.Score,
            body.Note,
            ct);
        if (!ok) return BadRequest(new { message = error ?? "تعذر حفظ التقييم." });
        return Ok(row);
    }
}
