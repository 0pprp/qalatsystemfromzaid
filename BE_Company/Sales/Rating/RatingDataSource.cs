using BE_Company.Sales;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Rating;

public sealed class RatingDataSource : IRatingDataSource
{
    private static readonly object ColumnLock = new();
    private static bool _columnEnsured;

    private readonly string _connectionString;

    public RatingDataSource(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DataBaseConnection")
            ?? throw new InvalidOperationException("Missing connection string DataBaseConnection.");
    }

    private const string FactsSelect = @"
SELECT
    C.CustomerID AS CustomerId,
    C.DelegateID AS DelegateId,
    CAST(ISNULL(C.IsLegal, 0) AS bit) AS IsLegal,
    CAST(ISNULL(C.IsFakeSale, 0) AS bit) AS IsFakeSale,
    S.DateSaleDevice,
    P.LastPaymentDate,
    CAST(ISNULL(S.AmountTotalSales, 0) AS float) AS AmountTotalSales,
    CAST(ISNULL(P.ReceiptsTotal, 0) AS float) AS ReceiptsTotal,
    CAST(ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) AS float) AS AmountRemaining,
    CAST(ISNULL(P.ReceiptCount, 0) AS int) AS ReceiptCount,
    CAST(ISNULL(C.RatingEquationFactor, 1) AS float) AS RatingEquationFactor
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
        ROUND(ISNULL(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal,
        COUNT(V.CustomerPaymentID) AS ReceiptCount
    FROM dbo.View_CustomersPaymentsDelegate V
    WHERE V.CustomerID = C.CustomerID
) P";

    public async Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByCustomerIdsAsync(
        IReadOnlyList<int> customerIds, CancellationToken ct = default)
    {
        if (customerIds.Count == 0)
        {
            return [];
        }

        var facts = await QueryFactsInBatchesAsync(
            customerIds,
            FactsSelect + " WHERE C.CustomerID IN @Ids;",
            ct);
        return await EnrichFromModuleSalesAsync(facts, ct);
    }

    public async Task<CustomerRatingFacts?> GetFactByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        var rows = await GetFactsByCustomerIdsAsync([customerId], ct);
        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByListIdsAsync(
        IReadOnlyList<int> listIds, CancellationToken ct = default)
    {
        if (listIds.Count == 0)
        {
            return [];
        }

        var facts = await QueryFactsInBatchesAsync(
            listIds,
            FactsSelect + " WHERE C.DelegateID IN @Ids;",
            ct);
        return await EnrichFromModuleSalesAsync(facts, ct);
    }

    private async Task<IReadOnlyList<CustomerRatingFacts>> QueryFactsInBatchesAsync(
        IReadOnlyList<int> ids,
        string sql,
        CancellationToken ct)
    {
        var merged = new List<CustomerRatingFacts>();
        var seen = new HashSet<int>();
        await using var connection = new SqlConnection(_connectionString);
        await EnsureRatingEquationColumnAsync(connection, ct);
        foreach (var chunk in SqlInClauseBatch.Chunk(ids))
        {
            ct.ThrowIfCancellationRequested();
            var rows = await connection.QueryAsync<CustomerRatingFactsRow>(new CommandDefinition(
                sql,
                new { Ids = chunk },
                cancellationToken: ct));
            foreach (var row in rows)
            {
                if (!seen.Add(row.CustomerId))
                {
                    continue;
                }

                merged.Add(Map(row));
            }
        }

        return merged;
    }

    private static async Task EnsureRatingEquationColumnAsync(SqlConnection connection, CancellationToken ct)
    {
        if (_columnEnsured)
            return;

        lock (ColumnLock)
        {
            if (_columnEnsured)
                return;
        }

        await connection.ExecuteAsync(new CommandDefinition(@"
IF COL_LENGTH(N'dbo.Customers', N'RatingEquationFactor') IS NULL
BEGIN
    ALTER TABLE dbo.Customers
        ADD RatingEquationFactor FLOAT NOT NULL
            CONSTRAINT DF_Customers_RatingEquationFactor DEFAULT (1);
END",
            cancellationToken: ct));

        lock (ColumnLock)
        {
            _columnEnsured = true;
        }
    }

    private async Task<IReadOnlyList<CustomerRatingFacts>> EnrichFromModuleSalesAsync(
        IReadOnlyList<CustomerRatingFacts> facts,
        CancellationToken ct)
    {
        var needIds = facts
            .Where(f => f.AmountTotalSales <= 0 || f.DateSaleDevice is null)
            .Select(f => f.CustomerId)
            .Distinct()
            .ToList();
        if (needIds.Count == 0)
        {
            return facts;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            var draftsExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (draftsExists != 1)
            {
                return facts;
            }

            var map = new Dictionary<int, ModuleSaleRatingSupplement>();
            foreach (var chunk in SqlInClauseBatch.Chunk(needIds))
            {
                var rows = await connection.QueryAsync<ModuleSaleRow>(new CommandDefinition(@"
SELECT
    CustomerId,
    MAX(COALESCE(CompletedAt, CreatedAt)) AS SaleDate,
    CAST(SUM(FinalSalePrice) AS float) AS AmountTotal,
    CAST(SUM(CASE WHEN DownPayment > 0 THEN DownPayment ELSE 0 END) AS float) AS DownPayment
FROM dbo.SalesDrafts
WHERE CustomerId IN @Ids
  AND Status IN (N'Completed', N'DocumentsReady', N'DocumentsPending')
  AND FinalSalePrice > 0
GROUP BY CustomerId;",
                    new { Ids = chunk }, cancellationToken: ct));
                foreach (var row in rows)
                {
                    if (row.CustomerId <= 0 || row.AmountTotal <= 0)
                    {
                        continue;
                    }

                    map[row.CustomerId] = new ModuleSaleRatingSupplement(
                        row.CustomerId,
                        row.SaleDate,
                        row.AmountTotal,
                        row.DownPayment);
                }
            }

            return CustomerRatingFactsEnricher.ApplyAll(facts, map);
        }
        catch
        {
            return facts;
        }
    }

    private static CustomerRatingFacts Map(CustomerRatingFactsRow row) =>
        new(
            row.CustomerId,
            row.DelegateId,
            row.IsLegal,
            row.IsFakeSale,
            row.DateSaleDevice,
            row.LastPaymentDate,
            row.AmountTotalSales,
            row.ReceiptsTotal,
            row.AmountRemaining,
            row.ReceiptCount,
            row.RatingEquationFactor <= 0 ? 1.0 : row.RatingEquationFactor);

    private sealed class CustomerRatingFactsRow
    {
        public int CustomerId { get; init; }
        public int? DelegateId { get; init; }
        public bool IsLegal { get; init; }
        public bool IsFakeSale { get; init; }
        public DateTime? DateSaleDevice { get; init; }
        public DateTime? LastPaymentDate { get; init; }
        public double AmountTotalSales { get; init; }
        public double ReceiptsTotal { get; init; }
        public double AmountRemaining { get; init; }
        public int ReceiptCount { get; init; }
        public double RatingEquationFactor { get; init; } = 1.0;
    }

    private sealed class ModuleSaleRow
    {
        public int CustomerId { get; init; }
        public DateTime? SaleDate { get; init; }
        public double AmountTotal { get; init; }
        public double DownPayment { get; init; }
    }
}
