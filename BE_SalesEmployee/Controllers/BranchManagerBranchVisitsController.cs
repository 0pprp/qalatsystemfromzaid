using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.BranchManager)]
[Route("api/branch-manager/branch-visits")]
[ApiController]
public sealed class BranchManagerBranchVisitsController : ControllerBase
{
    private readonly BmBranchVisitService _svc;

    public BranchManagerBranchVisitsController(BmBranchVisitService svc) => _svc = svc;

    public sealed class OpenBody
    {
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
    }

    public sealed class RatingItem
    {
        public int DelegateId { get; set; }
        public string? DelegateName { get; set; }
        public int Score { get; set; }
    }

    public sealed class RatingsBody
    {
        public List<RatingItem>? Ratings { get; set; }
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active(CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });
        var visit = await _svc.GetOpenForUserAsync(actor.Value.UserId, ct);
        if (visit is null) return Ok(null);
        return Ok(ToDto(visit, []));
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenBody? body, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });
        body ??= new OpenBody();
        var (ok, error, visit, tasks) = await _svc.OpenAsync(
            actor.Value.UserId,
            actor.Value.UserName,
            actor.Value.DisplayName,
            body.CityValue ?? "",
            body.CityName,
            ct);
        if (!ok || visit is null)
            return BadRequest(new { message = error ?? "تعذر فتح الزيارة." });
        return Ok(ToDto(visit, tasks));
    }

    [HttpPut("{id:guid}/delegate-ratings")]
    public async Task<IActionResult> Ratings(Guid id, [FromBody] RatingsBody? body, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });
        body ??= new RatingsBody();
        var items = (body.Ratings ?? [])
            .Select(r => (r.DelegateId, r.DelegateName ?? "", r.Score))
            .ToList();
        var (ok, error, visit) = await _svc.UpsertDelegateRatingsAsync(
            id, actor.Value.UserId, actor.Value.DisplayName, items, ct);
        if (!ok || visit is null)
            return BadRequest(new { message = error ?? "تعذر حفظ التقييمات." });
        return Ok(ToDto(visit, []));
    }

    [HttpPost("{id:guid}/depart")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Depart(Guid id, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });

        var notes = Request.Form["notes"].ToString();
        var files = Request.Form.Files.ToList();
        var requiredRaw = Request.Form["requiredDelegateIds"].ToString();
        var requiredIds = requiredRaw
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .Distinct()
            .ToList();
        var (ok, error, visit) = await _svc.DepartAsync(
            id, actor.Value.UserId, notes, files, ct, requiredIds);
        if (!ok || visit is null)
            return BadRequest(new { message = error ?? "تعذر مغادرة الفرع." });
        return Ok(ToDto(visit, []));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null) return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });
        var visit = await _svc.GetAsync(id, ct);
        if (visit is null) return NotFound(new { message = "الزيارة غير موجودة." });
        return Ok(ToDto(visit, []));
    }

    [HttpGet("{id:guid}/attachments/{fileId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid fileId, CancellationToken ct)
    {
        var (ok, error, full, contentType, fileName) = await _svc.DownloadAttachmentAsync(id, fileId, ct);
        if (!ok || string.IsNullOrWhiteSpace(full))
            return NotFound(new { message = error ?? "الملف غير موجود." });
        return PhysicalFile(full, contentType ?? "application/octet-stream", fileName ?? Path.GetFileName(full));
    }

    private (Guid UserId, string UserName, string DisplayName)? Actor()
    {
        var gw = TokenService.FromPrincipal(User);
        if (!Guid.TryParse((gw.UserID ?? "").Trim(), out var id) || id == Guid.Empty)
            return null;
        var display = string.IsNullOrWhiteSpace(gw.UserName) ? id.ToString("D") : gw.UserName.Trim();
        return (id, display, display);
    }

    private static object ToDto(DelegatedManager.Models.BmBranchVisit v, IReadOnlyList<object> assignedTasks) => new
    {
        id = v.Id,
        cityValue = v.CityValue,
        cityName = v.CityName,
        openedByUserId = v.OpenedByUserId,
        openedByUserName = v.OpenedByUserName,
        openedByDisplayName = v.OpenedByDisplayName,
        arrivedAtUtc = v.ArrivedAtUtc,
        departedAtUtc = v.DepartedAtUtc,
        status = v.Status,
        notes = v.Notes,
        createdAtUtc = v.CreatedAtUtc,
        updatedAtUtc = v.UpdatedAtUtc,
        members = v.Members.Select(m => new { userId = m.UserId, displayName = m.DisplayName }),
        memberCount = v.Members.Count,
        delegateRatings = v.DelegateRatings.Select(r => new
        {
            delegateId = r.DelegateId,
            delegateName = r.DelegateName,
            score = r.Score,
            ratedByDisplayName = r.RatedByDisplayName,
            ratedAtUtc = r.RatedAtUtc,
        }),
        attachments = v.Attachments.Select(a => new
        {
            id = a.Id,
            fileName = a.FileName,
            contentType = a.ContentType,
            sizeBytes = a.SizeBytes,
            uploadedAtUtc = a.UploadedAtUtc,
        }),
        assignedTasks,
        fixedTasks = new[]
        {
            new { key = "rate_delegates", title = "تقييم كل المندوبين (0–10)" },
            new { key = "assigned_tasks", title = "المهام المكلَّفة في المحافظة" },
        },
    };
}
