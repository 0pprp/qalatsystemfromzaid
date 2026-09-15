using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Gateway SQL implementation over dbo.MobileAppReleases.</summary>
public sealed class SqlMobileUpdateStore : IMobileUpdateStore
{
    private const string Columns = """
        AppKey, LatestVersionName, LatestVersionCode, MinimumSupportedVersionCode, ForceUpdate,
        ApkUrl, Sha256, ReleaseNotes, UpdatedAtUtc
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlMobileUpdateStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<MobileAppRelease?> GetAsync(string appKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return null;
        }

        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<MobileAppRelease>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.MobileAppReleases WHERE AppKey = @appKey;
            """, new { appKey = appKey.Trim() }, cancellationToken: ct));
    }

    public async Task UpsertAsync(MobileAppRelease release, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(release.AppKey))
        {
            throw new ArgumentException("AppKey is required", nameof(release));
        }

        var row = release.Clone();
        row.AppKey = row.AppKey.Trim();
        row.UpdatedAtUtc = row.UpdatedAtUtc == default ? DateTime.UtcNow : row.UpdatedAtUtc;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE dbo.MobileAppReleases AS target
            USING (SELECT @AppKey AS AppKey) AS source ON target.AppKey = source.AppKey
            WHEN MATCHED THEN UPDATE SET
                LatestVersionName = @LatestVersionName,
                LatestVersionCode = @LatestVersionCode,
                MinimumSupportedVersionCode = @MinimumSupportedVersionCode,
                ForceUpdate = @ForceUpdate,
                ApkUrl = @ApkUrl,
                Sha256 = @Sha256,
                ReleaseNotes = @ReleaseNotes,
                UpdatedAtUtc = @UpdatedAtUtc
            WHEN NOT MATCHED THEN INSERT
                (AppKey, LatestVersionName, LatestVersionCode, MinimumSupportedVersionCode, ForceUpdate,
                 ApkUrl, Sha256, ReleaseNotes, UpdatedAtUtc)
                VALUES
                (@AppKey, @LatestVersionName, @LatestVersionCode, @MinimumSupportedVersionCode, @ForceUpdate,
                 @ApkUrl, @Sha256, @ReleaseNotes, @UpdatedAtUtc);
            """, row, cancellationToken: ct));
    }
}
