using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class SalesManagerReadRepository : ISalesManagerReadRepository
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly IIraqClock _clock;

        public SalesManagerReadRepository(SalesDevelopmentGuard guard, IIraqClock clock)
        {
            _guard = guard;
            _clock = clock;
        }

        public async Task<IReadOnlyList<SalesManagerEmployeeRow>> ListEmployeesAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesManagerEmployeeRow>(new CommandDefinition(@"
SELECT UserID AS EmployeeId, UserName AS EmployeeName
FROM dbo.Users
WHERE UserType = N'موظف مبيعات'
ORDER BY UserName", cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<SalesManagerLocationPointDTO?> GetLatestPointAsync(int employeeId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await EnsureLiveTableAsync(connection, ct);
            return await connection.QueryFirstOrDefaultAsync<SalesManagerLocationPointDTO>(new CommandDefinition(@"
SELECT TOP 1 EmployeeId, ShiftId, CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
 Accuracy, Speed, Heading, DeviceTimestampUtc AS CapturedAtUtc
FROM dbo.SalesEmployeeLiveLocations
WHERE EmployeeId = @EmployeeId AND EndedAtUtc IS NULL",
                new { EmployeeId = employeeId }, cancellationToken: ct));
        }

        public async Task<SalesManagerTrackingEventDTO?> GetLatestEventAsync(int employeeId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesManagerTrackingEventDTO>(new CommandDefinition(@"
SELECT TOP 1 EventType, OccurredAtUtc AS OccurredAt, Metadata
FROM dbo.SalesTrackingEvents
WHERE EmployeeId = @EmployeeId
ORDER BY OccurredAtUtc DESC, Id DESC",
                new { EmployeeId = employeeId }, cancellationToken: ct));
        }

        public async Task<SalesShiftDTO?> GetShiftForBusinessDateAsync(int employeeId, DateTime businessDateIraq, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var shifts = await connection.QueryAsync<SalesShiftDTO>(new CommandDefinition(@"
SELECT TOP 20 Id AS ShiftId, EmployeeId, Status, StartedAtUtc, StartedAtIraq, CutoffAtUtc, ClosedAtUtc, CloseReason
FROM dbo.SalesWorkShifts
WHERE EmployeeId = @EmployeeId
ORDER BY StartedAtUtc DESC",
                new { EmployeeId = employeeId }, cancellationToken: ct));
            return shifts.FirstOrDefault(s => IraqTimeService.BusinessDateIraq(s.StartedAtIraq).Date == businessDateIraq.Date)
                   ?? shifts.FirstOrDefault(s => s.Status == SalesShiftStatuses.Active);
        }

        public async Task<IReadOnlyList<SalesManagerRoutePointDTO>> GetRouteAsync(int employeeId, DateTime fromUtc, DateTime toUtc, int maxPoints, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await EnsureOfficialColumnsAsync(connection, ct);
            var rows = await connection.QueryAsync<SalesManagerRoutePointDTO>(new CommandDefinition(@"
SELECT TOP (@Max) ShiftId, DeviceSequence, CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
 Accuracy, CapturedAtUtc AS CapturedAt, Speed, Heading,
 CAST(ISNULL(IsOfficial, 0) AS BIT) AS IsOfficial
FROM dbo.SalesLocationPoints
WHERE EmployeeId = @EmployeeId AND CapturedAtUtc >= @FromUtc AND CapturedAtUtc <= @ToUtc
ORDER BY CapturedAtUtc ASC, DeviceSequence ASC",
                new { EmployeeId = employeeId, FromUtc = fromUtc, ToUtc = toUtc, Max = maxPoints },
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<int> CountRouteAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.SalesLocationPoints
WHERE EmployeeId = @EmployeeId AND CapturedAtUtc >= @FromUtc AND CapturedAtUtc <= @ToUtc",
                new { EmployeeId = employeeId, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<SalesManagerTrackingEventDTO>> GetEventsAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesManagerTrackingEventDTO>(new CommandDefinition(@"
SELECT EventType, OccurredAtUtc AS OccurredAt, Metadata
FROM dbo.SalesTrackingEvents
WHERE EmployeeId = @EmployeeId AND OccurredAtUtc >= @FromUtc AND OccurredAtUtc <= @ToUtc
ORDER BY OccurredAtUtc ASC",
                new { EmployeeId = employeeId, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<IReadOnlyList<SalesDraftDTO>> ListSalesAsync(int? employeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesDraftDTO>(new CommandDefinition(@"
SELECT d.SaleId, d.EmployeeId, d.UserName, d.UserType, d.CityValue, d.CityName, d.Status, d.CustomerId, d.SourceCityValue,
 d.FullName, d.Phone, d.Province, d.NationalCardNumber, d.Address, d.NearestLandmark, d.MukhtarName, d.RationCenterNumber,
 d.EvaluationLevel, d.EvaluationNote, d.BaseSalePrice, d.FinalSalePrice, d.DailyInstallment, d.DownPayment,
 d.DefaultTotalSalePrice, d.DefaultDailyInstallment, d.DefaultDownPayment,
 d.OverrideTotalSalePrice, d.OverrideDailyInstallment, d.OverrideDownPayment,
 d.CreatedAt, d.CompletedAt, d.CompletedBy, d.DocumentsStatus, d.SalesRequestId, d.CustomerListId,
 c.CustomerName AS AccountCustomerName,
 r.CustomerName AS RequestCustomerName
FROM dbo.SalesDrafts d
LEFT JOIN dbo.Customers c ON c.CustomerID = d.CustomerId
LEFT JOIN dbo.SalesRequests r ON r.Id = d.SalesRequestId
WHERE (@EmployeeId IS NULL OR d.EmployeeId = @EmployeeId)
AND (@Status IS NULL OR d.Status = @Status)
AND (@FromUtc IS NULL OR d.CreatedAt >= @FromUtc)
AND (@ToUtc IS NULL OR d.CreatedAt <= @ToUtc)
ORDER BY d.CreatedAt DESC",
                new { EmployeeId = employeeId, Status = status, FromUtc = fromUtc, ToUtc = toUtc },
                cancellationToken: ct));
            var list = rows.ToList();
            foreach (var row in list)
            {
                SalesCustomerIdentity.ApplyDisplayName(row);
            }
            await SalesInventoryService.AttachListNamesAsync(connection, list, ct);
            return list;
        }

        public async Task<SalesDraftDTO?> GetSaleAsync(int saleId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var header = await connection.QueryFirstOrDefaultAsync<SalesDraftDTO>(new CommandDefinition(@"
SELECT d.SaleId, d.EmployeeId, d.UserName, d.UserType, d.CityValue, d.CityName, d.Status, d.CustomerId, d.SourceCityValue,
 d.FullName, d.Phone, d.Province, d.NationalCardNumber, d.Address, d.NearestLandmark, d.MukhtarName, d.RationCenterNumber,
 d.EvaluationLevel, d.EvaluationNote, d.BaseSalePrice, d.FinalSalePrice, d.DailyInstallment, d.DownPayment,
 d.DefaultTotalSalePrice, d.DefaultDailyInstallment, d.DefaultDownPayment,
 d.OverrideTotalSalePrice, d.OverrideDailyInstallment, d.OverrideDownPayment,
 d.CreatedAt, d.CompletedAt, d.CompletedBy, d.DocumentsStatus, d.SalesRequestId, d.CustomerListId,
 c.CustomerName AS AccountCustomerName,
 r.CustomerName AS RequestCustomerName
FROM dbo.SalesDrafts d
LEFT JOIN dbo.Customers c ON c.CustomerID = d.CustomerId
LEFT JOIN dbo.SalesRequests r ON r.Id = d.SalesRequestId
WHERE d.SaleId = @SaleId",
                new { SaleId = saleId }, cancellationToken: ct));
            if (header == null)
            {
                return null;
            }

            SalesCustomerIdentity.ApplyDisplayName(header);

            var items = await connection.QueryAsync<SalesDraftItemDTO>(new CommandDefinition(@"
SELECT SaleItemId, ProductId, ProductName, Quantity, UnitSalePrice, LineSalePrice
FROM dbo.SalesDraftItems WHERE SaleId = @SaleId",
                new { SaleId = saleId }, cancellationToken: ct));
            header.Items = items.ToList();
            await SalesInventoryService.AttachListNamesAsync(connection, [header], ct);
            return header;
        }

        public async Task<int> CountSalesTodayAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.SalesDrafts WHERE EmployeeId = @EmployeeId AND CreatedAt >= @FromUtc AND CreatedAt <= @ToUtc",
                new { EmployeeId = employeeId, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: ct));
        }

        public async Task<int> CountPendingSalesAsync(int? employeeId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.SalesDrafts
WHERE Status = @Pending AND (@EmployeeId IS NULL OR EmployeeId = @EmployeeId)",
                new { Pending = SalesStatuses.Pending, EmployeeId = employeeId }, cancellationToken: ct));
        }

        private static async Task EnsureOfficialColumnsAsync(SqlConnection connection, CancellationToken ct)
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesLocationPoints', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.SalesLocationPoints', N'IsOfficial') IS NULL
        ALTER TABLE dbo.SalesLocationPoints ADD IsOfficial BIT NOT NULL CONSTRAINT DF_SalesLocationPoints_IsOfficial DEFAULT (0);
    IF COL_LENGTH(N'dbo.SalesLocationPoints', N'OfficialSlotUtc') IS NULL
        ALTER TABLE dbo.SalesLocationPoints ADD OfficialSlotUtc DATETIME NULL;
    IF COL_LENGTH(N'dbo.SalesLocationPoints', N'ActualCapturedAtUtc') IS NULL
        ALTER TABLE dbo.SalesLocationPoints ADD ActualCapturedAtUtc DATETIME NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalesLocationPoints_ShiftOfficialSlot' AND object_id = OBJECT_ID(N'dbo.SalesLocationPoints'))
        CREATE UNIQUE INDEX UX_SalesLocationPoints_ShiftOfficialSlot ON dbo.SalesLocationPoints (ShiftId, OfficialSlotUtc) WHERE OfficialSlotUtc IS NOT NULL;
END;",
                cancellationToken: ct));
        }

        private static async Task EnsureLiveTableAsync(SqlConnection connection, CancellationToken ct)
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesEmployeeLiveLocations', N'U') IS NULL
AND OBJECT_ID(N'dbo.SalesWorkShifts', N'U') IS NOT NULL
BEGIN
    CREATE TABLE dbo.SalesEmployeeLiveLocations (
        EmployeeId INT NOT NULL PRIMARY KEY,
        ShiftId INT NOT NULL,
        Latitude DECIMAL(9,6) NOT NULL,
        Longitude DECIMAL(9,6) NOT NULL,
        Accuracy FLOAT NULL,
        Speed FLOAT NULL,
        Heading FLOAT NULL,
        UpdatedAtUtc DATETIME NOT NULL,
        DeviceTimestampUtc DATETIME NOT NULL,
        EndedAtUtc DATETIME NULL,
        CONSTRAINT FK_SalesEmployeeLiveLocations_Shifts FOREIGN KEY (ShiftId) REFERENCES dbo.SalesWorkShifts (Id)
    );
    CREATE INDEX IX_SalesEmployeeLiveLocations_Shift ON dbo.SalesEmployeeLiveLocations (ShiftId, EndedAtUtc);
END;",
                cancellationToken: ct));
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
    }
}
