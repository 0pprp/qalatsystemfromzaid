using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class SqlAppCommitteeTaskStore : IAppCommitteeTaskStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private const string Columns = """
        Id, Body, IsEnabled, SortOrder, CreatedAtUtc, UpdatedAtUtc, UpdatedBy
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlAppCommitteeTaskStore(SqlGatewayConnectionFactory factory)
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
IF OBJECT_ID(N'dbo.AppCommitteeTasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppCommitteeTasks (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppCommitteeTasks PRIMARY KEY,
        Body NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AppCommitteeTasks_Body DEFAULT (N''),
        IsEnabled BIT NOT NULL CONSTRAINT DF_AppCommitteeTasks_IsEnabled DEFAULT (1),
        SortOrder INT NOT NULL CONSTRAINT DF_AppCommitteeTasks_SortOrder DEFAULT (0),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppCommitteeTasks_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_AppCommitteeTasks_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy NVARCHAR(128) NULL
    );
    CREATE INDEX IX_AppCommitteeTasks_Enabled
        ON dbo.AppCommitteeTasks (IsEnabled, SortOrder, Id);
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<IReadOnlyList<AppCommitteeTask>> ListAllAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<AppCommitteeTask>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.AppCommitteeTasks
            ORDER BY SortOrder ASC, Id ASC;
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<AppCommitteeTask>> ListEnabledAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<AppCommitteeTask>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.AppCommitteeTasks
            WHERE IsEnabled = 1
            ORDER BY SortOrder ASC, Id ASC;
            """, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<AppCommitteeTask?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AppCommitteeTask>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.AppCommitteeTasks
            WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<AppCommitteeTask> InsertAsync(AppCommitteeTask row, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var payload = row.Clone();
        payload.Body = (payload.Body ?? "").Trim();
        var now = DateTime.UtcNow;
        if (payload.CreatedAtUtc == default) payload.CreatedAtUtc = now;
        if (payload.UpdatedAtUtc == default) payload.UpdatedAtUtc = now;

        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.AppCommitteeTasks
                (Body, IsEnabled, SortOrder, CreatedAtUtc, UpdatedAtUtc, UpdatedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@Body, @IsEnabled, @SortOrder, @CreatedAtUtc, @UpdatedAtUtc, @UpdatedBy);
            """, payload, cancellationToken: ct));
        payload.Id = id;
        return payload;
    }

    public async Task UpdateAsync(AppCommitteeTask row, CancellationToken ct = default)
    {
        if (row.Id <= 0)
            throw new ArgumentException("Id is required", nameof(row));

        await EnsureSchemaAsync(ct);
        var payload = row.Clone();
        payload.Body = (payload.Body ?? "").Trim();
        payload.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.AppCommitteeTasks SET
                Body = @Body,
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
            DELETE FROM dbo.AppCommitteeTasks WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }
}
