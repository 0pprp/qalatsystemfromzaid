using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class BmRatingDeclineAlertDto
    {
        public int Id { get; set; }
        public string SubjectType { get; set; } = "";
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = "";
        public double FromScore { get; set; }
        public double ToScore { get; set; }
        public double DeclineAbsolute { get; set; }
        public DateTime ReferenceCapturedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAtUtc { get; set; }
        public string Message { get; set; } = "";
    }

    public interface IBmRatingDeclineAlertService
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task RefreshAsync(CancellationToken ct = default);
        Task<IReadOnlyList<BmRatingDeclineAlertDto>> ListAsync(bool unreadOnly, CancellationToken ct = default);
        Task<BmRatingDeclineAlertDto?> GetAsync(int id, CancellationToken ct = default);
        Task<bool> MarkReadAsync(int id, CancellationToken ct = default);
    }

    /// <summary>
    /// Daily rating snapshots for followers + sales employees; unread alerts when score drops ≥ 0.3 vs ~7 days ago.
    /// </summary>
    public sealed class BmRatingDeclineAlertService : IBmRatingDeclineAlertService
    {
        public const int LookbackDays = 7;
        public const double Threshold = BranchMonthStatsService.DeclineAlertThreshold;

        private readonly SalesDevelopmentGuard _guard;
        private readonly IBranchManagerStatsService _stats;
        private static int _schemaReady;

        public BmRatingDeclineAlertService(
            SalesDevelopmentGuard guard,
            IBranchManagerStatsService stats)
        {
            _guard = guard;
            _stats = stats;
        }

        public static bool IsDecline(double oldScore, double newScore, double threshold = Threshold)
            => oldScore - newScore >= threshold - 1e-9;

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            if (Volatile.Read(ref _schemaReady) == 1)
            {
                return;
            }

            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales connection string missing.");
            await using var c = new SqlConnection(cs);
            await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.BmRatingSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmRatingSnapshots (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BmRatingSnapshots PRIMARY KEY,
        SubjectType NVARCHAR(32) NOT NULL,
        SubjectId INT NOT NULL,
        SubjectName NVARCHAR(200) NULL,
        Score FLOAT NOT NULL,
        CapturedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_BmRatingSnapshots_CapturedAtUtc DEFAULT (SYSUTCDATETIME()),
        CaptureDate DATE NOT NULL
    );
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_BmRatingSnapshots_Subject_Day'
      AND object_id = OBJECT_ID(N'dbo.BmRatingSnapshots')
)
    CREATE UNIQUE INDEX UQ_BmRatingSnapshots_Subject_Day
        ON dbo.BmRatingSnapshots (SubjectType, SubjectId, CaptureDate);

IF OBJECT_ID(N'dbo.BmRatingDeclineAlerts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmRatingDeclineAlerts (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BmRatingDeclineAlerts PRIMARY KEY,
        SubjectType NVARCHAR(32) NOT NULL,
        SubjectId INT NOT NULL,
        SubjectName NVARCHAR(200) NULL,
        FromScore FLOAT NOT NULL,
        ToScore FLOAT NOT NULL,
        DeclineAbsolute FLOAT NOT NULL,
        ReferenceCapturedAtUtc DATETIME2 NOT NULL,
        ReferenceCaptureDate DATE NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_BmRatingDeclineAlerts_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        IsRead BIT NOT NULL CONSTRAINT DF_BmRatingDeclineAlerts_IsRead DEFAULT (0),
        ReadAtUtc DATETIME2 NULL
    );
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_BmRatingDeclineAlerts_Subject_RefDay'
      AND object_id = OBJECT_ID(N'dbo.BmRatingDeclineAlerts')
)
    CREATE UNIQUE INDEX UQ_BmRatingDeclineAlerts_Subject_RefDay
        ON dbo.BmRatingDeclineAlerts (SubjectType, SubjectId, ReferenceCaptureDate);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_BmRatingDeclineAlerts_IsRead_Created'
      AND object_id = OBJECT_ID(N'dbo.BmRatingDeclineAlerts')
)
    CREATE INDEX IX_BmRatingDeclineAlerts_IsRead_Created
        ON dbo.BmRatingDeclineAlerts (IsRead, CreatedAtUtc DESC);
