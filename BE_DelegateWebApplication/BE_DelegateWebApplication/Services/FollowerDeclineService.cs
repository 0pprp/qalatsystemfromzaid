using BE_DelegateWebApplication.Services.FollowerRating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public interface IFollowerDeclineService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task RefreshForFollowerAsync(int followerUserId, DateTime nowLocal, CancellationToken ct = default);
    Task<IReadOnlyList<FollowerDecliningCustomerDto>> ListDecliningAsync(
        int followerUserId,
        DateTime nowLocal,
        CancellationToken ct = default);
}

public sealed class FollowerDeclineService : IFollowerDeclineService
{
    private readonly string _cs;
    private static int _schemaReady;

    public FollowerDeclineService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await using var c = new SqlConnection(_cs);
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerUserLists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerUserLists (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerUserLists_Decline PRIMARY KEY,
        UserId INT NOT NULL,
        ListId INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FUL_Decline_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_FollowerUserLists_Decline_User_List UNIQUE (UserId, ListId)
    );
END
IF OBJECT_ID(N'dbo.FollowerCustomerRatingSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerCustomerRatingSnapshots (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerCustomerRatingSnapshots PRIMARY KEY,
        CustomerId INT NOT NULL,
        ListId INT NOT NULL,
        CustomerName NVARCHAR(200) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        RatingLabel NVARCHAR(32) NOT NULL,
        Score INT NOT NULL,
        CapturedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FCRS_Captured DEFAULT (SYSUTCDATETIME()),
        CaptureDate DATE NOT NULL
    );
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_FollowerCustomerRatingSnapshots_Cust_Day'
      AND object_id = OBJECT_ID(N'dbo.FollowerCustomerRatingSnapshots')
)
    CREATE UNIQUE INDEX UQ_FollowerCustomerRatingSnapshots_Cust_Day
        ON dbo.FollowerCustomerRatingSnapshots (CustomerId, CaptureDate);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FollowerCustomerRatingSnapshots_List_Day'
      AND object_id = OBJECT_ID(N'dbo.FollowerCustomerRatingSnapshots')
)
    CREATE INDEX IX_FollowerCustomerRatingSnapshots_List_Day
        ON dbo.FollowerCustomerRatingSnapshots (ListId, CaptureDate);
", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task RefreshForFollowerAsync(int followerUserId, DateTime nowLocal, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var asOf = nowLocal.Date;
        var ratingCutoff = asOf.AddDays(-30);
        await using var c = new SqlConnection(_cs);
        var listIds = (await c.QueryAsync<int>(new CommandDefinition(@"
SELECT ListId FROM dbo.FollowerUserLists WHERE UserId = @UserId;",
            new { UserId = followerUserId }, cancellationToken: ct))).ToList();

        foreach (var listId in listIds)
        {
            ct.ThrowIfCancellationRequested();
            List<FollowerCustomerRatingFacts> facts;
            try
            {
                facts = await LoadRatingFactsAsync(c, listId, ct);
            }
            catch
            {
                continue;
            }

            foreach (var f in facts.Where(x => x.AmountRemaining > 0
                                               && x.LastPaymentDate is DateTime lp
                                               && lp.Date >= ratingCutoff))
            {
                try
                {
                    var r = FollowerCustomerRatingCalculator.Evaluate(f, asOf);
                    await UpsertAsync(c, r.CustomerId, listId, r.CustomerName, r.PhoneNumber, r.RatingLabel, r.Score, asOf, ct);
                }
                catch
                {
                    // skip one customer — never fail the whole tab
                }
            }
        }
    }

    public async Task<IReadOnlyList<FollowerDecliningCustomerDto>> ListDecliningAsync(
        int followerUserId,
        DateTime nowLocal,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        try
        {
            await RefreshForFollowerAsync(followerUserId, nowLocal, ct);
        }
        catch
        {
            // Still try to read any existing snapshots.
        }

        await using var c = new SqlConnection(_cs);
        IReadOnlyList<SnapshotRow> rows;
        try
        {
            rows = (await c.QueryAsync<SnapshotRow>(new CommandDefinition(@"
DECLARE @Today DATE = @AsOf;
DECLARE @From DATE = DATEADD(DAY, -@Lookback, @Today);

;WITH Latest AS (
    SELECT
        s.CustomerId,
        s.ListId,
        s.CustomerName,
        s.PhoneNumber,
        s.RatingLabel AS ToLabel,
        s.Score AS ToScore,
        s.CaptureDate AS CurrentCaptureDate,
        ROW_NUMBER() OVER (PARTITION BY s.CustomerId ORDER BY s.CaptureDate DESC, s.CapturedAtUtc DESC) AS rn
    FROM dbo.FollowerCustomerRatingSnapshots s
    INNER JOIN dbo.FollowerUserLists ful ON ful.ListId = s.ListId AND ful.UserId = @UserId
),
CurrentRows AS (
    SELECT * FROM Latest WHERE rn = 1
),
PriorRows AS (
    SELECT
        s.CustomerId,
        s.RatingLabel AS FromLabel,
        s.Score AS FromScore,
        s.CaptureDate AS PriorCaptureDate,
        ROW_NUMBER() OVER (
            PARTITION BY s.CustomerId
            ORDER BY s.CaptureDate DESC, s.CapturedAtUtc DESC
        ) AS rn
    FROM dbo.FollowerCustomerRatingSnapshots s
    INNER JOIN CurrentRows c ON c.CustomerId = s.CustomerId
    WHERE s.CaptureDate < c.CurrentCaptureDate
      AND s.CaptureDate >= @From
)
SELECT
    c.CustomerId,
    COALESCE(NULLIF(LTRIM(RTRIM(c.CustomerName)), N''), cust.CustomerName, N'') AS CustomerName,
    COALESCE(NULLIF(LTRIM(RTRIM(c.PhoneNumber)), N''), cust.PhoneNumber) AS PhoneNumber,
    c.ListId,
    d.DelegateName AS ListName,
    p.FromLabel,
    c.ToLabel,
    p.FromScore,
    c.ToScore,
    p.PriorCaptureDate,
    c.CurrentCaptureDate,
    DATEDIFF(DAY, p.PriorCaptureDate, c.CurrentCaptureDate) AS DaysBetween
FROM CurrentRows c
INNER JOIN PriorRows p ON p.CustomerId = c.CustomerId AND p.rn = 1
LEFT JOIN dbo.Customers cust ON cust.CustomerID = c.CustomerId
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.ListId
WHERE DATEDIFF(DAY, p.PriorCaptureDate, c.CurrentCaptureDate) BETWEEN 0 AND @Lookback;",
                new
                {
                    UserId = followerUserId,
                    AsOf = nowLocal.Date,
                    Lookback = FollowerDeclineRules.LookbackDays
                },
                cancellationToken: ct,
                commandTimeout: 90))).ToList();
        }
        catch
        {
            return Array.Empty<FollowerDecliningCustomerDto>();
        }

        return rows
            .Where(r => FollowerDeclineRules.IsDeclining(
                r.FromLabel, r.FromScore, r.ToLabel, r.ToScore, r.DaysBetween))
            .Select(r => new FollowerDecliningCustomerDto
            {
                CustomerId = r.CustomerId,
                CustomerName = r.CustomerName ?? "",
                PhoneNumber = r.PhoneNumber,
                ListId = r.ListId,
                ListName = r.ListName,
                FromLabel = r.FromLabel ?? "",
                ToLabel = r.ToLabel ?? "",
                FromScore = r.FromScore,
                ToScore = r.ToScore,
                PriorCaptureDate = r.PriorCaptureDate,
                CurrentCaptureDate = r.CurrentCaptureDate,
                DaysBetween = r.DaysBetween
            })
            .OrderBy(r => r.CustomerName)
            .ToList();
    }

    private static async Task UpsertAsync(
        SqlConnection c,
        int customerId,
        int listId,
        string? name,
        string? phone,
        string label,
        int score,
        DateTime captureDate,
        CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.FollowerCustomerRatingSnapshots AS t
USING (SELECT @CustomerId AS CustomerId, @CaptureDate AS CaptureDate) AS s
ON t.CustomerId = s.CustomerId AND t.CaptureDate = s.CaptureDate
WHEN MATCHED THEN UPDATE SET
    ListId = @ListId,
    CustomerName = @CustomerName,
    PhoneNumber = @PhoneNumber,
    RatingLabel = @RatingLabel,
    Score = @Score,
    CapturedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (CustomerId, ListId, CustomerName, PhoneNumber, RatingLabel, Score, CapturedAtUtc, CaptureDate)
VALUES
    (@CustomerId, @ListId, @CustomerName, @PhoneNumber, @RatingLabel, @Score, SYSUTCDATETIME(), @CaptureDate);",
            new
            {
                CustomerId = customerId,
                ListId = listId,
                CustomerName = name,
                PhoneNumber = phone,
                RatingLabel = label,
                Score = score,
                CaptureDate = captureDate.Date
            },
            cancellationToken: ct));
    }

    private static async Task<List<FollowerCustomerRatingFacts>> LoadRatingFactsAsync(
        SqlConnection c, int listId, CancellationToken ct)
    {
        try
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
    SELECT MAX(V.DateCreate) AS DateSaleDevice,
           ROUND(ISNULL(SUM(V.AmountTotalSalesDenar), 0), -3) AS AmountTotalSales
    FROM dbo.View_CustomersSalesDelegate V WHERE V.CustomerID = C.CustomerID
) S
OUTER APPLY (
    SELECT MAX(V.PaymentDate) AS LastPaymentDate,
           ROUND(ISNULL(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal
    FROM dbo.View_CustomersPaymentsDelegate V WHERE V.CustomerID = C.CustomerID
) P
WHERE C.DelegateID = @ListId;",
                new { ListId = listId }, cancellationToken: ct, commandTimeout: 90));
            return MapFacts(rows);
        }
        catch (SqlException)
        {
            var rows = await c.QueryAsync<dynamic>(new CommandDefinition(@"
SELECT
    C.CustomerID AS CustomerId,
    C.DelegateID AS DelegateId,
    ISNULL(VC.CustomerName, C.CustomerName) AS CustomerName,
    ISNULL(VC.PhoneNumber, C.PhoneNumber) AS PhoneNumber,
    CAST(0 AS bit) AS IsLegal,
    VC.DateSaleDevice AS DateSaleDevice,
    VC.LastPaymentDate AS LastPaymentDate,
    CAST(ISNULL(VC.AmountTotalSales, 0) AS float) AS AmountTotalSales,
    CAST(ISNULL(VC.ReceiptsTotal, 0) AS float) AS ReceiptsTotal,
    CAST(ISNULL(VC.AmountRemaining, 0) AS float) AS AmountRemaining,
    CAST(ISNULL(VC.AmountDaySales, 0) AS float) AS AmountDaySales
FROM dbo.Customers C
LEFT JOIN dbo.View_Customers VC ON VC.CustomerID = C.CustomerID
WHERE C.DelegateID = @ListId;",
                new { ListId = listId }, cancellationToken: ct, commandTimeout: 90));
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

    private sealed class SnapshotRow
    {
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public int ListId { get; set; }
        public string? ListName { get; set; }
        public string? FromLabel { get; set; }
        public string? ToLabel { get; set; }
        public int FromScore { get; set; }
        public int ToScore { get; set; }
        public DateTime PriorCaptureDate { get; set; }
        public DateTime CurrentCaptureDate { get; set; }
        public int DaysBetween { get; set; }
    }
}
