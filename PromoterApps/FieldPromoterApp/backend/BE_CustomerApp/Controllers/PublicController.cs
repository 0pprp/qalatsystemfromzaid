using Microsoft.AspNetCore.Mvc;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Gateway;

namespace BE_CustomerApp.Controllers;

public sealed record BranchDto(int CityValue, string Name);

/// <summary>Endpoints the app calls before sign-in.</summary>
[ApiController]
[Route("api/v1")]
public sealed class PublicController : ControllerBase
{
    private readonly BranchCatalog _branches;
    private readonly AppSettingsService _settings;
    private readonly PublicUrls _urls;
    private readonly AppReleaseService _release;

    public PublicController(BranchCatalog branches, AppSettingsService settings, PublicUrls urls, AppReleaseService release)
    {
        _branches = branches;
        _settings = settings;
        _urls = urls;
        _release = release;
    }

    /// <summary>Company branches: a sale order picks the branch that will handle it.</summary>
    [HttpGet("branches")]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Branches(CancellationToken ct) =>
        Ok((await _branches.GetAllAsync(ct)).Select(b => new BranchDto(b.CityValue, b.Name)));

    /// <summary>
    /// The published APK's version is both the latest and the minimum: an older app shows the blocking update screen
    /// with the download link (see <see cref="AppReleaseService"/>).
    /// </summary>
    [HttpGet("app/config")]
    public async Task<IActionResult> Config(CancellationToken ct)
    {
        var s = await _settings.GetAsync(ct);
        var release = _release.Current();
        var version = release?.Version ?? "0.0.0";
        return Ok(new
        {
            minSupportedVersion = version,
            latestVersion = version,
            s.SupportPhone,
            s.SupportWhatsapp,
            apkUrl = release?.ApkUrl ?? $"{_urls.StoreBaseUrl}/download",
        });
    }
}
