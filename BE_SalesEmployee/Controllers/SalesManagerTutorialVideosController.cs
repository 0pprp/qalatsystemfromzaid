using System.Security.Claims;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Director-only management of educational Q&amp;A videos.</summary>
[Authorize(Policy = SalesPolicies.Director)]
[Route("api/sales-manager/tutorial-videos")]
[ApiController]
public class SalesManagerTutorialVideosController : ControllerBase
{
    private readonly MobileTutorialVideoService _videos;

    public SalesManagerTutorialVideosController(MobileTutorialVideoService videos)
    {
        _videos = videos;
    }

    public sealed class UpsertTutorialVideoRequest
    {
        public string? AppKey { get; set; }
        public string? QuestionAr { get; set; }
        public string? VideoUrl { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    [HttpGet("apps")]
    public async Task<IActionResult> Apps(CancellationToken ct)
    {
        return Ok(await _videos.ListAppsAsync(ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _videos.ListForManagerAsync(ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertTutorialVideoRequest? body, CancellationToken ct)
    {
        body ??= new UpsertTutorialVideoRequest();
        var (ok, error, row) = await _videos.CreateAsync(
            body.AppKey,
            body.QuestionAr,
            body.VideoUrl,
            body.IsEnabled,
            ActorName(),
            ct);
        if (!ok || row is null)
        {
            return BadRequest(new { message = error ?? "تعذر إضافة الفيديو" });
        }

        return Ok(ToDto(row));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertTutorialVideoRequest? body, CancellationToken ct)
    {
        body ??= new UpsertTutorialVideoRequest();
        var (ok, error, row) = await _videos.UpdateAsync(
            id,
            body.AppKey,
            body.QuestionAr,
            body.VideoUrl,
            body.IsEnabled,
            ActorName(),
            ct);
        if (!ok || row is null)
        {
            return BadRequest(new { message = error ?? "تعذر تعديل الفيديو" });
        }

        return Ok(ToDto(row));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await _videos.DeleteAsync(id, ct);
        if (!ok)
        {
            return BadRequest(new { message = error ?? "تعذر حذف الفيديو" });
        }

        return Ok(new { message = "تم الحذف" });
    }

    private string ActorName() =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("employeeName")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "مدير";

    private static object ToDto(BE_SalesEmployee.DelegatedManager.Models.MobileAppTutorialVideo r) => new
    {
        id = r.Id,
        appKey = r.AppKey,
        questionAr = r.QuestionAr,
        videoUrl = r.VideoUrl,
        isEnabled = r.IsEnabled,
        sortOrder = r.SortOrder,
        createdAtUtc = r.CreatedAtUtc,
        updatedAtUtc = r.UpdatedAtUtc,
        updatedBy = r.UpdatedBy
    };
}
