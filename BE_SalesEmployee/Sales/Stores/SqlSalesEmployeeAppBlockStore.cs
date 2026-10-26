using System.Data;
using BE_SalesEmployee.DelegatedManager.Stores;
using Dapper;

namespace BE_SalesEmployee.Sales.Stores;

/// <summary>
/// Gateway-central blocks for sales-employee mobile app (login + start-shift gate).
/// </summary>
public sealed class SqlSalesEmployeeAppBlockStore : ISalesEmployeeAppBlockStore
{
    private int _schemaReady;
    private readonly SqlGatewayConnectionFactory _factory;

    public SqlSalesEmployeeAppBlockStore(SqlGatewayConnectionFactory factory) => _factory = factory;

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _schemaReady, 1, 0) == 1)
            return;
        try
        {
            using var connection = await _factory.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.SalesEmployeeAppBlocks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesEmployeeAppBlocks (
        CityValue NVARCHAR(100) NOT NULL,
        EmployeeId INT NOT NULL,
        IsBlocked BIT NOT NULL CONSTRAINT DF_SalesEmpAppBlocks_Blocked DEFAULT(1),
        UpdatedAtUtc DATETIME2(3) NOT NULL,
        UpdatedBy NVARCHAR(100) NULL,
        CONSTRAINT PK_SalesEmployeeAppBlocks PRIMARY KEY (CityValue, EmployeeId)
    );
END
""", cancellationToken: ct));
        }
        catch
        {
            Interlocked.Exchange(ref _schemaReady, 0);
            throw;
        }
    }

    public async Task<bool> IsBlockedAsync(string cityValue, int employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue) || employeeId <= 0)
            return false;
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var blocked = await connection.ExecuteScalarAsync<bool?>(new CommandDefinition("""
SELECT TOP (1) IsBlocked
FROM dbo.SalesEmployeeAppBlocks
WHERE CityValue = @CityValue AND EmployeeId = @EmployeeId;
""", new { CityValue = cityValue.Trim(), EmployeeId = employeeId }, cancellationToken: ct));
        return blocked == true;
    }

    public async Task<SalesEmployeeAppBlockState?> GetAsync(
        string cityValue, int employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue) || employeeId <= 0)
            return null;
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SalesEmployeeAppBlockState>(new CommandDefinition("""
SELECT CityValue, EmployeeId, IsBlocked, UpdatedAtUtc, UpdatedBy
FROM dbo.SalesEmployeeAppBlocks
WHERE CityValue = @CityValue AND EmployeeId = @EmployeeId;
""", new { CityValue = cityValue.Trim(), EmployeeId = employeeId }, cancellationToken: ct));
    }

    public async Task SetBlockedAsync(
        string cityValue,
        int employeeId,
        bool isBlocked,
        string? updatedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue) || employeeId <= 0)
            throw new ArgumentException("cityValue and employeeId required");
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
MERGE dbo.SalesEmployeeAppBlocks AS t
USING (SELECT @CityValue AS CityValue, @EmployeeId AS EmployeeId) AS s
ON t.CityValue = s.CityValue AND t.EmployeeId = s.EmployeeId
WHEN MATCHED THEN UPDATE SET
    IsBlocked = @IsBlocked,
    UpdatedAtUtc = SYSUTCDATETIME(),
    UpdatedBy = @UpdatedBy
WHEN NOT MATCHED THEN INSERT (CityValue, EmployeeId, IsBlocked, UpdatedAtUtc, UpdatedBy)
VALUES (@CityValue, @EmployeeId, @IsBlocked, SYSUTCDATETIME(), @UpdatedBy);
""", new
        {
            CityValue = cityValue.Trim(),
            EmployeeId = employeeId,
            IsBlocked = isBlocked,
            UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim(),
        }, cancellationToken: ct));
    }
}
