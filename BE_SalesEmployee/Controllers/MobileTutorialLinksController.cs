using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Anonymous: apps open the tutorial before / without login.</summary>
[AllowAnonymous]
[Route("api/mobile-tutorial-links")]
[ApiController]
public class MobileTutorialLinksController : ControllerBase
{
    private readonly MobileTutorialLinkService _links;

    public MobileTutorialLinksController(MobileTutorialLinkService links)
    {
        _links = links;
    }

    [HttpGet("{appKey}")]
    public async Task<IActionResult> Get(string appKey, CancellationToken ct)
    {
        var row = await _links.GetPublicAsync(appKey, ct);
        if (row is null)
        {
            return NotFound(new { message = "لا يوجد فيديو تعليمي مفعّل لهذا التطبيق" });
        }

        return Ok(new
        {
            appKey = row.AppKey,
            titleAr = row.TitleAr,
            videoUrl = row.VideoUrl,
            updatedAtUtc = row.UpdatedAtUtc
        });
    }
}