", cancellationToken: ct));
            Volatile.Write(ref _schemaReady, 1);
        }

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var catalog = await _stats.GetCatalogAsync(ct);
            var followerRows = catalog.Followers
                .Where(f => f.Id > 0)
                .Select(f => new SalesManagerFollowerDTO
                {
                    FollowerId = f.Id,
                    FollowerName = f.Name ?? ""
                })
                .ToList();
            var employeeRows = catalog.Employees
                .Where(e => e.Id > 0)
                .Select(e => new SalesManagerEmployeeDTO
                {
                    EmployeeId = e.Id,
                    EmployeeName = e.Name ?? ""
                })
                .ToList();

            await _stats.AttachFollowerLatestRatingsAsync(followerRows, ct);
            await _stats.AttachEmployeeLatestRatingsAsync(employeeRows, ct);

            var nowUtc = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(nowUtc);
            var subjects = new List<(string Type, int Id, string Name, double Score)>();
            foreach (var f in followerRows)
            {
                if (f.LatestRating is double score)
                {
                    subjects.Add(("follower", f.FollowerId, f.FollowerName, score));
                }
            }

            foreach (var e in employeeRows)
            {
                if (e.LatestRating is double score)
                {
                    subjects.Add(("employee", e.EmployeeId, e.EmployeeName, score));
                }
            }

            var cs = _guard.GetSalesConnectionString()!;
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);

            foreach (var s in subjects)
            {
                ct.ThrowIfCancellationRequested();
                await UpsertSnapshotAsync(connection, s.Type, s.Id, s.Name, s.Score, nowUtc, today, ct);
                await MaybeCreateAlertAsync(connection, s.Type, s.Id, s.Name, s.Score, nowUtc, today, ct);
            }
        }

        private static async Task UpsertSnapshotAsync(
            SqlConnection connection,
            string subjectType,
            int subjectId,
            string subjectName,
            double score,
            DateTime capturedAtUtc,
            DateOnly captureDate,
            CancellationToken ct)
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.BmRatingSnapshots AS t
USING (SELECT @SubjectType AS SubjectType, @SubjectId AS SubjectId, @CaptureDate AS CaptureDate) AS s
ON t.SubjectType = s.SubjectType AND t.SubjectId = s.SubjectId AND t.CaptureDate = s.CaptureDate
WHEN MATCHED THEN UPDATE SET
    Score = @Score,
    SubjectName = @SubjectName,
    CapturedAtUtc = @CapturedAtUtc
