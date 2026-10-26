using System.Security.Claims;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.Director)]
[Route("api/sales-manager/committee-tasks")]
[ApiController]
public class SalesManagerCommitteeTasksController : ControllerBase
{
    private readonly AppCommitteeTaskService _svc;

    public SalesManagerCommitteeTasksController(AppCommitteeTaskService svc)
    {
        _svc = svc;
    }

    public sealed class UpsertCommitteeTaskRequest
    {
        public string? Body { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _svc.ListForManagerAsync(ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertCommitteeTaskRequest? body, CancellationToken ct)
    {
        body ??= new UpsertCommitteeTaskRequest();
        var (ok, error, row) = await _svc.CreateAsync(body.Body, body.IsEnabled, ActorName(), ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر إضافة المهمة" });
        return Ok(ToDto(row));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertCommitteeTaskRequest? body, CancellationToken ct)
    {
        body ??= new UpsertCommitteeTaskRequest();
        var (ok, error, row) = await _svc.UpdateAsync(id, body.Body, body.IsEnabled, ActorName(), ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر تعديل المهمة" });
        return Ok(ToDto(row));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await _svc.DeleteAsync(id, ct);
        if (!ok)
            return BadRequest(new { message = error ?? "تعذر حذف المهمة" });
        return Ok(new { message = "تم الحذف" });
    }

    private string ActorName() =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("employeeName")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "مدير";

    private static object ToDto(AppCommitteeTask r) => new
    {
        id = r.Id,
        body = r.Body,
        isEnabled = r.IsEnabled,
        sortOrder = r.SortOrder,
        createdAtUtc = r.CreatedAtUtc,
        updatedAtUtc = r.UpdatedAtUtc,
        updatedBy = r.UpdatedBy
    };
}
