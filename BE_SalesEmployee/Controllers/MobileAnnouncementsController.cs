using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[AllowAnonymous]
[Route("api/mobile-announcements")]
[ApiController]
public class MobileAnnouncementsController : ControllerBase
{
    private readonly AppAnnouncementService _svc;

    public MobileAnnouncementsController(AppAnnouncementService svc)
    {
        _svc = svc;
    }

    [HttpGet("{appKey}")]
    public async Task<IActionResult> ListByApp(string appKey, CancellationToken ct)
    {
        var rows = await _svc.ListPublicByAppAsync(appKey, ct);
        return Ok(rows.Select(ToPublicDto));
    }

    [HttpGet("media/{fileName}")]
    public IActionResult Media(string fileName)
    {
        var (ok, fullPath, contentType) = _svc.ResolveImage(fileName);
        if (!ok || fullPath is null || contentType is null)
            return NotFound();
        return PhysicalFile(fullPath, contentType);
    }

    private static object ToPublicDto(AppAnnouncement r)
    {
        var banner = string.IsNullOrWhiteSpace(r.ImageFileName)
            ? null
            : $"/api/mobile-announcements/media/{r.ImageFileName}";
        var zoomName = string.IsNullOrWhiteSpace(r.ZoomImageFileName)
            ? r.ImageFileName
            : r.ZoomImageFileName;
        var zoom = string.IsNullOrWhiteSpace(zoomName)
            ? null
            : $"/api/mobile-announcements/media/{zoomName}";

        return new
        {
            id = r.Id,
            appKey = r.AppKey,
            contentType = r.ContentType,
            textBody = r.TextBody,
            imageUrl = banner,
            zoomImageUrl = zoom,
            hasLink = r.HasLink,
            linkUrl = r.HasLink ? r.LinkUrl : null,
            sortOrder = r.SortOrder
        };
    }
}
