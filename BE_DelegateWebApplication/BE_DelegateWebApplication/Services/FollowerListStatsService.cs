using BE_DelegateWebApplication.Services.FollowerRating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public sealed class FollowerListStatsService : IFollowerListStatsService
{
    private readonly string _cs;
    private static int _schemaReady;

    public FollowerListStatsService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await using var c = new SqlConnection(_cs);
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerListMonthlySnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerListMonthlySnapshots (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerListMonthlySnapshots PRIMARY KEY,
        ListId INT NOT NULL,
        YearMonth INT NOT NULL,
        TotalSalesSum DECIMAL(18,2) NOT NULL CONSTRAINT DF_FLMS_Total DEFAULT (0),
        DaySalesSum DECIMAL(18,2) NOT NULL CONSTRAINT DF_FLMS_Day DEFAULT (0),
        NewCustomersCount INT NOT NULL CONSTRAINT DF_FLMS_NewCust DEFAULT (0),
        CapturedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FLMS_Captured DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE INDEX UX_FollowerListMonthlySnapshots_List_YM
        ON dbo.FollowerListMonthlySnapshots (ListId, YearMonth);
END
IF OBJECT_ID(N'dbo.FollowerListRatingSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerListRatingSnapshots (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerListRatingSnapshots_Stats PRIMARY KEY,
        ListId INT NOT NULL,
        AverageScore FLOAT NOT NULL,
        RatingLabel NVARCHAR(32) NOT NULL,
        RatedCustomerCount INT NOT NULL CONSTRAINT DF_FLRS_Stats_Count DEFAULT (0),
        CapturedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FLRS_Stats_Captured DEFAULT (SYSUTCDATETIME()),
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
", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task<IReadOnlyList<FollowerListStatsRow>> GetStatsForFollowerAsync(
        int followerUserId,
        DateTime nowLocal,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        List<(int ListId, string? ListName)> lists;
        await using (var c = new SqlConnection(_cs))
            lists = await LoadFollowerListsAsync(c, followerUserId, ct);

        if (lists.Count == 0)
            return Array.Empty<FollowerListStatsRow>();

        var bag = new System.Collections.Concurrent.ConcurrentBag<(int Order, FollowerListStatsRow Row)>();
        await Parallel.ForEachAsync(
            lists.Select((l, i) => (l, i)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(4, lists.Count),
                CancellationToken = ct
            },
            async (item, token) =>
            {
                await using var conn = new SqlConnection(_cs);
                await conn.OpenAsync(token);
                var row = await BuildStatsRowAsync(
                    conn, item.l.ListId, item.l.ListName, nowLocal, token);
                bag.Add((item.i, row));
            });

        return bag.OrderBy(x => x.Order).Select(x => x.Row).ToList();
    }

    public async Task<FollowerListStatsRow?> GetStatsForListAsync(
        int followerUserId,
        int listId,
        DateTime nowLocal,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (!await IsListLinkedAsync(followerUserId, listId, ct))
            return null;

        await using var c = new SqlConnection(_cs);
        await c.OpenAsync(ct);
        var name = await c.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT d.DelegateName
FROM dbo.Delegates d
WHERE d.DelegateID = @ListId;",
            new { ListId = listId },
            cancellationToken: ct));
        return await BuildStatsRowAsync(c, listId, name, nowLocal, ct);
    }

    private async Task<FollowerListStatsRow> BuildStatsRowAsync(
        SqlConnection c,
        int listId,
        string? listName,
        DateTime nowLocal,
        CancellationToken ct)
    {
        var yearMonth = FollowerListStatsRules.ToYearMonth(nowLocal);
        var prevYm = FollowerListStatsRules.PreviousYearMonth(yearMonth);
        var (monthStart, monthEnd) = MonthBounds(yearMonth);
        var asOf = nowLocal.Date;
        var weekAgoDate = FollowerListTrendRules.WeekAgoDate(asOf);
        var monthAgoDate = FollowerListTrendRules.MonthAgoDate(asOf);

        // One connection: keep queries sequential (SqlClient is not concurrent-safe).
        var live = await LoadLiveAggregatesAsync(c, listId, yearMonth, ct);
        await UpsertSnapshotAsync(c, listId, yearMonth, live, ct);

        var prev = await c.QueryFirstOrDefaultAsync<(decimal TotalSalesSum, decimal DaySalesSum)?>(
            new CommandDefinition(@"
SELECT TOP 1 TotalSalesSum, DaySalesSum
FROM dbo.FollowerListMonthlySnapshots
WHERE ListId = @ListId AND YearMonth = @YearMonth;",
                new { ListId = listId, YearMonth = prevYm },
                cancellationToken: ct));

        var ratingCutoff = asOf.AddDays(-30);
        var rated = (await LoadRatingFactsAsync(c, listId, ct))
            .Where(f => f.AmountRemaining > 0
                        && !f.IsLegal
                        && f.LastPaymentDate is DateTime lp
                        && lp.Date >= ratingCutoff) // قانونية خارج التقييم
            .Select(f => FollowerCustomerRatingCalculator.Evaluate(f, asOf))
            .ToList();
        var agg = FollowerListRatingAggregator.Aggregate(rated);

        await UpsertDailyRatingSnapshotAsync(
            c, listId, agg.AverageScore, agg.FinalRatingLabel, agg.CustomerCount, asOf, ct);
        var scoreWeekAgo = await FollowerListCardsService.LoadSnapshotScoreAsync(
            c, listId, weekAgoDate, ct);
        var scoreMonthAgo = await FollowerListCardsService.LoadSnapshotScoreAsync(
            c, listId, monthAgoDate, ct);

        var zeroed = await LoadZeroedMonthAsync(c, listId, monthStart, monthEnd, ct);
        var neu = await LoadNewMonthMoneyAsync(c, listId, monthStart, monthEnd, ct);
        var prevTotal = prev?.TotalSalesSum ?? 0m;
        var prevDay = prev?.DaySalesSum ?? 0m;

        return new FollowerListStatsRow
        {
            ListId = listId,
            ListName = listName,
            YearMonth = yearMonth,
            PreviousYearMonth = prevYm,
            NewCustomersCount = live.NewCustomersCount,
            NewCustomersSaleSum = neu.SaleSum,
            NewCustomersDaySalesSum = neu.DaySum,
            ZeroedCustomersCount = zeroed.Count,
            ZeroedCustomersSaleSum = zeroed.SaleSum,
            ZeroedCustomersDaySalesSum = zeroed.DaySum,
            NetCustomersCount = live.NewCustomersCount - zeroed.Count,
            NetSaleSum = neu.SaleSum - zeroed.SaleSum,
            NetDaySalesSum = neu.DaySum - zeroed.DaySum,
            // Expensive 3.8% week scan removed from hot path (UI no longer shows it).
            ActiveCustomersCount = 0,
            AverageScore = agg.AverageScore,
            FinalRatingLabel = agg.FinalRatingLabel,
            ScoreWeekAgo = scoreWeekAgo,
            WeekTrend = FollowerListTrendRules.ComputeTrend(agg.AverageScore, scoreWeekAgo),
            ScoreMonthAgo = scoreMonthAgo,
            MonthTrend = FollowerListTrendRules.ComputeTrend(agg.AverageScore, scoreMonthAgo),
            ExcellentCount = agg.ExcellentCount,
            GoodCount = agg.GoodCount,
            WeakCount = agg.WeakCount,
            RejectedCount = agg.RejectedCount,
            LegalCount = agg.LegalCount,
            BadCount = agg.BadCount,
            RatedCustomerCount = agg.CustomerCount,
            CurrentTotalSales = live.TotalSalesSum,
            PreviousTotalSales = prevTotal,
            AddedAmount = FollowerListStatsRules.Delta(live.TotalSalesSum, prevTotal),
            CurrentDaySales = live.DaySalesSum,
            PreviousDaySales = prevDay,
            DaySalesDelta = FollowerListStatsRules.Delta(live.DaySalesSum, prevDay),
            PreviousMonthFrozen = prev != null
        };
    }

    private static async Task UpsertDailyRatingSnapshotAsync(
        SqlConnection c,
        int listId,
        double averageScore,
        string? ratingLabel,
        int ratedCount,
        DateTime captureDate,
        CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerListRatingSnapshots', N'U') IS NULL RETURN;
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
                RatingLabel = ratingLabel ?? "",
                RatedCustomerCount = ratedCount,
                CaptureDate = captureDate.Date
            },
            cancellationToken: ct));
    }

    public async Task<IReadOnlyList<FollowerListStatsCustomerRow>> GetRatingCustomersAsync(
        int followerUserId,
        int listId,
        string levelKey,
        DateTime nowLocal,
        CancellationToken ct)
    {
        if (!await IsListLinkedAsync(followerUserId, listId, ct))
            throw new UnauthorizedAccessException("القائمة غير مرتبطة");

        var allLevels = string.IsNullOrWhiteSpace(levelKey)
                        || levelKey.Equals("all", StringComparison.OrdinalIgnoreCase);
        FollowerCustomerRatingLevel level = default;
        if (!allLevels && !FollowerCustomerRatingLabels.TryParseLevelKey(levelKey, out level))
            throw new ArgumentException("مستوى التقييم غير صالح");

        await using var c = new SqlConnection(_cs);
        // Match ExcellentCount/… on list cards: open balance + payment within last 30 days.
        var ratingCutoff = nowLocal.Date.AddDays(-30);
        var rated = (await LoadRatingFactsAsync(c, listId, ct))
            .Where(f => f.AmountRemaining > 0
                        && !f.IsLegal
                        && f.LastPaymentDate is DateTime lp
                        && lp.Date >= ratingCutoff)
            .Select(f => FollowerCustomerRatingCalculator.Evaluate(f, nowLocal.Date));
        if (!allLevels)
            rated = rated.Where(r => r.Level == level);
        return rated
            .Select(ToCustomerRow)
            .OrderBy(r => r.CustomerName)
            .ToList();
    }

    public async Task<IReadOnlyList<FollowerListStatsCustomerRow>> GetActiveCustomersAsync(
        int followerUserId,
        int listId,
        DateTime nowLocal,
        CancellationToken ct)
    {
        if (!await IsListLinkedAsync(followerUserId, listId, ct))
            throw new UnauthorizedAccessException("القائمة غير مرتبطة");

        var (weekFrom, weekTo) = WeakWeekWindow(nowLocal);
        await using var c = new SqlConnection(_cs);
        var rows = await c.QueryAsync<FollowerListStatsCustomerRow>(new CommandDefinition(@"
SELECT
    c.CustomerID AS CustomerId,
    ISNULL(vc.CustomerName, c.CustomerName) AS CustomerName,
    ISNULL(vc.PhoneNumber, c.PhoneNumber) AS PhoneNumber,
    CAST(ISNULL(vc.AmountTotalSales, 0) AS FLOAT) AS AmountTotalSales,
    CAST(ISNULL(vc.AmountDaySales, 0) AS FLOAT) AS AmountDaySales,
    CAST(ISNULL(vc.AmountRemaining, 0) AS FLOAT) AS AmountRemaining,
    CAST(ISNULL(w.WeekPaid, 0) AS FLOAT) AS WeekPaid,
    CASE WHEN ISNULL(vc.AmountTotalSales, 0) > 0
         THEN ROUND(ISNULL(w.WeekPaid, 0) * 100.0 / vc.AmountTotalSales, 2)
         ELSE 0 END AS PaidPercent
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
LEFT JOIN (
    SELECT CustomerID, ROUND(SUM(ISNULL(AmountDenar, 0)), -3) AS WeekPaid
    FROM dbo.View_ReceiptCustomerDate
    WHERE CONVERT(date, PaymentDate) >= @FromDate
      AND CONVERT(date, PaymentDate) <= @ToDate
    GROUP BY CustomerID
) w ON w.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND ISNULL(vc.AmountRemaining, 0) > 0
  AND ISNULL(vc.AmountTotalSales, 0) > 0
  AND ISNULL(w.WeekPaid, 0) * 100.0 / vc.AmountTotalSales > 3.8
ORDER BY ISNULL(vc.CustomerName, c.CustomerName);",
            new { ListId = listId, FromDate = weekFrom, ToDate = weekTo },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<FollowerListStatsCustomerRow>> GetRemainderCustomersAsync(
        int followerUserId,
        int listId,
        decimal maxRemainingExclusive,
        DateTime nowLocal,
        CancellationToken ct,
        decimal minRemainingInclusive = 0)
    {
        if (!await IsListLinkedAsync(followerUserId, listId, ct))
            throw new UnauthorizedAccessException("القائمة غير مرتبطة");
        if (maxRemainingExclusive <= 0)
            throw new ArgumentException("حد الباقي غير صالح");
        if (minRemainingInclusive < 0 || minRemainingInclusive >= maxRemainingExclusive)
            throw new ArgumentException("نطاق الباقي غير صالح");

        await using var c = new SqlConnection(_cs);
        // Band: minInclusive <= remaining < maxExclusive (no overlap between 0–100k and 100–200k).
        try
        {
            var rows = await c.QueryAsync<FollowerListStatsCustomerRow>(new CommandDefinition(@"
SELECT
    c.CustomerID AS CustomerId,
    ISNULL(vc.CustomerName, c.CustomerName) AS CustomerName,
    ISNULL(vc.PhoneNumber, c.PhoneNumber) AS PhoneNumber,
    CAST(ISNULL(vc.AmountTotalSales, 0) AS FLOAT) AS AmountTotalSales,
    CAST(ISNULL(vc.AmountDaySales, 0) AS FLOAT) AS AmountDaySales,
    CAST(ISNULL(vc.AmountRemaining, 0) AS FLOAT) AS AmountRemaining
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND ISNULL(vc.AmountRemaining, 0) >= @MinRem
  AND ISNULL(vc.AmountRemaining, 0) < @MaxRem
  AND ISNULL(vc.AmountRemaining, 0) > 0
ORDER BY ISNULL(vc.AmountRemaining, 0), ISNULL(vc.CustomerName, c.CustomerName);",
                new
                {
                    ListId = listId,
                    MinRem = (double)minRemainingInclusive,
                    MaxRem = (double)maxRemainingExclusive
                },
                cancellationToken: ct));
            return rows.ToList();
        }
        catch
        {
            var facts = (await LoadRatingFactsAsync(c, listId, ct))
                .Where(f => f.AmountRemaining > 0
                            && !f.IsLegal
                            && f.AmountRemaining >= (double)minRemainingInclusive
                            && f.AmountRemaining < (double)maxRemainingExclusive);
            return facts
                .Select(f => ToCustomerRow(FollowerCustomerRatingCalculator.Evaluate(f, nowLocal.Date)))
                .OrderBy(r => r.AmountRemaining)
                .ThenBy(r => r.CustomerName)
                .ToList();
        }
    }

    private static FollowerListStatsCustomerRow ToCustomerRow(FollowerCustomerRatingResult r) => new()
    {
        CustomerId = r.CustomerId,
        CustomerName = r.CustomerName,
        PhoneNumber = r.PhoneNumber,
        AmountTotalSales = r.AmountTotalSales,
        AmountDaySales = r.AmountDaySales,
        AmountRemaining = r.AmountRemaining,
        RatingLabel = r.RatingLabel,
        Score = r.Score
    };

    private async Task<bool> IsListLinkedAsync(int followerUserId, int listId, CancellationToken ct)
    {
        await using var c = new SqlConnection(_cs);
        var n = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.FollowerUserLists
WHERE UserId = @UserId AND ListId = @ListId;",
            new { UserId = followerUserId, ListId = listId },
            cancellationToken: ct));
        return n > 0;
    }

    private static async Task<List<(int ListId, string? ListName)>> LoadFollowerListsAsync(
        SqlConnection c, int followerUserId, CancellationToken ct) =>
        (await c.QueryAsync<(int ListId, string? ListName)>(new CommandDefinition(@"
SELECT ful.ListId, d.DelegateName AS ListName
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Delegates d ON d.DelegateID = ful.ListId
WHERE ful.UserId = @FollowerUserId
ORDER BY d.DelegateName;",
            new { FollowerUserId = followerUserId },
            cancellationToken: ct))).ToList();

    private static async Task UpsertSnapshotAsync(
        SqlConnection c,
        int listId,
        int yearMonth,
        (decimal TotalSalesSum, decimal DaySalesSum, int NewCustomersCount) live,
        CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.FollowerListMonthlySnapshots AS t
USING (SELECT @ListId AS ListId, @YearMonth AS YearMonth) AS s
ON t.ListId = s.ListId AND t.YearMonth = s.YearMonth
WHEN MATCHED THEN UPDATE SET
    TotalSalesSum = @TotalSalesSum,
    DaySalesSum = @DaySalesSum,
    NewCustomersCount = @NewCustomersCount,
    CapturedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (ListId, YearMonth, TotalSalesSum, DaySalesSum, NewCustomersCount, CapturedAtUtc)
VALUES
    (@ListId, @YearMonth, @TotalSalesSum, @DaySalesSum, @NewCustomersCount, SYSUTCDATETIME());",
            new
            {
                ListId = listId,
                YearMonth = yearMonth,
                TotalSalesSum = live.TotalSalesSum,
                DaySalesSum = live.DaySalesSum,
                NewCustomersCount = live.NewCustomersCount
            },
            cancellationToken: ct));
    }

    private static async Task<(decimal TotalSalesSum, decimal DaySalesSum, int NewCustomersCount)> LoadLiveAggregatesAsync(
        SqlConnection c,
        int listId,
        int yearMonth,
        CancellationToken ct)
    {
        var (monthStart, monthEnd) = MonthBounds(yearMonth);

        var totals = await c.QueryFirstAsync<(decimal TotalSalesSum, decimal DaySalesSum)>(new CommandDefinition(@"
SELECT
    CAST(ISNULL(SUM(ISNULL(vc.AmountTotalSales, 0)), 0) AS DECIMAL(18,2)) AS TotalSalesSum,
    CAST(ISNULL(SUM(ISNULL(vc.AmountDaySales, 0)), 0) AS DECIMAL(18,2)) AS DaySalesSum
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId;",
            new { ListId = listId },
            cancellationToken: ct));

        var newCount = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(DISTINCT c.CustomerID)
FROM dbo.Customers c
INNER JOIN dbo.CustomersSales s ON s.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND s.DateCreate >= @MonthStart AND s.DateCreate < @MonthEnd
  AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersSales s2
      WHERE s2.CustomerID = c.CustomerID AND s2.DateCreate < @MonthStart
  );",
            new { ListId = listId, MonthStart = monthStart, MonthEnd = monthEnd },
            cancellationToken: ct));

        return (totals.TotalSalesSum, totals.DaySalesSum, newCount);
    }

    private static async Task<(decimal SaleSum, decimal DaySum)> LoadNewMonthMoneyAsync(
        SqlConnection c, int listId, DateTime monthStart, DateTime monthEnd, CancellationToken ct)
    {
        return await c.QueryFirstAsync<(decimal SaleSum, decimal DaySum)>(new CommandDefinition(@"
SELECT
    CAST(ISNULL(SUM(ISNULL(vc.AmountTotalSales, 0)), 0) AS DECIMAL(18,2)) AS SaleSum,
    CAST(ISNULL(SUM(ISNULL(vc.AmountDaySales, 0)), 0) AS DECIMAL(18,2)) AS DaySum
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND EXISTS (
      SELECT 1 FROM dbo.CustomersSales s
      WHERE s.CustomerID = c.CustomerID
        AND s.DateCreate >= @MonthStart AND s.DateCreate < @MonthEnd
  )
  AND NOT EXISTS (
      SELECT 1 FROM dbo.CustomersSales s2
      WHERE s2.CustomerID = c.CustomerID AND s2.DateCreate < @MonthStart
  );",
            new { ListId = listId, MonthStart = monthStart, MonthEnd = monthEnd },
            cancellationToken: ct));
    }

    private static async Task<(int Count, decimal SaleSum, decimal DaySum)> LoadZeroedMonthAsync(
        SqlConnection c, int listId, DateTime monthStart, DateTime monthEnd, CancellationToken ct)
    {
        return await c.QueryFirstAsync<(int Count, decimal SaleSum, decimal DaySum)>(new CommandDefinition(@"
SELECT
    COUNT(1) AS Count,
    CAST(ISNULL(SUM(ISNULL(vc.AmountTotalSales, 0)), 0) AS DECIMAL(18,2)) AS SaleSum,
    CAST(ISNULL(SUM(ISNULL(vc.AmountDaySales, 0)), 0) AS DECIMAL(18,2)) AS DaySum
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND ISNULL(vc.AmountRemaining, 0) = 0
  AND vc.LastPaymentDate IS NOT NULL
  AND CONVERT(date, vc.LastPaymentDate) >= @MonthStart
  AND CONVERT(date, vc.LastPaymentDate) < @MonthEnd;",
            new { ListId = listId, MonthStart = monthStart.Date, MonthEnd = monthEnd.Date },
            cancellationToken: ct));
    }

    private static async Task<Dictionary<int, double>> LoadWeekPaidMapAsync(
        SqlConnection c,
        IReadOnlyList<int> customerIds,
        DateTime weekFrom,
        DateTime weekTo,
        CancellationToken ct)
    {
        var map = new Dictionary<int, double>();
        if (customerIds.Count == 0) return map;

        var rows = await c.QueryAsync<(int CustomerId, double WeekPaid)>(new CommandDefinition(@"
SELECT CustomerID AS CustomerId,
       CAST(ROUND(SUM(ISNULL(AmountDenar, 0)), -3) AS float) AS WeekPaid
FROM dbo.View_ReceiptCustomerDate
WHERE CustomerID IN @Ids
  AND CONVERT(date, PaymentDate) >= @FromDate
  AND CONVERT(date, PaymentDate) <= @ToDate
GROUP BY CustomerID;",
            new { Ids = customerIds, FromDate = weekFrom.Date, ToDate = weekTo.Date },
            cancellationToken: ct));
        foreach (var row in rows)
            map[row.CustomerId] = row.WeekPaid;
        return map;
    }

    private static async Task<int> CountActiveAsync(
        SqlConnection c, int listId, DateTime weekFrom, DateTime weekTo, CancellationToken ct) =>
        await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
