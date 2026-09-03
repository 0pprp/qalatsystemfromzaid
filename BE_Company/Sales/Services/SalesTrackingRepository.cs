using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class SalesTrackingRepository : ISalesTrackingRepository
    {
        private readonly SalesDevelopmentGuard _guard;

        public SalesTrackingRepository(SalesDevelopmentGuard guard)
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

        public async Task<SalesShiftDTO?> GetActiveByEmployeeAsync(int employeeId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesShiftDTO>(new CommandDefinition(
                ShiftSelect + " WHERE EmployeeId = @EmployeeId AND Status = @Status",
                new { EmployeeId = employeeId, Status = SalesShiftStatuses.Active },
                cancellationToken: ct));
        }

        public async Task<SalesShiftDTO?> GetByIdAsync(int shiftId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesShiftDTO>(new CommandDefinition(
                ShiftSelect + " WHERE Id = @Id",
                new { Id = shiftId },
                cancellationToken: ct));
        }

        public async Task<SalesShiftDTO> InsertActiveAsync(
            int employeeId,
            string employeeName,
            string cityValue,
            string cityName,
            DateTime startedAtUtc,
            DateTime startedAtIraq,
            DateTime cutoffAtUtc,
            CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesWorkShifts
(EmployeeId, EmployeeName, CityValue, CityName, StartedAtUtc, StartedAtIraq, CutoffAtUtc, Status)
OUTPUT INSERTED.Id
VALUES (@EmployeeId, @EmployeeName, @CityValue, @CityName, @StartedAtUtc, @StartedAtIraq, @CutoffAtUtc, @Status);",
                new
                {
                    EmployeeId = employeeId,
                    EmployeeName = employeeName,
                    CityValue = cityValue,
                    CityName = cityName,
                    StartedAtUtc = startedAtUtc,
                    StartedAtIraq = startedAtIraq,
                    CutoffAtUtc = cutoffAtUtc,
                    Status = SalesShiftStatuses.Active
                }, cancellationToken: ct));
            return (await GetByIdAsync(id, ct))!;
        }

        public async Task CloseAsync(int shiftId, DateTime closedAtUtc, string reason, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesWorkShifts
SET Status = @Closed, ClosedAtUtc = @ClosedAtUtc, CloseReason = @Reason
WHERE Id = @Id AND Status = @Active",
                new
                {
                    Id = shiftId,
                    Closed = SalesShiftStatuses.Closed,
                    Active = SalesShiftStatuses.Active,
                    ClosedAtUtc = closedAtUtc,
                    Reason = reason
                }, cancellationToken: ct));
        }

        public async Task CloseExpiredAsync(DateTime utcNow, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesWorkShifts
SET Status = @Closed, ClosedAtUtc = @UtcNow, CloseReason = @Reason
WHERE Status = @Active AND CutoffAtUtc <= @UtcNow",
                new
                {
                    Closed = SalesShiftStatuses.Closed,
                    Active = SalesShiftStatuses.Active,
                    UtcNow = utcNow,
                    Reason = SalesShiftCloseReasons.AutomaticCutoff
                }, cancellationToken: ct));
        }

        public async Task<int> TryInsertPointAsync(
            int employeeId,
            int shiftId,
            SalesLocationPointRequestDTO point,
            DateTime receivedAtUtc,
            CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            try
            {
                var rows = await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesLocationPoints
(EmployeeId, ShiftId, Latitude, Longitude, Accuracy, Speed, Heading, Altitude, CapturedAtUtc, ReceivedAtUtc, DeviceSequence, DeviceSessionId)
VALUES
(@EmployeeId, @ShiftId, @Latitude, @Longitude, @Accuracy, @Speed, @Heading, @Altitude, @CapturedAtUtc, @ReceivedAtUtc, @DeviceSequence, @DeviceSessionId);",
                    new
                    {
                        EmployeeId = employeeId,
                        ShiftId = shiftId,
                        point.Latitude,
                        point.Longitude,
                        point.Accuracy,
                        point.Speed,
                        point.Heading,
                        point.Altitude,
                        point.CapturedAtUtc,
                        ReceivedAtUtc = receivedAtUtc,
                        point.DeviceSequence,
                        point.DeviceSessionId
                    }, cancellationToken: ct));
                return rows;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return 0;
            }
        }

        public async Task InsertEventAsync(int employeeId, int? shiftId, string eventType, DateTime occurredAtUtc, string? metadata, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesTrackingEvents (EmployeeId, ShiftId, EventType, OccurredAtUtc, Metadata)
VALUES (@EmployeeId, @ShiftId, @EventType, @OccurredAtUtc, @Metadata);",
                new { EmployeeId = employeeId, ShiftId = shiftId, EventType = eventType, OccurredAtUtc = occurredAtUtc, Metadata = Trunc(metadata, 400) },
                cancellationToken: ct));
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private static string? Trunc(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? value : (value.Length <= max ? value : value[..max]);

        private const string ShiftSelect = @"
SELECT Id AS ShiftId, EmployeeId, Status, StartedAtUtc, StartedAtIraq, CutoffAtUtc, ClosedAtUtc, CloseReason
FROM dbo.SalesWorkShifts";

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.SalesWorkShifts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesWorkShifts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(200) NULL,
        CityValue NVARCHAR(100) NULL,
        CityName NVARCHAR(200) NULL,
        StartedAtUtc DATETIME NOT NULL,
        StartedAtIraq DATETIME NOT NULL,
        CutoffAtUtc DATETIME NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        ClosedAtUtc DATETIME NULL,
        CloseReason NVARCHAR(50) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesWorkShifts_CreatedAt DEFAULT (GETUTCDATE())
    );
    CREATE UNIQUE INDEX UX_SalesWorkShifts_OneActive ON dbo.SalesWorkShifts (EmployeeId) WHERE Status = N'Active';
END;
IF OBJECT_ID(N'dbo.SalesLocationPoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesLocationPoints (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ShiftId INT NOT NULL,
        Latitude DECIMAL(9,6) NOT NULL,
        Longitude DECIMAL(9,6) NOT NULL,
        Accuracy FLOAT NULL,
        Speed FLOAT NULL,
        Heading FLOAT NULL,
        Altitude FLOAT NULL,
        CapturedAtUtc DATETIME NOT NULL,
        ReceivedAtUtc DATETIME NOT NULL,
        DeviceSequence BIGINT NOT NULL,
        DeviceSessionId NVARCHAR(100) NULL,
        CONSTRAINT FK_SalesLocationPoints_Shifts FOREIGN KEY (ShiftId) REFERENCES dbo.SalesWorkShifts (Id)
    );
    CREATE UNIQUE INDEX UX_SalesLocationPoints_ShiftSequence ON dbo.SalesLocationPoints (ShiftId, DeviceSequence);
END;
IF OBJECT_ID(N'dbo.SalesTrackingEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesTrackingEvents (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ShiftId INT NULL,
        EventType NVARCHAR(80) NOT NULL,
        OccurredAtUtc DATETIME NOT NULL,
        Metadata NVARCHAR(400) NULL
    );
END;";
    }
}
