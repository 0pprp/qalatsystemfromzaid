using System.Security.Claims;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.SalesManager)]
[Route("api/sales-manager/tutorial-links")]
[ApiController]
public class SalesManagerTutorialLinksController : ControllerBase
{
    private readonly MobileTutorialLinkService _links;

    public SalesManagerTutorialLinksController(MobileTutorialLinkService links)
    {
        _links = links;
    }

    public sealed class UpsertTutorialLinkRequest
    {
        public string? AppKey { get; set; }
        public string? TitleAr { get; set; }
        public string? VideoUrl { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _links.ListForManagerAsync(ct);
        return Ok(rows.Select(r => new
        {
            appKey = r.AppKey,
            titleAr = r.TitleAr,
            videoUrl = r.VideoUrl,
            isEnabled = r.IsEnabled,
            updatedAtUtc = r.UpdatedAtUtc,
            updatedBy = r.UpdatedBy
        }));
    }

    [HttpPut("{appKey}")]
    public Task<IActionResult> Put(string appKey, [FromBody] UpsertTutorialLinkRequest body, CancellationToken ct) =>
        Upsert(appKey, body, ct);

    [HttpPost]
    public Task<IActionResult> Post([FromBody] UpsertTutorialLinkRequest body, CancellationToken ct) =>
        Upsert(body.AppKey ?? string.Empty, body, ct);

    private async Task<IActionResult> Upsert(string appKey, UpsertTutorialLinkRequest? body, CancellationToken ct)
    {
        body ??= new UpsertTutorialLinkRequest();
        var actor = User.FindFirstValue(ClaimTypes.Name)
                    ?? User.FindFirstValue("employeeName")
                    ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? "مدير مبيعات";
        var (ok, error, row) = await _links.UpsertAsync(
            appKey,
            body.TitleAr,
            body.VideoUrl,
            body.IsEnabled,
            actor,
            ct);
        if (!ok || row is null)
        {
            return BadRequest(new { message = error ?? "تعذر حفظ الرابط" });
        }

        return Ok(new
        {
            appKey = row.AppKey,
            titleAr = row.TitleAr,
            videoUrl = row.VideoUrl,
            isEnabled = row.IsEnabled,
            updatedAtUtc = row.UpdatedAtUtc,
            updatedBy = row.UpdatedBy
        });
    }
}
