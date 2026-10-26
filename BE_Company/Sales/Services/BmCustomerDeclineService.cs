using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class BmCustomerDeclineDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public string? DelegateName { get; set; }
        public string FromLabel { get; set; } = CustomerRatingLabels.Excellent;
        public string ToLabel { get; set; } = "";
        public int ToScore { get; set; }
        public DateTime PriorExcellentDate { get; set; }
        public DateTime CurrentCaptureDate { get; set; }
        public int DaysBetween { get; set; }
    }

    public interface IBmCustomerDeclineService
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task RefreshAsync(CancellationToken ct = default);
        Task<IReadOnlyList<BmCustomerDeclineDto>> ListDecliningAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// BM-only: customers whose rating label dropped from ممتاز to سيء/متلكئ within 15 days.
    /// Isolated under branch-manager; does not change shared rating APIs.
    /// </summary>
    public sealed class BmCustomerDeclineService : IBmCustomerDeclineService
    {
        public const int LookbackDays = 15;

        private readonly SalesDevelopmentGuard _guard;
        private readonly IBranchManagerStatsService _stats;
        private readonly IRatingDataSource _rating;
        private static int _schemaReady;

        public BmCustomerDeclineService(
            SalesDevelopmentGuard guard,
            IBranchManagerStatsService stats,
            IRatingDataSource rating)
        {
            _guard = guard;
            _stats = stats;
            _rating = rating;
        }

        public static bool IsTargetDecline(string? fromLabel, string? toLabel)
        {
            if (!string.Equals(fromLabel?.Trim(), CustomerRatingLabels.Excellent, StringComparison.Ordinal))
            {
                return false;
            }

            var to = toLabel?.Trim() ?? "";
            return string.Equals(to, CustomerRatingLabels.Bad, StringComparison.Ordinal)
                   || string.Equals(to, CustomerRatingLabels.Rejected, StringComparison.Ordinal);
        }

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
IF OBJECT_ID(N'dbo.BmCustomerRatingSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmCustomerRatingSnapshots (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BmCustomerRatingSnapshots PRIMARY KEY,
        CustomerId INT NOT NULL,
        CustomerName NVARCHAR(200) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        DelegateId INT NULL,
        RatingLabel NVARCHAR(32) NOT NULL,
        Score INT NOT NULL,
        CapturedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_BmCustomerRatingSnapshots_CapturedAtUtc DEFAULT (SYSUTCDATETIME()),
        CaptureDate DATE NOT NULL
    );
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_BmCustomerRatingSnapshots_Customer_Day'
      AND object_id = OBJECT_ID(N'dbo.BmCustomerRatingSnapshots')
)
    CREATE UNIQUE INDEX UQ_BmCustomerRatingSnapshots_Customer_Day
        ON dbo.BmCustomerRatingSnapshots (CustomerId, CaptureDate);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_BmCustomerRatingSnapshots_Label_Day'
      AND object_id = OBJECT_ID(N'dbo.BmCustomerRatingSnapshots')
)
    CREATE INDEX IX_BmCustomerRatingSnapshots_Label_Day
        ON dbo.BmCustomerRatingSnapshots (RatingLabel, CaptureDate);