LEFT JOIN (
    SELECT CustomerID, ROUND(SUM(ISNULL(AmountDenar, 0)), -3) AS WeekPaid
    FROM dbo.View_ReceiptCustomerDate
    WHERE CONVERT(date, PaymentDate) >= @FromDate
      AND CONVERT(date, PaymentDate) <= @ToDate
    GROUP BY CustomerID
) w ON w.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND ISNULL(vc.AmountRemaining, 0) > 0
  AND ISNULL(vc.AmountTotalSales, 0) > 0
  AND ISNULL(w.WeekPaid, 0) * 100.0 / vc.AmountTotalSales > 3.8;",
            new { ListId = listId, FromDate = weekFrom, ToDate = weekTo },
            cancellationToken: ct));

    private static async Task<List<FollowerCustomerRatingFacts>> LoadRatingFactsAsync(
        SqlConnection c, int listId, CancellationToken ct)
    {
        // Fast path: View_Customers. Slow OUTER APPLY only if the view path fails.
        try
        {
            var rows = await c.QueryAsync<dynamic>(new CommandDefinition(@"
SELECT
    c.CustomerID AS CustomerId,
    c.DelegateID AS DelegateId,
    ISNULL(vc.CustomerName, c.CustomerName) AS CustomerName,
    ISNULL(vc.PhoneNumber, c.PhoneNumber) AS PhoneNumber,
    CAST(ISNULL(c.IsLegal, 0) AS bit) AS IsLegal,
    vc.DateSaleDevice AS DateSaleDevice,
    vc.LastPaymentDate AS LastPaymentDate,
    CAST(ISNULL(vc.AmountTotalSales, 0) AS FLOAT) AS AmountTotalSales,
    CAST(ISNULL(vc.ReceiptsTotal, 0) AS FLOAT) AS ReceiptsTotal,
    CAST(ISNULL(vc.AmountRemaining, 0) AS FLOAT) AS AmountRemaining,
    CAST(ISNULL(vc.AmountDaySales, 0) AS FLOAT) AS AmountDaySales
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId;",
                new { ListId = listId },
                cancellationToken: ct));
            return MapFacts(rows);
        }
        catch
        {
            var rows = await c.QueryAsync<dynamic>(new CommandDefinition(@"
SELECT
    C.CustomerID AS CustomerId,
    C.DelegateID AS DelegateId,
    C.CustomerName AS CustomerName,
    C.PhoneNumber AS PhoneNumber,
    CAST(ISNULL(C.IsLegal, 0) AS bit) AS IsLegal,
    S.DateSaleDevice AS DateSaleDevice,
    P.LastPaymentDate AS LastPaymentDate,
    CAST(ISNULL(S.AmountTotalSales, 0) AS float) AS AmountTotalSales,
    CAST(ISNULL(P.ReceiptsTotal, 0) AS float) AS ReceiptsTotal,
    CAST(ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) AS float) AS AmountRemaining,
    CAST(ISNULL(VC.AmountDaySales, 0) AS float) AS AmountDaySales
FROM dbo.Customers C
LEFT JOIN dbo.View_Customers VC ON VC.CustomerID = C.CustomerID
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
                r.DelegateId == null ? null : (int?)Convert.ToInt32(r.DelegateId),
                (string?)r.CustomerName,
                (string?)r.PhoneNumber,
                r.IsLegal is bool b ? b : Convert.ToBoolean(r.IsLegal ?? false),
                r.DateSaleDevice as DateTime?,
                r.LastPaymentDate as DateTime?,
                Convert.ToDouble(r.AmountTotalSales ?? 0),
                Convert.ToDouble(r.ReceiptsTotal ?? 0),
                Convert.ToDouble(r.AmountRemaining ?? 0),
                Convert.ToDouble(r.AmountDaySales ?? 0)));
        }
        return list;
    }

    private static (DateTime MonthStart, DateTime MonthEndExclusive) MonthBounds(int yearMonth)
    {
        var y = yearMonth / 100;
        var m = yearMonth % 100;
        var start = new DateTime(y, m, 1);
        return (start, start.AddMonths(1));
    }

    /// <summary>Same window as Customers_GetWeakWeekPayers: [today-7, yesterday].</summary>
    private static (DateTime From, DateTime To) WeakWeekWindow(DateTime nowLocal)
    {
        var today = nowLocal.Date;
        return (today.AddDays(-7), today.AddDays(-1));
    }
}
