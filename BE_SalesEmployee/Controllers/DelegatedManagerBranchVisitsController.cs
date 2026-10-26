using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.DelegatedManager)]
[Route("api/delegated-manager/branch-visits")]
[ApiController]
public sealed class DelegatedManagerBranchVisitsController : ControllerBase
{
    private readonly BmBranchVisitService _svc;

    public DelegatedManagerBranchVisitsController(BmBranchVisitService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? cityValue,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var rows = await _svc.ListAsync(cityValue, fromUtc, toUtc, take, ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var visit = await _svc.GetAsync(id, ct);
        return visit is null
            ? NotFound(new { message = "الزيارة غير موجودة." })
            : Ok(ToDto(visit));
    }

    [HttpGet("{id:guid}/attachments/{fileId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid fileId, CancellationToken ct)
    {
        var (ok, error, full, contentType, fileName) = await _svc.DownloadAttachmentAsync(id, fileId, ct);
        if (!ok || string.IsNullOrWhiteSpace(full))
            return NotFound(new { message = error ?? "الملف غير موجود." });
        return PhysicalFile(full, contentType ?? "application/octet-stream", fileName ?? Path.GetFileName(full));
    }

    private static object ToDto(DelegatedManager.Models.BmBranchVisit v) => new
    {
        id = v.Id,
        cityValue = v.CityValue,
        cityName = v.CityName,
        openedByDisplayName = v.OpenedByDisplayName,
        arrivedAtUtc = v.ArrivedAtUtc,
        departedAtUtc = v.DepartedAtUtc,
        status = v.Status,
        notes = v.Notes,
        memberCount = v.Members.Count,
        members = v.Members.Select(m => new { displayName = m.DisplayName }),
        delegateRatings = v.DelegateRatings.Select(r => new
        {
            delegateId = r.DelegateId,
            delegateName = r.DelegateName,
            score = r.Score,
        }),
        attachments = v.Attachments.Select(a => new
        {
            id = a.Id,
            fileName = a.FileName,
            sizeBytes = a.SizeBytes,
        }),
    };
}
