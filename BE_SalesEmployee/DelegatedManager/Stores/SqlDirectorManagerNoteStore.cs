using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Gateway SQL for dbo.DirectorManagerNotes — inbox for المدير (حيدر) only.</summary>
public sealed class SqlDirectorManagerNoteStore : IDirectorManagerNoteStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private const string Columns = """
        Id, SourceApp, SourceType, SenderUserId, SenderUserName, SenderDisplayName, SenderRole,
        CityValue, CityName, Subject, Body, Status, CreatedAtUtc, ReadAtUtc, MetadataJson
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlDirectorManagerNoteStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady)
            return;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.DirectorManagerNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DirectorManagerNotes (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DirectorManagerNotes PRIMARY KEY,
        SourceApp NVARCHAR(64) NOT NULL,
        SourceType NVARCHAR(32) NULL,
        SenderUserId NVARCHAR(64) NULL,
        SenderUserName NVARCHAR(128) NULL,
        SenderDisplayName NVARCHAR(256) NOT NULL,
        SenderRole NVARCHAR(64) NOT NULL,
        CityValue NVARCHAR(64) NULL,
        CityName NVARCHAR(128) NULL,
        Subject NVARCHAR(256) NOT NULL,
        Body NVARCHAR(2000) NOT NULL,
        Status NVARCHAR(32) NOT NULL CONSTRAINT DF_DMN_Status DEFAULT (N'Unread'),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DMN_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ReadAtUtc DATETIME2(3) NULL,
        MetadataJson NVARCHAR(MAX) NULL
    );
    CREATE INDEX IX_DirectorManagerNotes_Created
        ON dbo.DirectorManagerNotes (CreatedAtUtc DESC, Id DESC);
    CREATE INDEX IX_DirectorManagerNotes_Status
        ON dbo.DirectorManagerNotes (Status, CreatedAtUtc DESC);
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<DirectorManagerNote> CreateAsync(DirectorManagerNote note, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var row = note.Clone();
        if (row.Id == Guid.Empty)
            row.Id = Guid.NewGuid();
        if (row.CreatedAtUtc == default)
            row.CreatedAtUtc = DateTime.UtcNow;
        row.Status = ComplaintStatuses.Unread;
        row.ReadAtUtc = null;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO dbo.DirectorManagerNotes ({Columns})
            VALUES (@Id, @SourceApp, @SourceType, @SenderUserId, @SenderUserName, @SenderDisplayName, @SenderRole,
                    @CityValue, @CityName, @Subject, @Body, @Status, @CreatedAtUtc, @ReadAtUtc, @MetadataJson);
            """, row, cancellationToken: ct));
        return row;
    }

    public async Task<PagedResult<DirectorManagerNote>> QueryAsync(DirectorNoteInboxQuery query, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        var parameters = new DynamicParameters();
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Unread", ComplaintStatuses.Unread);
        parameters.Add("UnreadOnly", query.UnreadOnly ? 1 : 0);
        parameters.Add("CityValue", string.IsNullOrWhiteSpace(query.CityValue) ? null : query.CityValue.Trim());
        parameters.Add("SourceApp", string.IsNullOrWhiteSpace(query.SourceApp) ? null : query.SourceApp.Trim());
        parameters.Add("Search", string.IsNullOrWhiteSpace(query.Search) ? null : $"%{query.Search.Trim()}%");

        const string where = """
            WHERE (@UnreadOnly = 0 OR Status = @Unread)
              AND (@CityValue IS NULL OR CityValue = @CityValue)
              AND (@SourceApp IS NULL OR SourceApp = @SourceApp)
              AND (@Search IS NULL OR Subject LIKE @Search OR Body LIKE @Search OR SenderDisplayName LIKE @Search)
            """;

        using var connection = await _factory.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.DirectorManagerNotes
            {where}
            ORDER BY CreatedAtUtc DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM dbo.DirectorManagerNotes {where};
            """, parameters, cancellationToken: ct));

        var items = (await grid.ReadAsync<DirectorManagerNote>()).ToList();
        var total = await grid.ReadSingleAsync<int>();
        return new PagedResult<DirectorManagerNote>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<DirectorManagerNote?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<DirectorManagerNote>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.DirectorManagerNotes WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<DirectorManagerNote?> MarkReadAsync(Guid id, DateTime readAtUtc, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.DirectorManagerNotes
            SET Status = @read, ReadAtUtc = @readAtUtc
            WHERE Id = @id AND Status = @unread;
            """, new { id, readAtUtc, read = ComplaintStatuses.Read, unread = ComplaintStatuses.Unread }, cancellationToken: ct));

        return await connection.QuerySingleOrDefaultAsync<DirectorManagerNote>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.DirectorManagerNotes WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<int> MarkAllReadAsync(DateTime readAtUtc, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.DirectorManagerNotes
            SET Status = @read, ReadAtUtc = @readAtUtc
            WHERE Status = @unread;
            """, new { readAtUtc, read = ComplaintStatuses.Read, unread = ComplaintStatuses.Unread }, cancellationToken: ct));
    }

    public async Task<int> CountUnreadAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM dbo.DirectorManagerNotes WHERE Status = @unread;
            """, new { unread = ComplaintStatuses.Unread }, cancellationToken: ct));
    }
}
