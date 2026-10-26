using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public sealed class DelegateFirstCollectionEvalRow
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ListId { get; set; }
    public int DelegateId { get; set; }
    public string? DelegateName { get; set; }
    public int Rating { get; set; }
    public string? Note { get; set; }
    public decimal ShopStockEstimatedValue { get; set; }
    public decimal EstimatedDailyRevenue { get; set; }
    public decimal? SaleAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public interface IDelegateFirstCollectionEvalService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<(DelegateFirstCollectionEvalRow Row, bool Created)> SubmitAsync(
        int delegateId,
        string? delegateName,
        int customerId,
        int listId,
        int rating,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue,
        decimal? saleAmount,
        CancellationToken ct);

    Task<DelegateFirstCollectionEvalRow?> GetForCustomerAsync(int customerId, CancellationToken ct);
}

public sealed class DelegateFirstCollectionEvalService : IDelegateFirstCollectionEvalService
{
    private readonly string _cs;
    private static int _schemaReady;
    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    public DelegateFirstCollectionEvalService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await SchemaGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _schemaReady) == 1) return;
            await EnsureSchemaCoreAsync(ct);
            Volatile.Write(ref _schemaReady, 1);
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    private async Task EnsureSchemaCoreAsync(CancellationToken ct)
    {
        await using var c = new SqlConnection(_cs);
        // ALTER TABLE must run via dynamic SQL: SQL Server compiles static ALTER in the
        // same batch even when IF COL_LENGTH is false, which throws when columns already exist
        // and leaves _schemaReady=0 so every APK submit retries DDL under lock pressure.
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DelegateFirstCollectionEvaluations (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DelegateFirstCollectionEvaluations PRIMARY KEY,
        CustomerId INT NOT NULL,
        ListId INT NOT NULL,
        DelegateId INT NOT NULL,
        DelegateName NVARCHAR(200) NULL,
        Rating INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        ShopStockEstimatedValue DECIMAL(18,0) NOT NULL,
        EstimatedDailyRevenue DECIMAL(18,0) NOT NULL,
        SaleAmount DECIMAL(18,0) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DFCE_At DEFAULT (SYSUTCDATETIME())
    );
END

IF OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'ListId') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD ListId INT NOT NULL CONSTRAINT DF_DFCE_ListId DEFAULT (0)');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'DelegateId') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD DelegateId INT NOT NULL CONSTRAINT DF_DFCE_DelegateId DEFAULT (0)');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'DelegateName') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD DelegateName NVARCHAR(200) NULL');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'Rating') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD Rating INT NOT NULL CONSTRAINT DF_DFCE_Rating DEFAULT (1)');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'Note') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD Note NVARCHAR(1000) NULL');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'ShopStockEstimatedValue') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_DFCE_Stock DEFAULT (0)');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'EstimatedDailyRevenue') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_DFCE_Daily DEFAULT (0)');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'SaleAmount') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD SaleAmount DECIMAL(18,0) NULL');
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'CreatedAtUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DFCE_At2 DEFAULT (SYSUTCDATETIME())');

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_DFCE_Customer'
          AND object_id = OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations'))
    BEGIN
        EXEC(N'CREATE UNIQUE INDEX UX_DFCE_Customer ON dbo.DelegateFirstCollectionEvaluations (CustomerId)');
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_DFCE_Delegate'
          AND object_id = OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations'))
    BEGIN
        EXEC(N'CREATE INDEX IX_DFCE_Delegate ON dbo.DelegateFirstCollectionEvaluations (DelegateId, CreatedAtUtc DESC)');
    END
END
", cancellationToken: ct, commandTimeout: 30));
    }

    /// <summary>
    /// Idempotent: first successful submit wins; later calls return the stored row
    /// with <paramref name="created"/> = false (no second WrongSale forward).
    /// </summary>
    public async Task<(DelegateFirstCollectionEvalRow Row, bool Created)> SubmitAsync(
        int delegateId,
        string? delegateName,
        int customerId,
        int listId,
        int rating,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue,
        decimal? saleAmount,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (!FollowerSaleAuthRules.IsValidRating(rating))
            throw new InvalidOperationException("التقييم يجب أن يكون من 1 إلى 10");
        if (!FollowerSaleAuthRules.IsValidNoteForRating(rating, note))
            throw new InvalidOperationException("الملاحظة إجبارية عندما يكون التقييم أقل من 7");
        if (!FollowerSaleAuthRules.IsValidShopMoney(shopStockEstimatedValue))
            throw new InvalidOperationException("قيمة بضاعة المحل مطلوبة ويجب أن تكون أكبر من صفر");
        if (!FollowerSaleAuthRules.IsValidShopMoney(estimatedDailyRevenue))
            throw new InvalidOperationException("قيمة الوارد اليومي مطلوبة ويجب أن تكون أكبر من صفر");

        await using var c = new SqlConnection(_cs);
        var existing = await c.QueryFirstOrDefaultAsync<DelegateFirstCollectionEvalRow>(new CommandDefinition(@"
SELECT TOP 1 * FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId = @CustomerId;",
            new { CustomerId = customerId }, cancellationToken: ct));
        if (existing != null)
            return (existing, false);

        try
        {
            var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.DelegateFirstCollectionEvaluations
    (CustomerId, ListId, DelegateId, DelegateName, Rating, Note,
     ShopStockEstimatedValue, EstimatedDailyRevenue, SaleAmount, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES
    (@CustomerId, @ListId, @DelegateId, @DelegateName, @Rating, @Note,
     @ShopStockEstimatedValue, @EstimatedDailyRevenue, @SaleAmount, SYSUTCDATETIME());",
                new
                {
                    CustomerId = customerId,
                    ListId = listId,
                    DelegateId = delegateId,
                    DelegateName = string.IsNullOrWhiteSpace(delegateName) ? null : delegateName.Trim(),
                    Rating = rating,
                    Note = FollowerSaleAuthRules.NormalizeNote(note),
                    ShopStockEstimatedValue = decimal.Round(shopStockEstimatedValue, 0, MidpointRounding.AwayFromZero),
                    EstimatedDailyRevenue = decimal.Round(estimatedDailyRevenue, 0, MidpointRounding.AwayFromZero),
                    SaleAmount = saleAmount is > 0
                        ? decimal.Round(saleAmount.Value, 0, MidpointRounding.AwayFromZero)
                        : (decimal?)null
                },
                cancellationToken: ct));

            var row = await c.QueryFirstAsync<DelegateFirstCollectionEvalRow>(new CommandDefinition(
                "SELECT TOP 1 * FROM dbo.DelegateFirstCollectionEvaluations WHERE Id = @Id",
                new { Id = id }, cancellationToken: ct));
            return (row!, true);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            var raced = await c.QueryFirstAsync<DelegateFirstCollectionEvalRow>(new CommandDefinition(@"
SELECT TOP 1 * FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId = @CustomerId;",
                new { CustomerId = customerId }, cancellationToken: ct));
            return (raced, false);
        }
    }

    public async Task<DelegateFirstCollectionEvalRow?> GetForCustomerAsync(int customerId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        return await c.QueryFirstOrDefaultAsync<DelegateFirstCollectionEvalRow>(new CommandDefinition(@"
SELECT TOP 1 * FROM dbo.DelegateFirstCollectionEvaluations WHERE CustomerId = @CustomerId;",
            new { CustomerId = customerId }, cancellationToken: ct));
    }
}
