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

    public DelegateFirstCollectionEvalService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await using var c = new SqlConnection(_cs);
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
    CREATE UNIQUE INDEX UX_DFCE_Customer ON dbo.DelegateFirstCollectionEvaluations (CustomerId);
    CREATE INDEX IX_DFCE_Delegate ON dbo.DelegateFirstCollectionEvaluations (DelegateId, CreatedAtUtc DESC);
END
", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
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
