using System.Collections.Concurrent;
using BE_DelegateWebApplication.Services.FollowerRating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public interface IFollowerListCardsService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<FollowerListCardDto>> GetListCardsAsync(
        int followerUserId,
        DateTime nowLocal,
        CancellationToken ct = default);

    Task<IReadOnlyList<FollowerCollectionListDto>> GetCollectionListsAsync(
        int followerUserId,
        CancellationToken ct = default);
}

public sealed class FollowerListCardsService : IFollowerListCardsService
{
    private readonly string _cs;
    private static int _schemaReady;
    private const int MaxParallelLists = 4;

    public FollowerListCardsService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await using var c = new SqlConnection(_cs);
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerListRatingSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerListRatingSnapshots (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerListRatingSnapshots PRIMARY KEY,
        ListId INT NOT NULL,
        AverageScore FLOAT NOT NULL,
        RatingLabel NVARCHAR(32) NOT NULL,
        RatedCustomerCount INT NOT NULL CONSTRAINT DF_FLRS_Count DEFAULT (0),
        CapturedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FLRS_Captured DEFAULT (SYSUTCDATETIME()),
        CaptureDate DATE NOT NULL
    );
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_FollowerListRatingSnapshots_List_Day'
      AND object_id = OBJECT_ID(N'dbo.FollowerListRatingSnapshots')
)
    CREATE UNIQUE INDEX UQ_FollowerListRatingSnapshots_List_Day
        ON dbo.FollowerListRatingSnapshots (ListId, CaptureDate);
