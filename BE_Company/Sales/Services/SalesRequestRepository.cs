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
 CustomerAddress, Notes, Status, CreatedAtUtc, AssignedAtUtc, AssignedByUserId, AssignedByName, PendingNote, ReturnNote)
OUTPUT INSERTED.Id
VALUES
(@CreatedByUserId, @CreatedByName, @CreatedByUserType, @TargetEmployeeId, @TargetEmployeeName, @CityValue, @CityName,
 @CustomerSourceType, @ExistingCustomerId, @CustomerSourceCityValue, @CustomerName, @CustomerPhone, @CustomerProvince,
 @CustomerAddress, @Notes, @Status, @CreatedAtUtc, @AssignedAtUtc, @AssignedByUserId, @AssignedByName, @PendingNote, @ReturnNote);",
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
 Status = @Status, TargetEmployeeId = @TargetEmployeeId, TargetEmployeeName = @TargetEmployeeName,
 CityValue = @CityValue, CityName = @CityName,
 ViewedAtUtc = @ViewedAtUtc, ProcessingAtUtc = @ProcessingAtUtc,
 ConvertedToSaleId = @ConvertedToSaleId, CompletedAtUtc = @CompletedAtUtc,
 RejectedAtUtc = @RejectedAtUtc, RejectionReason = @RejectionReason,
 AssignedAtUtc = @AssignedAtUtc, AssignedByUserId = @AssignedByUserId, AssignedByName = @AssignedByName,
 PendingNote = @PendingNote, ReturnNote = @ReturnNote
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

        public async Task InsertHistoryAsync(SalesRequestHistoryDTO row, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesRequestHistory
(RequestId, EventType, Status, ActorUserId, ActorName, ActorType, EmployeeId, Note, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES
(@RequestId, @Event, @Status, @ActorUserId, @ActorName, @ActorType, @EmployeeId, @Note, @CreatedAtUtc);",
                row, cancellationToken: ct));
            row.Id = id;
        }

        public async Task<IReadOnlyList<SalesRequestHistoryDTO>> ListHistoryAsync(int requestId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesRequestHistoryDTO>(new CommandDefinition(@"
SELECT Id, RequestId, EventType AS Event, Status, ActorUserId, ActorName, ActorType, EmployeeId, Note, CreatedAtUtc
FROM dbo.SalesRequestHistory
WHERE RequestId = @RequestId
ORDER BY CreatedAtUtc ASC, Id ASC",
                new { RequestId = requestId }, cancellationToken: ct));
            return rows.ToList();
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private const string SelectSql = @"
SELECT Id, CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName,
 CityValue, CityName, CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue,
 CustomerName, CustomerPhone, CustomerProvince, CustomerAddress, Notes, Status,
 CreatedAtUtc, ViewedAtUtc, ProcessingAtUtc, ConvertedToSaleId, CompletedAtUtc, RejectedAtUtc, RejectionReason,
 AssignedAtUtc, AssignedByUserId, AssignedByName, PendingNote, ReturnNote
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
END;
IF COL_LENGTH(N'dbo.SalesRequests', N'AssignedAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD AssignedAtUtc DATETIME NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'AssignedByUserId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD AssignedByUserId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'AssignedByName') IS NULL
    ALTER TABLE dbo.SalesRequests ADD AssignedByName NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'PendingNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD PendingNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ReturnNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ReturnNote NVARCHAR(1000) NULL;
IF OBJECT_ID(N'dbo.SalesRequestHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequestHistory (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RequestId INT NOT NULL,
        EventType NVARCHAR(50) NOT NULL,
        Status NVARCHAR(30) NULL,
        ActorUserId INT NULL,
        ActorName NVARCHAR(200) NULL,
        ActorType NVARCHAR(50) NULL,
        EmployeeId INT NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesRequestHistory_CreatedAt DEFAULT (GETUTCDATE())
    );
    CREATE INDEX IX_SalesRequestHistory_RequestId ON dbo.SalesRequestHistory (RequestId, CreatedAtUtc, Id);
END;";
    }
}
