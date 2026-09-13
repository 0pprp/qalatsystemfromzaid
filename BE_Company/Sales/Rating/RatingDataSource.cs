using BE_Company.Sales;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Rating;

public sealed class RatingDataSource : IRatingDataSource
{
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
    CAST(ISNULL(P.ReceiptCount, 0) AS int) AS ReceiptCount
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

        return await QueryFactsInBatchesAsync(
            customerIds,
            FactsSelect + " WHERE C.CustomerID IN @Ids;",
            ct);
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

        return await QueryFactsInBatchesAsync(
            listIds,
            FactsSelect + " WHERE C.DelegateID IN @Ids;",
            ct);
    }

    private async Task<IReadOnlyList<CustomerRatingFacts>> QueryFactsInBatchesAsync(
        IReadOnlyList<int> ids,
        string sql,
        CancellationToken ct)
    {
        var merged = new List<CustomerRatingFacts>();
        var seen = new HashSet<int>();
        await using var connection = new SqlConnection(_connectionString);
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
            row.ReceiptCount);

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
    }
}
