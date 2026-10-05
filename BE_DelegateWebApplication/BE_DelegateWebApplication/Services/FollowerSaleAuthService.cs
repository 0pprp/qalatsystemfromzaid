using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public sealed class FollowerSaleAuthService : IFollowerSaleAuthService
{
    private readonly string _cs;
    private static int _schemaReady;

    public FollowerSaleAuthService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await using var c = new SqlConnection(_cs);
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerSaleAuthentications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerSaleAuthentications (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerSaleAuthentications PRIMARY KEY,
        CustomerId INT NOT NULL,
        ListId INT NOT NULL,
        CustomerSaleId INT NULL,
        SaleDraftId INT NULL,
        FollowerUserId INT NOT NULL,
        FollowerName NVARCHAR(200) NULL,
        Rating INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FollowerSaleAuth_Stock DEFAULT (0),
        EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FollowerSaleAuth_Daily DEFAULT (0),
        AuthenticatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerSaleAuth_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE UNIQUE INDEX UX_FollowerSaleAuth_Customer_List
        ON dbo.FollowerSaleAuthentications (CustomerId, ListId);
    CREATE INDEX IX_FollowerSaleAuth_Follower
        ON dbo.FollowerSaleAuthentications (FollowerUserId, AuthenticatedAtUtc DESC);
END
IF COL_LENGTH(N'dbo.FollowerSaleAuthentications', N'ShopStockEstimatedValue') IS NULL
    ALTER TABLE dbo.FollowerSaleAuthentications ADD ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FollowerSaleAuth_Stock2 DEFAULT (0);
IF COL_LENGTH(N'dbo.FollowerSaleAuthentications', N'EstimatedDailyRevenue') IS NULL
    ALTER TABLE dbo.FollowerSaleAuthentications ADD EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FollowerSaleAuth_Daily2 DEFAULT (0);
", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task<IReadOnlyList<FollowerPendingSaleRow>> ListPendingAsync(
        int followerUserId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        var rows = await c.QueryAsync<FollowerPendingSaleRow>(new CommandDefinition(@"
SELECT
    c.CustomerID AS CustomerId,
    c.CustomerName,
    c.PhoneNumber,
    c.DelegateID AS ListId,
    d.DelegateName AS ListName,
    cs.CustomerSaleID AS CustomerSaleId,
    sd.SaleId AS SaleDraftId,
    cs.DateCreate AS SaleDate,
    ISNULL(agg.AmountTotalSales, 0) AS AmountTotalSales,
    ISNULL(agg.AmountDaySales, 0) AS AmountDaySales,
    c.ShopName,
    c.Address
FROM dbo.Customers c
INNER JOIN dbo.FollowerUserLists ful
    ON ful.ListId = c.DelegateID AND ful.UserId = @FollowerUserId
INNER JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
CROSS APPLY (
    SELECT TOP 1 s.CustomerSaleID, s.DateCreate
    FROM dbo.CustomersSales s
    WHERE s.CustomerID = c.CustomerID
    ORDER BY s.DateCreate DESC, s.CustomerSaleID DESC
) cs
OUTER APPLY (
    SELECT TOP 1 sd.SaleId
    FROM dbo.SalesDrafts sd
    WHERE sd.CustomerId = c.CustomerID
      AND sd.Status = N'Completed'
      AND ISNULL(sd.PostingStatus, N'') = N'Posted'
    ORDER BY sd.PostedAtUtc DESC, sd.SaleId DESC
) sd
OUTER APPLY (
    SELECT
        SUM(ISNULL(v.AmountTotalSales, 0)) AS AmountTotalSales,
        SUM(ISNULL(v.AmountDaySales, 0)) AS AmountDaySales
    FROM (
        SELECT TOP 1 *
        FROM dbo.View_Customers vc
        WHERE vc.CustomerID = c.CustomerID
    ) v
) agg
WHERE sd.SaleId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.FollowerSaleAuthentications a
      WHERE a.CustomerId = c.CustomerID AND a.ListId = c.DelegateID
  )
ORDER BY cs.DateCreate DESC, c.CustomerID DESC;",
            new { FollowerUserId = followerUserId },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<FollowerSaleAuthRow> AuthenticateAsync(
        int followerUserId,
        string? followerName,
        int customerId,
        int listId,
        int rating,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (!FollowerSaleAuthRules.IsValidRating(rating))
        {
            throw new InvalidOperationException("التقييم يجب أن يكون من 1 إلى 10");
        }

        if (!FollowerSaleAuthRules.IsValidNoteForRating(rating, note))
        {
            throw new InvalidOperationException("الملاحظة إجبارية عندما يكون التقييم أقل من 7");
        }

        if (!FollowerSaleAuthRules.IsValidShopMoney(shopStockEstimatedValue))
        {
            throw new InvalidOperationException("قيمة بضاعة المحل مطلوبة ويجب أن تكون أكبر من صفر");
        }

        if (!FollowerSaleAuthRules.IsValidShopMoney(estimatedDailyRevenue))
        {
            throw new InvalidOperationException("قيمة واردات المحل اليومية مطلوبة ويجب أن تكون أكبر من صفر");
        }

        await using var c = new SqlConnection(_cs);
        var existing = await c.QueryFirstOrDefaultAsync<FollowerSaleAuthRow>(new CommandDefinition(@"
SELECT TOP 1 *
FROM dbo.FollowerSaleAuthentications
WHERE CustomerId = @CustomerId AND ListId = @ListId;",
            new { CustomerId = customerId, ListId = listId },
            cancellationToken: ct));
        if (existing != null)
        {
            throw new InvalidOperationException("تمت مصادقة هذا المبيع مسبقاً");
        }

        var meta = await c.QueryFirstOrDefaultAsync<SaleMetaRow>(new CommandDefinition(@"
SELECT
    (SELECT TOP 1 CustomerSaleID FROM dbo.CustomersSales
     WHERE CustomerID = @CustomerId ORDER BY DateCreate DESC, CustomerSaleID DESC) AS CustomerSaleId,
    (SELECT TOP 1 SaleId FROM dbo.SalesDrafts
     WHERE CustomerId = @CustomerId AND Status = N'Completed' AND ISNULL(PostingStatus, N'') = N'Posted'
     ORDER BY PostedAtUtc DESC, SaleId DESC) AS SaleDraftId;",
            new { CustomerId = customerId },
            cancellationToken: ct));

        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.FollowerSaleAuthentications
    (CustomerId, ListId, CustomerSaleId, SaleDraftId, FollowerUserId, FollowerName, Rating, Note,
     ShopStockEstimatedValue, EstimatedDailyRevenue, AuthenticatedAtUtc)
OUTPUT INSERTED.Id
VALUES
    (@CustomerId, @ListId, @CustomerSaleId, @SaleDraftId, @FollowerUserId, @FollowerName, @Rating, @Note,
     @ShopStockEstimatedValue, @EstimatedDailyRevenue, SYSUTCDATETIME());",
            new
            {
                CustomerId = customerId,
                ListId = listId,
                CustomerSaleId = meta?.CustomerSaleId,
                SaleDraftId = meta?.SaleDraftId,
                FollowerUserId = followerUserId,
                FollowerName = string.IsNullOrWhiteSpace(followerName) ? null : followerName.Trim(),
                Rating = rating,
                Note = FollowerSaleAuthRules.NormalizeNote(note),
                ShopStockEstimatedValue = decimal.Round(shopStockEstimatedValue, 0, MidpointRounding.AwayFromZero),
                EstimatedDailyRevenue = decimal.Round(estimatedDailyRevenue, 0, MidpointRounding.AwayFromZero)
            },
            cancellationToken: ct));

        return (await GetByIdAsync(c, id, ct))!;
    }

    private sealed class SaleMetaRow
    {
        public int? CustomerSaleId { get; set; }
        public int? SaleDraftId { get; set; }
    }

    public async Task<FollowerSaleAuthRow?> GetForCustomerAsync(int customerId, int listId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        return await c.QueryFirstOrDefaultAsync<FollowerSaleAuthRow>(new CommandDefinition(@"
SELECT TOP 1 *
FROM dbo.FollowerSaleAuthentications
WHERE CustomerId = @CustomerId AND ListId = @ListId;",
            new { CustomerId = customerId, ListId = listId },
            cancellationToken: ct));
    }

    private static Task<FollowerSaleAuthRow?> GetByIdAsync(SqlConnection c, int id, CancellationToken ct) =>
        c.QueryFirstOrDefaultAsync<FollowerSaleAuthRow>(new CommandDefinition(
            "SELECT TOP 1 * FROM dbo.FollowerSaleAuthentications WHERE Id = @Id",
            new { Id = id },
            cancellationToken: ct));
}
