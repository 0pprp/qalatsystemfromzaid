using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Anonymous: apps list Q&amp;A tutorial videos before / without login.</summary>
[AllowAnonymous]
[Route("api/mobile-tutorial-videos")]
[ApiController]
public class MobileTutorialVideosController : ControllerBase
{
    private readonly MobileTutorialVideoService _videos;

    public MobileTutorialVideosController(MobileTutorialVideoService videos)
    {
        _videos = videos;
    }

    [HttpGet("{appKey}")]
    public async Task<IActionResult> ListByApp(string appKey, CancellationToken ct)
    {
        var rows = await _videos.ListPublicByAppAsync(appKey, ct);
        return Ok(rows.Select(r => new
        {
            id = r.Id,
            appKey = r.AppKey,
            questionAr = r.QuestionAr,
            videoUrl = r.VideoUrl,
            sortOrder = r.SortOrder
        }));
    }
}