", cancellationToken: ct));
            Volatile.Write(ref _schemaReady, 1);
        }

        public async Task RefreshAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var catalog = await _stats.GetCatalogAsync(ct);
            var listIds = catalog.Lists.Where(l => l.Id > 0).Select(l => l.Id).Distinct().ToList();
            if (listIds.Count == 0)
            {
                return;
            }

            var facts = ListFollowerRatingCohort.OpenBalanceOnly(
                await _rating.GetFactsByListIdsAsync(listIds, ct));
            if (facts.Count == 0)
            {
                return;
            }

            var asOf = DateTime.UtcNow.AddHours(3).Date;
            var rated = CustomerRatingCalculator.EvaluateMany(facts, asOf);
            var names = await LoadCustomerNamesAsync(facts.Select(f => f.CustomerId).ToList(), ct);

            var nowUtc = DateTime.UtcNow;
            var captureDate = DateOnly.FromDateTime(asOf);
            var cs = _guard.GetSalesConnectionString()!;
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);

            for (var i = 0; i < facts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var fact = facts[i];
                var r = rated[i];
                if (r.IsSettled)
                {
                    continue;
                }

                names.TryGetValue(fact.CustomerId, out var info);
                await UpsertSnapshotAsync(
                    connection,
                    fact.CustomerId,
                    info.Name ?? "",
                    info.Phone,
                    fact.DelegateId,
                    r.RatingLabel,
                    r.Score,
                    nowUtc,
                    captureDate,
                    ct);
            }
        }

        public async Task<IReadOnlyList<BmCustomerDeclineDto>> ListDecliningAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var cs = _guard.GetSalesConnectionString()!;
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<DeclineRow>(new CommandDefinition(@"
DECLARE @Today DATE = CAST(DATEADD(HOUR, 3, SYSUTCDATETIME()) AS DATE);
DECLARE @From DATE = DATEADD(DAY, -@LookbackDays, @Today);

;WITH Latest AS (
    SELECT
        s.CustomerId,
        s.CustomerName,
        s.PhoneNumber,
        s.DelegateId,
        s.RatingLabel AS ToLabel,
        s.Score AS ToScore,
        s.CaptureDate AS CurrentCaptureDate,
        ROW_NUMBER() OVER (PARTITION BY s.CustomerId ORDER BY s.CaptureDate DESC, s.CapturedAtUtc DESC) AS rn
    FROM dbo.BmCustomerRatingSnapshots s
),
CurrentBad AS (
    SELECT *
    FROM Latest
    WHERE rn = 1
      AND ToLabel IN (N'سيء', N'متلكئ')
),
PriorExcellent AS (
    SELECT
        s.CustomerId,
        MAX(s.CaptureDate) AS PriorExcellentDate
    FROM dbo.BmCustomerRatingSnapshots s
    INNER JOIN CurrentBad c ON c.CustomerId = s.CustomerId
    WHERE s.RatingLabel = N'ممتاز'
      AND s.CaptureDate >= @From
      AND s.CaptureDate <= c.CurrentCaptureDate
    GROUP BY s.CustomerId
)
SELECT
    c.CustomerId,
    COALESCE(NULLIF(LTRIM(RTRIM(c.CustomerName)), N''), cust.CustomerName, N'') AS CustomerName,
    COALESCE(NULLIF(LTRIM(RTRIM(c.PhoneNumber)), N''), cust.PhoneNumber) AS PhoneNumber,
    d.DelegateName,
    N'ممتاز' AS FromLabel,
    c.ToLabel,
    c.ToScore,
    p.PriorExcellentDate,
    c.CurrentCaptureDate,
    DATEDIFF(DAY, p.PriorExcellentDate, c.CurrentCaptureDate) AS DaysBetween
FROM CurrentBad c
INNER JOIN PriorExcellent p ON p.CustomerId = c.CustomerId
LEFT JOIN dbo.Customers cust WITH (NOLOCK) ON cust.CustomerID = c.CustomerId
LEFT JOIN dbo.Delegates d WITH (NOLOCK) ON d.DelegateID = COALESCE(c.DelegateId, cust.DelegateID)
WHERE DATEDIFF(DAY, p.PriorExcellentDate, c.CurrentCaptureDate) BETWEEN 0 AND @LookbackDays
ORDER BY c.CurrentCaptureDate DESC, c.CustomerId;",
                new { LookbackDays },
                cancellationToken: ct));

            return rows.Select(r => new BmCustomerDeclineDto
            {
                CustomerId = r.CustomerId,
                CustomerName = r.CustomerName ?? "",
                PhoneNumber = r.PhoneNumber,
                DelegateName = r.DelegateName,
                FromLabel = r.FromLabel ?? CustomerRatingLabels.Excellent,
                ToLabel = r.ToLabel ?? "",
                ToScore = r.ToScore,
                PriorExcellentDate = r.PriorExcellentDate,
                CurrentCaptureDate = r.CurrentCaptureDate,
                DaysBetween = r.DaysBetween
            }).ToList();
        }

        private static async Task UpsertSnapshotAsync(
            SqlConnection connection,
            int customerId,
            string customerName,
            string? phoneNumber,
            int? delegateId,
            string ratingLabel,
            int score,
            DateTime capturedAtUtc,
            DateOnly captureDate,
            CancellationToken ct)
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.BmCustomerRatingSnapshots AS t
USING (SELECT @CustomerId AS CustomerId, @CaptureDate AS CaptureDate) AS s
ON t.CustomerId = s.CustomerId AND t.CaptureDate = s.CaptureDate
WHEN MATCHED THEN UPDATE SET
    CustomerName = @CustomerName,
    PhoneNumber = @PhoneNumber,
    DelegateId = @DelegateId,
    RatingLabel = @RatingLabel,
    Score = @Score,
    CapturedAtUtc = @CapturedAtUtc