WHEN NOT MATCHED THEN INSERT (SubjectType, SubjectId, SubjectName, Score, CapturedAtUtc, CaptureDate)
VALUES (@SubjectType, @SubjectId, @SubjectName, @Score, @CapturedAtUtc, @CaptureDate);",
                new
                {
                    SubjectType = subjectType,
                    SubjectId = subjectId,
                    SubjectName = subjectName,
                    Score = score,
                    CapturedAtUtc = capturedAtUtc,
                    CaptureDate = captureDate.ToDateTime(TimeOnly.MinValue)
                },
                cancellationToken: ct));
        }

        private static async Task MaybeCreateAlertAsync(
            SqlConnection connection,
            string subjectType,
            int subjectId,
            string subjectName,
            double newScore,
            DateTime nowUtc,
            DateOnly today,
            CancellationToken ct)
        {
            var targetDate = today.AddDays(-LookbackDays);
            var windowFrom = targetDate.AddDays(-1);
            var windowTo = targetDate.AddDays(1);

            var prior = await connection.QueryFirstOrDefaultAsync<PriorSnapshotRow>(
                new CommandDefinition(@"
SELECT TOP 1 Score, CapturedAtUtc, CaptureDate
FROM dbo.BmRatingSnapshots
WHERE SubjectType = @SubjectType
  AND SubjectId = @SubjectId
  AND CaptureDate >= @WindowFrom
  AND CaptureDate <= @WindowTo
ORDER BY ABS(DATEDIFF(day, CaptureDate, @TargetDate)), CapturedAtUtc DESC;",
                    new
                    {
                        SubjectType = subjectType,
                        SubjectId = subjectId,
                        WindowFrom = windowFrom.ToDateTime(TimeOnly.MinValue),
                        WindowTo = windowTo.ToDateTime(TimeOnly.MinValue),
                        TargetDate = targetDate.ToDateTime(TimeOnly.MinValue)
                    },
                    cancellationToken: ct));

            if (prior is null)
            {
                return;
            }

            var oldScore = prior.Score;
            if (!IsDecline(oldScore, newScore))
            {
                return;
            }

            var decline = Math.Round(oldScore - newScore, 2, MidpointRounding.AwayFromZero);
            var refDate = DateOnly.FromDateTime(prior.CaptureDate);
            await connection.ExecuteAsync(new CommandDefinition(@"
IF NOT EXISTS (
    SELECT 1 FROM dbo.BmRatingDeclineAlerts
    WHERE SubjectType = @SubjectType
      AND SubjectId = @SubjectId
      AND ReferenceCaptureDate = @ReferenceCaptureDate
)
INSERT INTO dbo.BmRatingDeclineAlerts (
    SubjectType, SubjectId, SubjectName, FromScore, ToScore, DeclineAbsolute,
    ReferenceCapturedAtUtc, ReferenceCaptureDate, CreatedAtUtc, IsRead
)
VALUES (
    @SubjectType, @SubjectId, @SubjectName, @FromScore, @ToScore, @DeclineAbsolute,
    @ReferenceCapturedAtUtc, @ReferenceCaptureDate, @CreatedAtUtc, 0
);",
                new
                {
                    SubjectType = subjectType,
                    SubjectId = subjectId,
                    SubjectName = subjectName,
                    FromScore = oldScore,
                    ToScore = newScore,
                    DeclineAbsolute = decline,
                    ReferenceCapturedAtUtc = prior.CapturedAtUtc,
                    ReferenceCaptureDate = refDate.ToDateTime(TimeOnly.MinValue),
                    CreatedAtUtc = nowUtc
                },
                cancellationToken: ct));
        }

        public async Task<IReadOnlyList<BmRatingDeclineAlertDto>> ListAsync(bool unreadOnly, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var cs = _guard.GetSalesConnectionString()!;
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<BmRatingDeclineAlertRow>(new CommandDefinition(@"
SELECT Id, SubjectType, SubjectId, SubjectName, FromScore, ToScore, DeclineAbsolute,
       ReferenceCapturedAtUtc, CreatedAtUtc, IsRead, ReadAtUtc
FROM dbo.BmRatingDeclineAlerts
WHERE (@UnreadOnly = 0 OR IsRead = 0)
ORDER BY IsRead ASC, CreatedAtUtc DESC;",
                new { UnreadOnly = unreadOnly ? 1 : 0 },
                cancellationToken: ct));
            return rows.Select(Map).ToList();
        }

        public async Task<BmRatingDeclineAlertDto?> GetAsync(int id, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var cs = _guard.GetSalesConnectionString()!;
            await using var connection = new SqlConnection(cs);
            var row = await connection.QueryFirstOrDefaultAsync<BmRatingDeclineAlertRow>(new CommandDefinition(@"
SELECT Id, SubjectType, SubjectId, SubjectName, FromScore, ToScore, DeclineAbsolute,
       ReferenceCapturedAtUtc, CreatedAtUtc, IsRead, ReadAtUtc
FROM dbo.BmRatingDeclineAlerts
WHERE Id = @Id;",
                new { Id = id },
                cancellationToken: ct));
            return row == null ? null : Map(row);
        }

        public async Task<bool> MarkReadAsync(int id, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var cs = _guard.GetSalesConnectionString()!;
            await using var connection = new SqlConnection(cs);
            var n = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.BmRatingDeclineAlerts
SET IsRead = 1, ReadAtUtc = SYSUTCDATETIME()
WHERE Id = @Id AND IsRead = 0;",
                new { Id = id },
                cancellationToken: ct));
            if (n > 0)
            {
                return true;
            }

            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.BmRatingDeclineAlerts WHERE Id = @Id) THEN 1 ELSE 0 END",
                new { Id = id },
                cancellationToken: ct));
            return exists == 1;
        }

        private static BmRatingDeclineAlertDto Map(BmRatingDeclineAlertRow r)
        {
            var name = string.IsNullOrWhiteSpace(r.SubjectName) ? "—" : r.SubjectName.Trim();
            var from = Math.Round(r.FromScore, 2, MidpointRounding.AwayFromZero);
            var to = Math.Round(r.ToScore, 2, MidpointRounding.AwayFromZero);
            return new BmRatingDeclineAlertDto
            {
                Id = r.Id,
                SubjectType = r.SubjectType,
                SubjectId = r.SubjectId,
                SubjectName = name,
                FromScore = from,
                ToScore = to,
                DeclineAbsolute = Math.Round(r.DeclineAbsolute, 2, MidpointRounding.AwayFromZero),
                ReferenceCapturedAtUtc = r.ReferenceCapturedAtUtc,
                CreatedAtUtc = r.CreatedAtUtc,
                IsRead = r.IsRead,
                ReadAtUtc = r.ReadAtUtc,
                Message = $"نزل {name} من {from:0.##} إلى {to:0.##}"
            };
        }

        private sealed class PriorSnapshotRow
        {
            public double Score { get; set; }
            public DateTime CapturedAtUtc { get; set; }
            public DateTime CaptureDate { get; set; }
        }

        private sealed class BmRatingDeclineAlertRow
        {
            public int Id { get; set; }
            public string SubjectType { get; set; } = "";
            public int SubjectId { get; set; }
            public string? SubjectName { get; set; }
            public double FromScore { get; set; }
            public double ToScore { get; set; }
            public double DeclineAbsolute { get; set; }
            public DateTime ReferenceCapturedAtUtc { get; set; }
            public DateTime CreatedAtUtc { get; set; }
            public bool IsRead { get; set; }
            public DateTime? ReadAtUtc { get; set; }
        }
    }
}
