using System.Collections.Concurrent;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Default store so demo and tests run without gateway SQL. Seeds the same row as the migration.</summary>
public sealed class InMemoryMobileUpdateStore : IMobileUpdateStore
{
    public const string DelegatedManagerAppKey = "delegated-manager";
    public const string DelegateAppKey = "delegate";

    private readonly ConcurrentDictionary<string, MobileAppRelease> _rows =
        new(StringComparer.OrdinalIgnoreCase);

    public InMemoryMobileUpdateStore()
    {
        _rows[DelegatedManagerAppKey] = Seed(DelegatedManagerAppKey, "delegated-manager.apk");
        _rows[DelegateAppKey] = Seed(DelegateAppKey, "delegate.apk");
    }

    private static MobileAppRelease Seed(string appKey, string apkFileName) => new()
    {
        AppKey = appKey,
        LatestVersionName = "1.0.0",
        LatestVersionCode = 1,
        MinimumSupportedVersionCode = 1,
        ForceUpdate = false,
        ApkUrl = $"https://example.invalid/{apkFileName}",
        Sha256 = new string('0', 64),
        ReleaseNotes = "إصدار أولي",
        UpdatedAtUtc = DateTime.UtcNow
    };

    public Task<MobileAppRelease?> GetAsync(string appKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return Task.FromResult<MobileAppRelease?>(null);
        }

        return Task.FromResult(_rows.TryGetValue(appKey.Trim(), out var row) ? row.Clone() : null);
    }

    public Task UpsertAsync(MobileAppRelease release, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(release.AppKey))
        {
            throw new ArgumentException("AppKey is required", nameof(release));
        }

        var row = release.Clone();
        row.AppKey = row.AppKey.Trim();
        if (row.UpdatedAtUtc == default)
        {
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        _rows[row.AppKey] = row;
        return Task.CompletedTask;
    }
}
