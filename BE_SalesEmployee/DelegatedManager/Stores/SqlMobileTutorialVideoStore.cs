using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Gateway SQL for dbo.MobileAppTutorialVideos — many Q&amp;A videos per appKey.</summary>
public sealed class SqlMobileTutorialVideoStore : IMobileTutorialVideoStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private const string Columns = """
        Id, AppKey, QuestionAr, VideoUrl, IsEnabled, SortOrder, CreatedAtUtc, UpdatedAtUtc, UpdatedBy
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlMobileTutorialVideoStore(SqlGatewayConnectionFactory factory)
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
IF OBJECT_ID(N'dbo.MobileAppTutorialVideos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MobileAppTutorialVideos (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MobileAppTutorialVideos PRIMARY KEY,
        AppKey NVARCHAR(64) NOT NULL,
        QuestionAr NVARCHAR(400) NOT NULL,
        VideoUrl NVARCHAR(1024) NOT NULL CONSTRAINT DF_MATV_VideoUrl DEFAULT (N''),
        IsEnabled BIT NOT NULL CONSTRAINT DF_MATV_IsEnabled DEFAULT (1),
        SortOrder INT NOT NULL CONSTRAINT DF_MATV_SortOrder DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_MATV_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_MATV_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy NVARCHAR(128) NULL
    );
    CREATE INDEX IX_MobileAppTutorialVideos_AppKey
        ON dbo.MobileAppTutorialVideos (AppKey, IsEnabled, SortOrder, Id);
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<IReadOnlyList<MobileAppTutorialVideo>> ListAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<MobileAppTutorialVideo>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.MobileAppTutorialVideos
            ORDER BY AppKey ASC, SortOrder ASC, Id ASC;
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MobileAppTutorialVideo>> ListByAppAsync(
        string appKey,
        bool enabledOnly,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return Array.Empty<MobileAppTutorialVideo>();
        }

        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<MobileAppTutorialVideo>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.MobileAppTutorialVideos
            WHERE AppKey = @appKey
              AND (@enabledOnly = 0 OR IsEnabled = 1)
            ORDER BY SortOrder ASC, Id ASC;
            """, new { appKey = appKey.Trim(), enabledOnly = enabledOnly ? 1 : 0 }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<MobileAppTutorialVideo?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<MobileAppTutorialVideo>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.MobileAppTutorialVideos
            WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<MobileAppTutorialVideo> InsertAsync(MobileAppTutorialVideo row, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var payload = row.Clone();
        payload.AppKey = payload.AppKey.Trim();
        payload.QuestionAr = (payload.QuestionAr ?? string.Empty).Trim();
        payload.VideoUrl = (payload.VideoUrl ?? string.Empty).Trim();
        var now = DateTime.UtcNow;
        if (payload.CreatedAtUtc == default) payload.CreatedAtUtc = now;
        if (payload.UpdatedAtUtc == default) payload.UpdatedAtUtc = now;

        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.MobileAppTutorialVideos
                (AppKey, QuestionAr, VideoUrl, IsEnabled, SortOrder, CreatedAtUtc, UpdatedAtUtc, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@AppKey, @QuestionAr, @VideoUrl, @IsEnabled, @SortOrder, @CreatedAtUtc, @UpdatedAtUtc, @UpdatedBy);
            """, payload, cancellationToken: ct));
        payload.Id = id;
        return payload;
    }

    public async Task UpdateAsync(MobileAppTutorialVideo row, CancellationToken ct = default)
    {
        if (row.Id <= 0)
        {
            throw new ArgumentException("Id is required", nameof(row));
        }

        await EnsureSchemaAsync(ct);
        var payload = row.Clone();
        payload.AppKey = payload.AppKey.Trim();
        payload.QuestionAr = (payload.QuestionAr ?? string.Empty).Trim();
        payload.VideoUrl = (payload.VideoUrl ?? string.Empty).Trim();
        payload.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.MobileAppTutorialVideos SET
                AppKey = @AppKey,
                QuestionAr = @QuestionAr,
                VideoUrl = @VideoUrl,
                IsEnabled = @IsEnabled,
                SortOrder = @SortOrder,
                UpdatedAtUtc = @UpdatedAtUtc,
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id;
            """, payload, cancellationToken: ct));
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return;
        }

        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM dbo.MobileAppTutorialVideos WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }
}
