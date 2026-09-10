using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services.FollowerTracking
{
    public static class FollowerIraqTime
    {
        public static readonly TimeSpan BaghdadOffset = TimeSpan.FromHours(3);
        public static readonly TimeSpan CutoffTime = TimeSpan.FromHours(3);

        public static DateTime UtcNow => DateTime.UtcNow;

        public static DateTime ToIraq(DateTime utc) =>
            DateTime.SpecifyKind(utc.ToUniversalTime().Add(BaghdadOffset), DateTimeKind.Unspecified);

        public static DateTime ToUtcFromIraq(DateTime iraqLocal) =>
            DateTime.SpecifyKind(iraqLocal.Add(-BaghdadOffset), DateTimeKind.Utc);

        public static DateTime CutoffUtc(DateTime utcNow)
        {
            var iraq = ToIraq(utcNow);
            var businessDate = iraq.Date;
            if (iraq.TimeOfDay < CutoffTime)
            {
                businessDate = businessDate.AddDays(-1);
            }

            return ToUtcFromIraq(businessDate.AddDays(1).Add(CutoffTime));
        }

        public static bool IsExpired(DateTime cutoffAtUtc, DateTime utcNow) => utcNow >= cutoffAtUtc;
    }

    /// <summary>Same algorithm as sales OfficialSlot: start + n*10min, n &gt;= 1.</summary>
    public static class FollowerOfficialSlot
    {
        public static readonly TimeSpan Length = TimeSpan.FromMinutes(10);

        public static bool IsExactOfficialSlot(DateTime shiftStartUtc, DateTime officialSlotUtc)
        {
            var start = Utc(shiftStartUtc);
            var slot = Utc(officialSlotUtc);
            var delta = slot - start;
            if (delta < Length) return false;
            return delta.Ticks % Length.Ticks == 0;
        }

        public static long SlotIndex(DateTime shiftStartUtc, DateTime slotUtc)
        {
            var index = (Utc(slotUtc) - Utc(shiftStartUtc)).Ticks / Length.Ticks;
            return index <= 0 ? 1 : index;
        }

        public static DateTime SlotUtc(DateTime shiftStartUtc, long index) =>
            Utc(shiftStartUtc).AddMinutes(10 * index);

        public static IReadOnlyList<DateTime> DueSlots(
            DateTime shiftStartUtc,
            DateTime? lastOfficialSlotUtc,
            DateTime nowUtc,
            DateTime cutoffUtc)
        {
            shiftStartUtc = Utc(shiftStartUtc);
            nowUtc = Utc(nowUtc);
            cutoffUtc = Utc(cutoffUtc);
            DateTime? last = lastOfficialSlotUtc is DateTime prior && prior != default ? Utc(prior) : null;
            var slots = new List<DateTime>();
            for (var index = 1; index <= 2000; index++)
            {
                var due = shiftStartUtc.AddMinutes(10 * index);
                if (due >= cutoffUtc || due > nowUtc) break;
                if (last is null || due > last.Value) slots.Add(due);
            }

            return slots;
        }

        private static DateTime Utc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    public sealed class FollowerShiftDto
    {
        public int ShiftId { get; set; }
        public int FollowerId { get; set; }
        public string FollowerName { get; set; } = "";
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime StartedAtUtc { get; set; }
        public DateTime StartedAtIraq { get; set; }
        public DateTime CutoffAtUtc { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public string? CloseReason { get; set; }
        public bool IsNew { get; set; }
        public bool HasActiveShift { get; set; }
    }

    public sealed class FollowerLocationPointDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public DateTime? OfficialSlotUtc { get; set; }
        public DateTime? ActualCapturedAtUtc { get; set; }
        public bool IsOfficial { get; set; } = true;
        public long DeviceSequence { get; set; }
    }

    public sealed class FollowerLocationBatchRequestDto
    {
        public int ShiftId { get; set; }
        public List<FollowerLocationPointDto> Points { get; set; } = [];
    }

    public sealed class FollowerLocationBatchResultDto
    {
        public int ShiftId { get; set; }
        public string ShiftStatus { get; set; } = "";
        public int Accepted { get; set; }
        public int Duplicates { get; set; }
        public int Rejected { get; set; }
    }

    public sealed class FollowerLiveLocationRequestDto
    {
        public int ShiftId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public DateTime CapturedAtUtc { get; set; }
    }

    public sealed class FollowerLiveLocationDto
    {
        public int FollowerId { get; set; }
        public string FollowerName { get; set; } = "";
        public int ShiftId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string ShiftStatus { get; set; } = "Active";
        public string LocationStatus { get; set; } = "Live";
    }

    public sealed class FollowerRoutePointDto
    {
        public long DeviceSequence { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime CapturedAt { get; set; }
        public bool IsOfficial { get; set; }
    }

    public interface IFollowerTrackingRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        Task<FollowerShiftDto?> GetActiveByFollowerAsync(int followerId, CancellationToken ct);
        Task<FollowerShiftDto?> GetByIdAsync(int shiftId, CancellationToken ct);
        Task<FollowerShiftDto> InsertActiveAsync(int followerId, string followerName, string? cityValue, string? cityName, DateTime startedAtUtc, DateTime startedAtIraq, DateTime cutoffAtUtc, CancellationToken ct);
        Task CloseAsync(int shiftId, DateTime closedAtUtc, string reason, CancellationToken ct);
        Task CloseExpiredAsync(DateTime utcNow, CancellationToken ct);
        Task<int> TryInsertPointAsync(int followerId, int shiftId, FollowerLocationPointDto point, DateTime receivedAtUtc, CancellationToken ct);
        Task UpsertLiveAsync(int followerId, string followerName, int shiftId, FollowerLiveLocationRequestDto point, DateTime updatedAtUtc, CancellationToken ct);
        Task EndLiveAsync(int followerId, CancellationToken ct);
        Task InsertEventAsync(int followerId, int? shiftId, string eventType, DateTime occurredAtUtc, string? metadata, CancellationToken ct);
        Task<IReadOnlyList<FollowerLiveLocationDto>> ListLiveAsync(CancellationToken ct);
        Task<IReadOnlyList<FollowerRoutePointDto>> GetRouteAsync(int followerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
        Task<FollowerShiftDto?> GetShiftForDayAsync(int followerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
        Task<IReadOnlyList<(int FollowerId, string FollowerName)>> ListFollowersWithShiftsAsync(CancellationToken ct);
    }

    public sealed class FollowerTrackingRepository : IFollowerTrackingRepository
    {
        private readonly string _cs;

        public FollowerTrackingRepository(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("DataBaseConnection missing.");
        }

        private IDbConnection Conn() => new SqlConnection(_cs);

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            await using var connection = new SqlConnection(_cs);
            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = SchemaSql;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public Task<FollowerShiftDto?> GetActiveByFollowerAsync(int followerId, CancellationToken ct) =>
            QueryShift("WHERE FollowerId = @FollowerId AND Status = N'Active'", new { FollowerId = followerId }, ct);

        public Task<FollowerShiftDto?> GetByIdAsync(int shiftId, CancellationToken ct) =>
            QueryShift("WHERE Id = @Id", new { Id = shiftId }, ct);

        private async Task<FollowerShiftDto?> QueryShift(string where, object args, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            return await c.QueryFirstOrDefaultAsync<FollowerShiftDto>(new CommandDefinition(
                ShiftSelect + " " + where, args, cancellationToken: ct));
        }

        public async Task<FollowerShiftDto> InsertActiveAsync(
            int followerId, string followerName, string? cityValue, string? cityName,
            DateTime startedAtUtc, DateTime startedAtIraq, DateTime cutoffAtUtc, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.FollowerWorkShifts
(FollowerId, FollowerName, CityValue, CityName, StartedAtUtc, StartedAtIraq, CutoffAtUtc, Status)
OUTPUT INSERTED.Id
VALUES (@FollowerId, @FollowerName, @CityValue, @CityName, @StartedAtUtc, @StartedAtIraq, @CutoffAtUtc, N'Active');",
                new
                {
                    FollowerId = followerId,
                    FollowerName = followerName,
                    CityValue = cityValue,
                    CityName = cityName,
                    StartedAtUtc = startedAtUtc,
                    StartedAtIraq = startedAtIraq,
                    CutoffAtUtc = cutoffAtUtc
                }, cancellationToken: ct));
            return (await GetByIdAsync(id, ct))!;
        }

        public async Task CloseAsync(int shiftId, DateTime closedAtUtc, string reason, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerWorkShifts
SET Status = N'Closed', ClosedAtUtc = @ClosedAtUtc, CloseReason = @Reason
WHERE Id = @Id;",
                new { Id = shiftId, ClosedAtUtc = closedAtUtc, Reason = reason }, cancellationToken: ct));
        }

        public async Task CloseExpiredAsync(DateTime utcNow, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerWorkShifts
SET Status = N'Closed', ClosedAtUtc = @UtcNow, CloseReason = N'AutomaticCutoff'
WHERE Status = N'Active' AND CutoffAtUtc <= @UtcNow;",
                new { UtcNow = utcNow }, cancellationToken: ct));
        }

        public async Task<int> TryInsertPointAsync(int followerId, int shiftId, FollowerLocationPointDto point, DateTime receivedAtUtc, CancellationToken ct)
        {
            try
            {
                await using var c = (SqlConnection)Conn();
                return await c.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.FollowerLocationPoints
(FollowerId, ShiftId, Latitude, Longitude, Accuracy, Speed, Heading, CapturedAtUtc, ReceivedAtUtc, DeviceSequence, IsOfficial, OfficialSlotUtc, ActualCapturedAtUtc)
VALUES
(@FollowerId, @ShiftId, @Latitude, @Longitude, @Accuracy, @Speed, @Heading, @CapturedAtUtc, @ReceivedAtUtc, @DeviceSequence, @IsOfficial, @OfficialSlotUtc, @ActualCapturedAtUtc);",
                    new
                    {
                        FollowerId = followerId,
                        ShiftId = shiftId,
                        point.Latitude,
                        point.Longitude,
                        point.Accuracy,
                        point.Speed,
                        point.Heading,
                        CapturedAtUtc = point.CapturedAtUtc,
                        ReceivedAtUtc = receivedAtUtc,
                        point.DeviceSequence,
                        IsOfficial = point.IsOfficial,
                        OfficialSlotUtc = point.OfficialSlotUtc ?? point.CapturedAtUtc,
                        ActualCapturedAtUtc = point.ActualCapturedAtUtc ?? point.CapturedAtUtc
                    }, cancellationToken: ct));
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                return 0;
            }
        }

        public async Task UpsertLiveAsync(int followerId, string followerName, int shiftId, FollowerLiveLocationRequestDto point, DateTime updatedAtUtc, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.FollowerLiveLocations AS t
USING (SELECT @FollowerId AS FollowerId) AS s ON t.FollowerId = s.FollowerId
WHEN MATCHED AND (t.CapturedAtUtc IS NULL OR t.CapturedAtUtc <= @CapturedAtUtc) THEN UPDATE SET
 ShiftId = @ShiftId, FollowerName = @FollowerName, Latitude = @Latitude, Longitude = @Longitude,
 Accuracy = @Accuracy, Speed = @Speed, Heading = @Heading, CapturedAtUtc = @CapturedAtUtc, UpdatedAtUtc = @UpdatedAtUtc
WHEN NOT MATCHED THEN INSERT
(FollowerId, FollowerName, ShiftId, Latitude, Longitude, Accuracy, Speed, Heading, CapturedAtUtc, UpdatedAtUtc)
VALUES (@FollowerId, @FollowerName, @ShiftId, @Latitude, @Longitude, @Accuracy, @Speed, @Heading, @CapturedAtUtc, @UpdatedAtUtc);",
                new
                {
                    FollowerId = followerId,
                    FollowerName = followerName,
                    ShiftId = shiftId,
                    point.Latitude,
                    point.Longitude,
                    point.Accuracy,
                    point.Speed,
                    point.Heading,
                    CapturedAtUtc = point.CapturedAtUtc,
                    UpdatedAtUtc = updatedAtUtc
                }, cancellationToken: ct));
        }

        public async Task EndLiveAsync(int followerId, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.FollowerLiveLocations WHERE FollowerId = @FollowerId",
                new { FollowerId = followerId }, cancellationToken: ct));
        }

        public async Task InsertEventAsync(int followerId, int? shiftId, string eventType, DateTime occurredAtUtc, string? metadata, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            await c.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.FollowerTrackingEvents (FollowerId, ShiftId, EventType, OccurredAtUtc, Metadata)
VALUES (@FollowerId, @ShiftId, @EventType, @OccurredAtUtc, @Metadata);",
                new { FollowerId = followerId, ShiftId = shiftId, EventType = eventType, OccurredAtUtc = occurredAtUtc, Metadata = metadata },
                cancellationToken: ct));
        }

        public async Task<IReadOnlyList<FollowerLiveLocationDto>> ListLiveAsync(CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            var rows = await c.QueryAsync<FollowerLiveLocationDto>(new CommandDefinition(@"
SELECT l.FollowerId, l.FollowerName, l.ShiftId, CAST(l.Latitude AS FLOAT) AS Latitude, CAST(l.Longitude AS FLOAT) AS Longitude,
 l.Accuracy, l.CapturedAtUtc, l.UpdatedAtUtc, N'Active' AS ShiftStatus, N'Live' AS LocationStatus
FROM dbo.FollowerLiveLocations l
INNER JOIN dbo.FollowerWorkShifts s ON s.Id = l.ShiftId AND s.Status = N'Active'
ORDER BY l.UpdatedAtUtc DESC;", cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<IReadOnlyList<FollowerRoutePointDto>> GetRouteAsync(int followerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            var rows = await c.QueryAsync<FollowerRoutePointDto>(new CommandDefinition(@"
SELECT DeviceSequence, CAST(Latitude AS FLOAT) AS Latitude, CAST(Longitude AS FLOAT) AS Longitude,
 COALESCE(OfficialSlotUtc, CapturedAtUtc) AS CapturedAt, IsOfficial
FROM dbo.FollowerLocationPoints
WHERE FollowerId = @FollowerId AND CapturedAtUtc >= @FromUtc AND CapturedAtUtc < @ToUtc
ORDER BY CapturedAtUtc ASC, DeviceSequence ASC;",
                new { FollowerId = followerId, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<FollowerShiftDto?> GetShiftForDayAsync(int followerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            return await c.QueryFirstOrDefaultAsync<FollowerShiftDto>(new CommandDefinition(
                ShiftSelect + @" WHERE FollowerId = @FollowerId AND StartedAtUtc < @ToUtc AND (ClosedAtUtc IS NULL OR ClosedAtUtc >= @FromUtc)
ORDER BY StartedAtUtc DESC",
                new { FollowerId = followerId, FromUtc = fromUtc, ToUtc = toUtc }, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<(int FollowerId, string FollowerName)>> ListFollowersWithShiftsAsync(CancellationToken ct)
        {
            await using var c = (SqlConnection)Conn();
            var rows = await c.QueryAsync(new CommandDefinition(@"
SELECT DISTINCT FollowerId, FollowerName FROM dbo.FollowerWorkShifts ORDER BY FollowerName;", cancellationToken: ct));
            return rows.Select(r => ((int)r.FollowerId, (string)r.FollowerName)).ToList();
        }

        private const string ShiftSelect = @"
SELECT Id AS ShiftId, FollowerId, FollowerName, CityValue, CityName, Status,
 StartedAtUtc, StartedAtIraq, CutoffAtUtc, ClosedAtUtc, CloseReason
FROM dbo.FollowerWorkShifts";

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.FollowerWorkShifts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerWorkShifts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FollowerId INT NOT NULL,
        FollowerName NVARCHAR(200) NULL,
        CityValue NVARCHAR(100) NULL,
        CityName NVARCHAR(200) NULL,
        StartedAtUtc DATETIME NOT NULL,
        StartedAtIraq DATETIME NOT NULL,
        CutoffAtUtc DATETIME NOT NULL,
        ClosedAtUtc DATETIME NULL,
        CloseReason NVARCHAR(50) NULL,
        Status NVARCHAR(20) NOT NULL
    );
    CREATE UNIQUE INDEX UX_FollowerWorkShifts_OneActive ON dbo.FollowerWorkShifts (FollowerId) WHERE Status = N'Active';
END;
IF OBJECT_ID(N'dbo.FollowerLocationPoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerLocationPoints (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FollowerId INT NOT NULL,
        ShiftId INT NOT NULL,
        Latitude DECIMAL(9,6) NOT NULL,
        Longitude DECIMAL(9,6) NOT NULL,
        Accuracy FLOAT NULL,
        Speed FLOAT NULL,
        Heading FLOAT NULL,
        CapturedAtUtc DATETIME NOT NULL,
        ReceivedAtUtc DATETIME NOT NULL,
        DeviceSequence BIGINT NOT NULL,
        IsOfficial BIT NOT NULL CONSTRAINT DF_FollowerLoc_IsOfficial DEFAULT(1),
        OfficialSlotUtc DATETIME NULL,
        ActualCapturedAtUtc DATETIME NULL
    );
    CREATE UNIQUE INDEX UX_FollowerLocationPoints_ShiftSequence ON dbo.FollowerLocationPoints (ShiftId, DeviceSequence);
    CREATE UNIQUE INDEX UX_FollowerLocationPoints_ShiftOfficialSlot ON dbo.FollowerLocationPoints (ShiftId, OfficialSlotUtc) WHERE OfficialSlotUtc IS NOT NULL;
END;
IF OBJECT_ID(N'dbo.FollowerLiveLocations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerLiveLocations (
        FollowerId INT NOT NULL PRIMARY KEY,
        FollowerName NVARCHAR(200) NULL,
        ShiftId INT NOT NULL,
        Latitude DECIMAL(9,6) NOT NULL,
        Longitude DECIMAL(9,6) NOT NULL,
        Accuracy FLOAT NULL,
        Speed FLOAT NULL,
        Heading FLOAT NULL,
        CapturedAtUtc DATETIME NOT NULL,
        UpdatedAtUtc DATETIME NOT NULL
    );
END;
IF OBJECT_ID(N'dbo.FollowerTrackingEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerTrackingEvents (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FollowerId INT NOT NULL,
        ShiftId INT NULL,
        EventType NVARCHAR(80) NOT NULL,
        OccurredAtUtc DATETIME NOT NULL,
        Metadata NVARCHAR(1000) NULL
    );
END;";
    }
}