IF COL_LENGTH(N'dbo.Delegates', N'FollowerCollectionEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Delegates ADD FollowerCollectionEnabled BIT NOT NULL
        CONSTRAINT DF_Delegates_FollowerCollectionEnabled_Cards DEFAULT (0);
END
", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task<IReadOnlyList<FollowerListCardDto>> GetListCardsAsync(
        int followerUserId,
        DateTime nowLocal,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var asOf = nowLocal.Date;
        var weekAgo = FollowerListTrendRules.WeekAgoDate(asOf);
        var monthAgo = FollowerListTrendRules.MonthAgoDate(asOf);
        var ratingCutoff = asOf.AddDays(-30);

        List<(int ListId, string? ListName, bool CollectionEnabled)> lists;
        await using (var c = new SqlConnection(_cs))
        {
            lists = (await c.QueryAsync<(int ListId, string? ListName, bool CollectionEnabled)>(
                new CommandDefinition(@"
SELECT
    ful.ListId,
    d.DelegateName AS ListName,
    CAST(ISNULL(d.FollowerCollectionEnabled, 0) AS BIT) AS CollectionEnabled
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Delegates d ON d.DelegateID = ful.ListId
WHERE ful.UserId = @FollowerUserId
ORDER BY d.DelegateName;",
                    new { FollowerUserId = followerUserId },
                    cancellationToken: ct))).ToList();
        }

        if (lists.Count == 0)
            return Array.Empty<FollowerListCardDto>();

        var bag = new ConcurrentBag<(int Order, FollowerListCardDto Card)>();
        await Parallel.ForEachAsync(
            lists.Select((list, index) => (list, index)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(MaxParallelLists, lists.Count),
                CancellationToken = ct
            },
            async (item, token) =>
            {
                var list = item.list;
                await using var conn = new SqlConnection(_cs);
                await conn.OpenAsync(token);

                var facts = await LoadRatingFactsFastAsync(conn, list.ListId, token);
                var rated = facts
                    .Where(f => f.AmountRemaining > 0
                                && f.LastPaymentDate is DateTime lp
                                && lp.Date >= ratingCutoff)
                    .Select(f => FollowerCustomerRatingCalculator.Evaluate(f, asOf))
                    .ToList();
                var agg = FollowerListRatingAggregator.Aggregate(rated);
                await UpsertListSnapshotAsync(
                    conn, list.ListId, agg.AverageScore, agg.FinalRatingLabel, agg.CustomerCount, asOf, token);

                var weekAgoScore = await LoadSnapshotScoreAsync(conn, list.ListId, weekAgo, token);
                var monthAgoScore = await LoadSnapshotScoreAsync(conn, list.ListId, monthAgo, token);

                bag.Add((item.index, new FollowerListCardDto
                {
                    ListId = list.ListId,
                    ListName = list.ListName,
                    Score = agg.AverageScore,
                    RatingLabel = agg.FinalRatingLabel,
                    ScoreWeekAgo = weekAgoScore,
                    Trend = FollowerListTrendRules.ComputeTrend(agg.AverageScore, weekAgoScore),
                    ScoreMonthAgo = monthAgoScore,
                    MonthTrend = FollowerListTrendRules.ComputeTrend(agg.AverageScore, monthAgoScore),
                    FollowerCollectionEnabled = list.CollectionEnabled
                }));
            });

        return bag.OrderBy(x => x.Order).Select(x => x.Card).ToList();
    }

    public async Task<IReadOnlyList<FollowerCollectionListDto>> GetCollectionListsAsync(
        int followerUserId,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        var rows = await c.QueryAsync<FollowerCollectionListDto>(new CommandDefinition(@"
SELECT
    ful.ListId,
    d.DelegateName AS ListName,
    CAST(ISNULL(d.FollowerCollectionEnabled, 0) AS BIT) AS FollowerCollectionEnabled
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Delegates d ON d.DelegateID = ful.ListId
WHERE ful.UserId = @FollowerUserId
ORDER BY d.DelegateName;",
            new { FollowerUserId = followerUserId },
            cancellationToken: ct));
        return rows.ToList();
    }

    private static async Task UpsertListSnapshotAsync(
        SqlConnection c,
        int listId,
        double averageScore,
        string ratingLabel,
        int ratedCount,
        DateTime captureDate,
        CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.FollowerListRatingSnapshots AS t
USING (SELECT @ListId AS ListId, @CaptureDate AS CaptureDate) AS s
ON t.ListId = s.ListId AND t.CaptureDate = s.CaptureDate
WHEN MATCHED THEN UPDATE SET
    AverageScore = @AverageScore,
    RatingLabel = @RatingLabel,
    RatedCustomerCount = @RatedCustomerCount,
    CapturedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (ListId, AverageScore, RatingLabel, RatedCustomerCount, CapturedAtUtc, CaptureDate)
VALUES
    (@ListId, @AverageScore, @RatingLabel, @RatedCustomerCount, SYSUTCDATETIME(), @CaptureDate);",
            new
            {
                ListId = listId,
                AverageScore = averageScore,
                RatingLabel = ratingLabel,
                RatedCustomerCount = ratedCount,
                CaptureDate = captureDate.Date
            },
            cancellationToken: ct));
    }

    /// <summary>
    /// Prefer exact CaptureDate; else nearest prior within 3 days (covers missed snapshot days).
    /// </summary>
    internal static async Task<double?> LoadSnapshotScoreAsync(
        SqlConnection c, int listId, DateTime captureDate, CancellationToken ct)
    {
        var exact = await c.ExecuteScalarAsync<double?>(new CommandDefinition(@"
SELECT TOP 1 AverageScore
FROM dbo.FollowerListRatingSnapshots
WHERE ListId = @ListId AND CaptureDate = @CaptureDate;",
            new { ListId = listId, CaptureDate = captureDate.Date },
            cancellationToken: ct));
        if (exact is not null) return exact;

        return await c.ExecuteScalarAsync<double?>(new CommandDefinition(@"
SELECT TOP 1 AverageScore
FROM dbo.FollowerListRatingSnapshots
WHERE ListId = @ListId
  AND CaptureDate >= DATEADD(day, -3, @CaptureDate)
  AND CaptureDate <= @CaptureDate
ORDER BY CaptureDate DESC;",
            new { ListId = listId, CaptureDate = captureDate.Date },
            cancellationToken: ct));
    }

    /// <summary>
    /// Fast path via View_Customers (no per-row OUTER APPLY). Falls back to detailed joins if view shape fails.
    /// </summary>
    private static async Task<List<FollowerCustomerRatingFacts>> LoadRatingFactsFastAsync(
        SqlConnection c, int listId, CancellationToken ct)
    {
        try
        {
            var rows = await c.QueryAsync<dynamic>(new CommandDefinition(@"
SELECT
    C.CustomerID AS CustomerId,
    C.DelegateID AS DelegateId,
    ISNULL(VC.CustomerName, C.CustomerName) AS CustomerName,
    ISNULL(VC.PhoneNumber, C.PhoneNumber) AS PhoneNumber,
    CAST(ISNULL(C.IsLegal, 0) AS bit) AS IsLegal,
    VC.DateSaleDevice AS DateSaleDevice,
    VC.LastPaymentDate AS LastPaymentDate,
    CAST(ISNULL(VC.AmountTotalSales, 0) AS float) AS AmountTotalSales,
    CAST(ISNULL(VC.ReceiptsTotal, 0) AS float) AS ReceiptsTotal,
    CAST(ISNULL(VC.AmountRemaining, 0) AS float) AS AmountRemaining,
    CAST(ISNULL(VC.AmountDaySales, 0) AS float) AS AmountDaySales
FROM dbo.Customers C
LEFT JOIN dbo.View_Customers VC ON VC.CustomerID = C.CustomerID
WHERE C.DelegateID = @ListId;",
                new { ListId = listId },
                cancellationToken: ct));
            return MapFacts(rows);
        }
        catch (SqlException)
        {
            var rows = await c.QueryAsync<dynamic>(new CommandDefinition(@"
SELECT
    C.CustomerID AS CustomerId,
    C.DelegateID AS DelegateId,
    C.CustomerName AS CustomerName,
    C.PhoneNumber AS PhoneNumber,
    CAST(0 AS bit) AS IsLegal,
    S.DateSaleDevice AS DateSaleDevice,
    P.LastPaymentDate AS LastPaymentDate,
    CAST(ISNULL(S.AmountTotalSales, 0) AS float) AS AmountTotalSales,
    CAST(ISNULL(P.ReceiptsTotal, 0) AS float) AS ReceiptsTotal,
    CAST(ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) AS float) AS AmountRemaining,
    CAST(0 AS float) AS AmountDaySales
FROM dbo.Customers C
OUTER APPLY (
    SELECT
        MAX(V.DateCreate) AS DateSaleDevice,
        ROUND(ISNULL(SUM(V.AmountTotalSalesDenar), 0), -3) AS AmountTotalSales
    FROM dbo.View_CustomersSalesDelegate V
    WHERE V.CustomerID = C.CustomerID
) S
OUTER APPLY (
    SELECT
        MAX(V.PaymentDate) AS LastPaymentDate,
        ROUND(ISNULL(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal
    FROM dbo.View_CustomersPaymentsDelegate V
    WHERE V.CustomerID = C.CustomerID
) P
WHERE C.DelegateID = @ListId;",
                new { ListId = listId },
                cancellationToken: ct));
            return MapFacts(rows);
        }
    }

    private static List<FollowerCustomerRatingFacts> MapFacts(IEnumerable<dynamic> rows)
    {
        var list = new List<FollowerCustomerRatingFacts>();
        foreach (var r in rows)
        {
            list.Add(new FollowerCustomerRatingFacts(
                (int)r.CustomerId,
                r.DelegateId == null ? null : (int?)r.DelegateId,
                r.CustomerName as string,
                r.PhoneNumber as string,
                r.IsLegal != null && (bool)r.IsLegal,
                r.DateSaleDevice as DateTime?,
                r.LastPaymentDate as DateTime?,
                (double)(r.AmountTotalSales ?? 0d),
                (double)(r.ReceiptsTotal ?? 0d),
                (double)(r.AmountRemaining ?? 0d),
                (double)(r.AmountDaySales ?? 0d)));
        }

        return list;
    }
}
