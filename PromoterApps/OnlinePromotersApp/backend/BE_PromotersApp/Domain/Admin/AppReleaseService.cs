using System.Text.Json;

namespace BE_PromotersApp.Domain.Admin;

/// <summary>The app version that is published for download, and its link.</summary>
public sealed record AppRelease(string Version, string ApkUrl);

/// <summary>
/// The published APK decides the app version: publishing (deploy/publish-agents-apk.sh) uploads the APK and writes
/// <c>release.json</c> (<c>{"version":"1.4.6","apkUrl":"http://…"}</c>) next to it; the file is set by
/// <c>Updates:ReleaseFile</c>. That version is both the latest and the minimum, so every older app must update.
/// Without the file nothing is forced. Re-read when the file changes.
/// </summary>
public sealed class AppReleaseService
{
    private readonly string? _file;
    private readonly ILogger<AppReleaseService> _logger;
    private readonly object _lock = new();
    private DateTime _readStamp;
    private AppRelease? _cached;

    public AppReleaseService(IConfiguration config, ILogger<AppReleaseService> logger)
    {
        _file = config["Updates:ReleaseFile"] is { Length: > 0 } f ? f : null;
        _logger = logger;
    }

    public AppRelease? Current()
    {
        if (_file is null) return null;
        try
        {
            var info = new FileInfo(_file);
            if (!info.Exists) return null;
            var stamp = info.LastWriteTimeUtc;
            lock (_lock)
            {
                if (_cached is not null && stamp == _readStamp) return _cached;
                using var doc = JsonDocument.Parse(File.ReadAllText(_file));
                var root = doc.RootElement;
                var version = root.TryGetProperty("version", out var v) ? v.GetString()?.Trim() : null;
                var url = root.TryGetProperty("apkUrl", out var u) ? u.GetString()?.Trim() : null;
                if (version is null || !Version.TryParse(version, out _) || url is null || !Uri.TryCreate(url, UriKind.Absolute, out _))
                {
                    _logger.LogWarning("Ignoring {File}: needs a version like 1.4.6 and an absolute apkUrl", _file);
                    return null;
                }
                _cached = new AppRelease(version, url);
                _readStamp = stamp;
                return _cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the app release file {File}", _file);
            return null;
        }
    }
}
