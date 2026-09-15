using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Anonymous on purpose: the app checks for updates before the user can sign in.</summary>
[AllowAnonymous]
[Route("api/mobile-updates")]
[ApiController]
public class MobileUpdatesController : ControllerBase
{
    private readonly MobileUpdateService _updates;

    public MobileUpdatesController(MobileUpdateService updates)
    {
        _updates = updates;
    }

    [HttpGet("{appKey}")]
    public async Task<IActionResult> Get(string appKey, [FromQuery] int? currentVersionCode, CancellationToken ct)
    {
        var check = await _updates.EvaluateAsync(appKey, currentVersionCode ?? 0, ct);
        if (check is null)
        {
            return NotFound(new { message = "التطبيق غير معروف" });
        }

        var release = check.Release;
        return Ok(new
        {
            appKey = release.AppKey,
            latestVersionName = release.LatestVersionName,
            latestVersionCode = release.LatestVersionCode,
            minimumSupportedVersionCode = release.MinimumSupportedVersionCode,
            forceUpdate = release.ForceUpdate,
            apkUrl = release.ApkUrl,
            sha256 = release.Sha256,
            releaseNotes = release.ReleaseNotes,
            updatedAtUtc = release.UpdatedAtUtc,
            updateKind = check.Kind.ToString(),
            updateAvailable = check.UpdateAvailable,
            mandatory = check.Mandatory
        });
    }
}
