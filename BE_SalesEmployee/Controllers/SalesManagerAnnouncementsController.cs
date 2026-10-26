using System.Security.Claims;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.Director)]
[Route("api/sales-manager/announcements")]
[ApiController]
[RequestSizeLimit(12 * 1024 * 1024)]
public class SalesManagerAnnouncementsController : ControllerBase
{
    private readonly AppAnnouncementService _svc;

    public SalesManagerAnnouncementsController(AppAnnouncementService svc)
    {
        _svc = svc;
    }

    public sealed class UpsertAnnouncementRequest
    {
        public string? AppKey { get; set; }
        public string? ContentType { get; set; }
        public string? TextBody { get; set; }
        public string? ImageFileName { get; set; }
        public string? ZoomImageFileName { get; set; }
        public bool HasLink { get; set; }
        public string? LinkUrl { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    [HttpGet("apps")]
    public async Task<IActionResult> Apps(CancellationToken ct) =>
        Ok(await _svc.ListAppsAsync(ct));

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _svc.ListForManagerAsync(ct);
        return Ok(rows.Select(ToDto));
    }

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile? file, CancellationToken ct)
    {
        file ??= Request.Form.Files.FirstOrDefault(f => f.Length > 0);
        if (file == null || file.Length <= 0)
            return BadRequest(new { message = "الصورة مطلوبة." });

        try
        {
            var (ok, error, fileName) = await _svc.SaveImageAsync(file, ct);
            if (!ok || string.IsNullOrWhiteSpace(fileName))
                return BadRequest(new { message = error ?? "تعذر رفع الصورة" });

            return Ok(new
            {
                imageFileName = fileName,
                imageUrl = $"/api/mobile-announcements/media/{fileName}"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "تعذر حفظ ملف الصورة على السيرفر." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertAnnouncementRequest? body, CancellationToken ct)
    {
        body ??= new UpsertAnnouncementRequest();
        var (ok, error, row) = await _svc.CreateAsync(
            body.AppKey,
            body.ContentType,
            body.TextBody,
            body.ImageFileName,
            body.ZoomImageFileName,
            body.HasLink,
            body.LinkUrl,
            body.IsEnabled,
            ActorName(),
            ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر إضافة الإعلان" });
        return Ok(ToDto(row));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertAnnouncementRequest? body, CancellationToken ct)
    {
        body ??= new UpsertAnnouncementRequest();
        var (ok, error, row) = await _svc.UpdateAsync(
            id,
            body.AppKey,
            body.ContentType,
            body.TextBody,
            body.ImageFileName,
            body.ZoomImageFileName,
            body.HasLink,
            body.LinkUrl,
            body.IsEnabled,
            ActorName(),
            ct);
        if (!ok || row is null)
            return BadRequest(new { message = error ?? "تعذر تعديل الإعلان" });
        return Ok(ToDto(row));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var (ok, error) = await _svc.DeleteAsync(id, ct);
        if (!ok)
            return BadRequest(new { message = error ?? "تعذر حذف الإعلان" });
        return Ok(new { message = "تم الحذف" });
    }

    private string ActorName() =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("employeeName")
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? "مدير";

    private static object ToDto(AppAnnouncement r) => new
    {
        id = r.Id,
        appKey = r.AppKey,
        contentType = r.ContentType,
        textBody = r.TextBody,
        imageFileName = r.ImageFileName,
        imageUrl = string.IsNullOrWhiteSpace(r.ImageFileName)
            ? null
            : $"/api/mobile-announcements/media/{r.ImageFileName}",
        zoomImageFileName = r.ZoomImageFileName,
        zoomImageUrl = string.IsNullOrWhiteSpace(r.ZoomImageFileName)
            ? (string.IsNullOrWhiteSpace(r.ImageFileName)
                ? null
                : $"/api/mobile-announcements/media/{r.ImageFileName}")
            : $"/api/mobile-announcements/media/{r.ZoomImageFileName}",
        hasLink = r.HasLink,
        linkUrl = r.LinkUrl,
        isEnabled = r.IsEnabled,
        sortOrder = r.SortOrder,
        createdAtUtc = r.CreatedAtUtc,
        updatedAtUtc = r.UpdatedAtUtc,
        updatedBy = r.UpdatedBy
    };
}