WHEN NOT MATCHED THEN INSERT (
    CustomerId, CustomerName, PhoneNumber, DelegateId, RatingLabel, Score, CapturedAtUtc, CaptureDate
) VALUES (
    @CustomerId, @CustomerName, @PhoneNumber, @DelegateId, @RatingLabel, @Score, @CapturedAtUtc, @CaptureDate
);",
                new
                {
                    CustomerId = customerId,
                    CustomerName = Truncate(customerName, 200),
                    PhoneNumber = Truncate(phoneNumber, 50),
                    DelegateId = delegateId,
                    RatingLabel = Truncate(ratingLabel, 32) ?? "",
                    Score = score,
                    CapturedAtUtc = capturedAtUtc,
                    CaptureDate = captureDate.ToDateTime(TimeOnly.MinValue)
                },
                cancellationToken: ct));
        }

        private async Task<Dictionary<int, (string? Name, string? Phone)>> LoadCustomerNamesAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct)
        {
            var result = new Dictionary<int, (string? Name, string? Phone)>();
            if (customerIds.Count == 0)
            {
                return result;
            }

            var cs = _guard.GetSalesConnectionString()!;
            await using var connection = new SqlConnection(cs);
            foreach (var chunk in Chunk(customerIds, 500))
            {
                ct.ThrowIfCancellationRequested();
                var rows = await connection.QueryAsync<(int CustomerId, string? CustomerName, string? PhoneNumber)>(
                    new CommandDefinition(@"
SELECT CustomerID AS CustomerId, CustomerName, PhoneNumber
FROM dbo.Customers WITH (NOLOCK)
WHERE CustomerID IN @Ids;",
                        new { Ids = chunk },
                        cancellationToken: ct));
                foreach (var row in rows)
                {
                    result[row.CustomerId] = (row.CustomerName, row.PhoneNumber);
                }
            }

            return result;
        }

        private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
        {
            for (var i = 0; i < source.Count; i += size)
            {
                var take = Math.Min(size, source.Count - i);
                var list = new List<T>(take);
                for (var j = 0; j < take; j++)
                {
                    list.Add(source[i + j]);
                }

                yield return list;
            }
        }

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= max ? value : value[..max];
        }

        private sealed class DeclineRow
        {
            public int CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? DelegateName { get; set; }
            public string? FromLabel { get; set; }
            public string? ToLabel { get; set; }
            public int ToScore { get; set; }
            public DateTime PriorExcellentDate { get; set; }
            public DateTime CurrentCaptureDate { get; set; }
            public int DaysBetween { get; set; }
        }
    }
}
