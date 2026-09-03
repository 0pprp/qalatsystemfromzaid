using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class SalesRequestRepository : ISalesRequestRepository
    {
        private readonly SalesDevelopmentGuard _guard;

        public SalesRequestRepository(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = SchemaSql;
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<SalesRequestDTO> InsertAsync(SalesRequestDTO row, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesRequests
(CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName, CityValue, CityName,
 CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue, CustomerName, CustomerPhone, CustomerProvince,
 CustomerAddress, Notes, Status, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES
(@CreatedByUserId, @CreatedByName, @CreatedByUserType, @TargetEmployeeId, @TargetEmployeeName, @CityValue, @CityName,
 @CustomerSourceType, @ExistingCustomerId, @CustomerSourceCityValue, @CustomerName, @CustomerPhone, @CustomerProvince,
 @CustomerAddress, @Notes, @Status, @CreatedAtUtc);",
                row, cancellationToken: ct));
            row.Id = id;
            return row;
        }

        public async Task<SalesRequestDTO?> GetByIdAsync(int id, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesRequestDTO>(new CommandDefinition(
                SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListAsync(int? targetEmployeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var sql = SelectSql + @" WHERE (@TargetEmployeeId IS NULL OR TargetEmployeeId = @TargetEmployeeId)
AND (@Status IS NULL OR Status = @Status)
AND (@FromUtc IS NULL OR CreatedAtUtc >= @FromUtc)
AND (@ToUtc IS NULL OR CreatedAtUtc <= @ToUtc)
ORDER BY CreatedAtUtc DESC";
            var rows = await connection.QueryAsync<SalesRequestDTO>(new CommandDefinition(sql,
                new { TargetEmployeeId = targetEmployeeId, Status = status, FromUtc = fromUtc, ToUtc = toUtc },
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task UpdateAsync(SalesRequestDTO row, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesRequests SET
 Status = @Status, ViewedAtUtc = @ViewedAtUtc, ProcessingAtUtc = @ProcessingAtUtc,
 ConvertedToSaleId = @ConvertedToSaleId, CompletedAtUtc = @CompletedAtUtc,
 RejectedAtUtc = @RejectedAtUtc, RejectionReason = @RejectionReason
WHERE Id = @Id", row, cancellationToken: ct));
        }

        public async Task<int> CountByStatusAsync(string status, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.SalesRequests WHERE Status = @Status",
                new { Status = status }, cancellationToken: ct));
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private const string SelectSql = @"
SELECT Id, CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName,
 CityValue, CityName, CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue,
 CustomerName, CustomerPhone, CustomerProvince, CustomerAddress, Notes, Status,
 CreatedAtUtc, ViewedAtUtc, ProcessingAtUtc, ConvertedToSaleId, CompletedAtUtc, RejectedAtUtc, RejectionReason
FROM dbo.SalesRequests";

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequests (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NULL,
        CreatedByUserType NVARCHAR(50) NULL,
        TargetEmployeeId INT NOT NULL,
        TargetEmployeeName NVARCHAR(200) NULL,
        CityValue NVARCHAR(100) NULL,
        CityName NVARCHAR(200) NULL,
        CustomerSourceType NVARCHAR(30) NOT NULL,
        ExistingCustomerId INT NULL,
        CustomerSourceCityValue NVARCHAR(100) NULL,
        CustomerName NVARCHAR(200) NOT NULL,
        CustomerPhone NVARCHAR(50) NULL,
        CustomerProvince NVARCHAR(100) NULL,
        CustomerAddress NVARCHAR(400) NULL,
        Notes NVARCHAR(1000) NULL,
        Status NVARCHAR(30) NOT NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesRequests_CreatedAt DEFAULT (GETUTCDATE()),
        ViewedAtUtc DATETIME NULL,
        ProcessingAtUtc DATETIME NULL,
        ConvertedToSaleId INT NULL,
        CompletedAtUtc DATETIME NULL,
        RejectedAtUtc DATETIME NULL,
        RejectionReason NVARCHAR(400) NULL
    );
END;
IF COL_LENGTH(N'dbo.SalesDrafts', N'SalesRequestId') IS NULL
BEGIN
    ALTER TABLE dbo.SalesDrafts ADD SalesRequestId INT NULL;
END;";
    }
}
