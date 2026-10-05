using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Gateway SQL for dbo.MobileAppTutorialLinks — one tutorial URL per mobile appKey.</summary>
public sealed class SqlMobileTutorialLinkStore : IMobileTutorialLinkStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private const string Columns = """
        AppKey, TitleAr, VideoUrl, IsEnabled, UpdatedAtUtc, UpdatedBy
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlMobileTutorialLinkStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady)
        {
            return;
        }

        lock (SchemaGate)
        {
            if (_schemaReady)
            {
                return;
            }
        }

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.MobileAppTutorialLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MobileAppTutorialLinks (
        AppKey NVARCHAR(64) NOT NULL CONSTRAINT PK_MobileAppTutorialLinks PRIMARY KEY,
        TitleAr NVARCHAR(200) NOT NULL,
        VideoUrl NVARCHAR(1024) NOT NULL CONSTRAINT DF_MATL_VideoUrl DEFAULT (N''),
        IsEnabled BIT NOT NULL CONSTRAINT DF_MATL_IsEnabled DEFAULT (1),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_MATL_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy NVARCHAR(128) NULL
    );
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<IReadOnlyList<MobileAppTutorialLink>> ListAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<MobileAppTutorialLink>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.MobileAppTutorialLinks
            ORDER BY TitleAr ASC, AppKey ASC;
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<MobileAppTutorialLink?> GetAsync(string appKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return null;
        }

        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<MobileAppTutorialLink>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.MobileAppTutorialLinks
            WHERE AppKey = @appKey;
            """, new { appKey = appKey.Trim() }, cancellationToken: ct));
    }

    public async Task UpsertAsync(MobileAppTutorialLink row, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(row.AppKey))
        {
            throw new ArgumentException("AppKey is required", nameof(row));
        }

        await EnsureSchemaAsync(ct);
        var payload = row.Clone();
        payload.AppKey = payload.AppKey.Trim();
        payload.TitleAr = (payload.TitleAr ?? string.Empty).Trim();
        payload.VideoUrl = (payload.VideoUrl ?? string.Empty).Trim();
        payload.UpdatedAtUtc = payload.UpdatedAtUtc == default ? DateTime.UtcNow : payload.UpdatedAtUtc;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE dbo.MobileAppTutorialLinks AS target
            USING (SELECT @AppKey AS AppKey) AS source ON target.AppKey = source.AppKey
            WHEN MATCHED THEN UPDATE SET
                TitleAr = @TitleAr,
                VideoUrl = @VideoUrl,
                IsEnabled = @IsEnabled,
                UpdatedAtUtc = @UpdatedAtUtc,
                UpdatedBy = @UpdatedBy
            WHEN NOT MATCHED THEN INSERT
                (AppKey, TitleAr, VideoUrl, IsEnabled, UpdatedAtUtc, UpdatedBy)
                VALUES
                (@AppKey, @TitleAr, @VideoUrl, @IsEnabled, @UpdatedAtUtc, @UpdatedBy);
            """, payload, cancellationToken: ct));
    }
}
