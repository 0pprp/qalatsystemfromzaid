using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Gateway SQL for dbo.PriceBoardEntries — monitor-authored per-province price ads.</summary>
public sealed class SqlPriceBoardStore : IPriceBoardStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private const string Columns = """
        Id, ProductName, SalePrice, DailyInstallment, DownPayment,
        TargetCityValue, TargetCityName, CreatedAtUtc, UpdatedAtUtc, CreatedByName, UpdatedByName
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlPriceBoardStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady)
            return;

        lock (SchemaGate)
        {
            if (_schemaReady)
                return;
        }

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.PriceBoardEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PriceBoardEntries (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PriceBoardEntries PRIMARY KEY,
        ProductName NVARCHAR(400) NOT NULL,
        SalePrice DECIMAL(18,0) NOT NULL CONSTRAINT DF_PBE_SalePrice DEFAULT (0),
        DailyInstallment DECIMAL(18,0) NOT NULL CONSTRAINT DF_PBE_DailyInstallment DEFAULT (0),
        DownPayment DECIMAL(18,0) NOT NULL CONSTRAINT DF_PBE_DownPayment DEFAULT (0),
        TargetCityValue NVARCHAR(100) NOT NULL,
        TargetCityName NVARCHAR(200) NOT NULL CONSTRAINT DF_PBE_TargetCityName DEFAULT (N''),
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_PBE_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_PBE_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByName NVARCHAR(200) NULL,
        UpdatedByName NVARCHAR(200) NULL
    );
    CREATE INDEX IX_PriceBoardEntries_City
        ON dbo.PriceBoardEntries (TargetCityValue, UpdatedAtUtc DESC, Id DESC);
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<IReadOnlyList<PriceBoardEntry>> ListAsync(string? cityValue, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            var all = await connection.QueryAsync<PriceBoardEntry>(new CommandDefinition($"""
                SELECT {Columns}
                FROM dbo.PriceBoardEntries
                ORDER BY UpdatedAtUtc DESC, Id DESC
                """, cancellationToken: ct));
            return all.AsList();
        }

        var filtered = await connection.QueryAsync<PriceBoardEntry>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.PriceBoardEntries
            WHERE TargetCityValue = @city
            ORDER BY UpdatedAtUtc DESC, Id DESC
            """, new { city = cityValue.Trim() }, cancellationToken: ct));
        return filtered.AsList();
    }

    public async Task<PriceBoardEntry?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PriceBoardEntry>(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.PriceBoardEntries
            WHERE Id = @id
            """, new { id }, cancellationToken: ct));
    }

    public async Task<PriceBoardEntry> InsertAsync(PriceBoardEntry row, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.QuerySingleAsync<int>(new CommandDefinition("""
            INSERT INTO dbo.PriceBoardEntries
            (ProductName, SalePrice, DailyInstallment, DownPayment, TargetCityValue, TargetCityName,
             CreatedByName, UpdatedByName)
            OUTPUT INSERTED.Id
            VALUES
            (@ProductName, @SalePrice, @DailyInstallment, @DownPayment, @TargetCityValue, @TargetCityName,
             @CreatedByName, @UpdatedByName)
            """, row, cancellationToken: ct));
        row.Id = id;
        var saved = await GetByIdAsync(id, ct);
        return saved ?? row;
    }

    public async Task UpdateAsync(PriceBoardEntry row, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.PriceBoardEntries SET
                ProductName = @ProductName,
                SalePrice = @SalePrice,
                DailyInstallment = @DailyInstallment,
                DownPayment = @DownPayment,
                TargetCityValue = @TargetCityValue,
                TargetCityName = @TargetCityName,
                UpdatedAtUtc = SYSUTCDATETIME(),
                UpdatedByName = @UpdatedByName
            WHERE Id = @Id
            """, row, cancellationToken: ct));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var n = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.PriceBoardEntries WHERE Id = @id",
            new { id },
            cancellationToken: ct));
        return n > 0;
    }
}
