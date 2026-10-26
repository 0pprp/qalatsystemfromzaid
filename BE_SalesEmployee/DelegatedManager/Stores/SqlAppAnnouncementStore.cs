using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class SqlAppAnnouncementStore : IAppAnnouncementStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private const string Columns = """
        Id, AppKey, ContentType, TextBody, ImageFileName, ZoomImageFileName, HasLink, LinkUrl,
        IsEnabled, SortOrder, CreatedAtUtc, UpdatedAtUtc, UpdatedBy
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlAppAnnouncementStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady) return;

        lock (SchemaGate)
        {
            if (_schemaReady) return;
        }

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.AppAnnouncements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppAnnouncements (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppAnnouncements PRIMARY KEY,
        AppKey NVARCHAR(64) NOT NULL,
        ContentType NVARCHAR(16) NOT NULL CONSTRAINT DF_AppAnn_ContentType DEFAULT (N'text'),
        TextBody NVARCHAR(2000) NOT NULL CONSTRAINT DF_AppAnn_TextBody DEFAULT (N''),
        ImageFileName NVARCHAR(260) NOT NULL CONSTRAINT DF_AppAnn_ImageFileName DEFAULT (N''),
        ZoomImageFileName NVARCHAR(260) NOT NULL CONSTRAINT DF_AppAnn_ZoomImageFileName DEFAULT (N''),
        HasLink BIT NOT NULL CONSTRAINT DF_AppAnn_HasLink DEFAULT (0),
        LinkUrl NVARCHAR(1024) NOT NULL CONSTRAINT DF_AppAnn_LinkUrl DEFAULT (N''),
        IsEnabled BIT NOT NULL CONSTRAINT DF_AppAnn_IsEnabled DEFAULT (1),
        SortOrder INT NOT NULL CONSTRAINT DF_AppAnn_SortOrder DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppAnn_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppAnn_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy NVARCHAR(128) NULL
    );
    CREATE INDEX IX_AppAnnouncements_AppKey
        ON dbo.AppAnnouncements (AppKey, IsEnabled, SortOrder, Id);
END

IF OBJECT_ID(N'dbo.AppAnnouncements', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AppAnnouncements', N'ZoomImageFileName') IS NULL
BEGIN
    ALTER TABLE dbo.AppAnnouncements
        ADD ZoomImageFileName NVARCHAR(260) NOT NULL
            CONSTRAINT DF_AppAnn_ZoomImageFileName DEFAULT (N'');
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<IReadOnlyList<AppAnnouncement>> ListAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<AppAnnouncement>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.AppAnnouncements
            ORDER BY AppKey ASC, SortOrder ASC, Id ASC;
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AppAnnouncement>> ListByAppAsync(
        string appKey,
        bool enabledOnly,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
            return Array.Empty<AppAnnouncement>();

        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<AppAnnouncement>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.AppAnnouncements
            WHERE AppKey = @appKey
              AND (@enabledOnly = 0 OR IsEnabled = 1)
            ORDER BY SortOrder ASC, Id ASC;
            """, new { appKey = appKey.Trim(), enabledOnly = enabledOnly ? 1 : 0 }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<AppAnnouncement?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AppAnnouncement>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.AppAnnouncements
            WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<AppAnnouncement> InsertAsync(AppAnnouncement row, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var payload = Normalize(row);
        var now = DateTime.UtcNow;
        if (payload.CreatedAtUtc == default) payload.CreatedAtUtc = now;
        if (payload.UpdatedAtUtc == default) payload.UpdatedAtUtc = now;

        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.AppAnnouncements
                (AppKey, ContentType, TextBody, ImageFileName, ZoomImageFileName, HasLink, LinkUrl,
                 IsEnabled, SortOrder, CreatedAtUtc, UpdatedAtUtc, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@AppKey, @ContentType, @TextBody, @ImageFileName, @ZoomImageFileName, @HasLink, @LinkUrl,
                 @IsEnabled, @SortOrder, @CreatedAtUtc, @UpdatedAtUtc, @UpdatedBy);
            """, payload, cancellationToken: ct));
        payload.Id = id;
        return payload;
    }

    public async Task UpdateAsync(AppAnnouncement row, CancellationToken ct = default)
    {
        if (row.Id <= 0)
            throw new ArgumentException("Id is required", nameof(row));

        await EnsureSchemaAsync(ct);
        var payload = Normalize(row);
        payload.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.AppAnnouncements SET
                AppKey = @AppKey,
                ContentType = @ContentType,
                TextBody = @TextBody,
                ImageFileName = @ImageFileName,
                ZoomImageFileName = @ZoomImageFileName,
                HasLink = @HasLink,
                LinkUrl = @LinkUrl,
                IsEnabled = @IsEnabled,
                SortOrder = @SortOrder,
                UpdatedAtUtc = @UpdatedAtUtc,
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id;
            """, payload, cancellationToken: ct));
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return;
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM dbo.AppAnnouncements WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    private static AppAnnouncement Normalize(AppAnnouncement row)
    {
        var payload = row.Clone();
        payload.AppKey = (payload.AppKey ?? "").Trim();
        payload.ContentType = (payload.ContentType ?? "text").Trim().ToLowerInvariant();
        payload.TextBody = (payload.TextBody ?? "").Trim();
        payload.ImageFileName = (payload.ImageFileName ?? "").Trim();
        payload.ZoomImageFileName = (payload.ZoomImageFileName ?? "").Trim();
        payload.LinkUrl = (payload.LinkUrl ?? "").Trim();
        return payload;
    }
}
