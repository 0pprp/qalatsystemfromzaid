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
    CAST(ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) AS float) AS AmountRemaining
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
) P";

    public async Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByCustomerIdsAsync(
        IReadOnlyList<int> customerIds, CancellationToken ct = default)
    {
        if (customerIds.Count == 0) return [];
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<CustomerRatingFactsRow>(new CommandDefinition(
            FactsSelect + " WHERE C.CustomerID IN @Ids;",
            new { Ids = customerIds.ToArray() },
            cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<CustomerRatingFacts?> GetFactByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        var rows = await GetFactsByCustomerIdsAsync([customerId], ct);
        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByListIdsAsync(
        IReadOnlyList<int> listIds, CancellationToken ct = default)
    {
        if (listIds.Count == 0) return [];
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync<CustomerRatingFactsRow>(new CommandDefinition(
            FactsSelect + " WHERE C.DelegateID IN @Ids;",
            new { Ids = listIds.ToArray() },
            cancellationToken: ct));
        return rows.Select(Map).ToList();
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
            row.AmountRemaining);

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
    }
}
