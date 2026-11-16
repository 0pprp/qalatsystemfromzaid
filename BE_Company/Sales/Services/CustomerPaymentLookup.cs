using BE_Company.Sales;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace BE_Company.Sales.Services
{
    public interface ICustomerPaymentLookup
    {
        Task<IReadOnlyList<CustomerPaymentSlice>> GetByCustomerIdsAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct);
    }

    public sealed class CustomerPaymentLookup : ICustomerPaymentLookup
    {
        private readonly string _connectionString;

        public CustomerPaymentLookup(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")
                ?? throw new InvalidOperationException("Missing connection string DataBaseConnection.");
        }

        public async Task<IReadOnlyList<CustomerPaymentSlice>> GetByCustomerIdsAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct)
        {
            var ids = customerIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return [];
            }

            try
            {
                var merged = new List<CustomerPaymentSlice>();
                await using var connection = new SqlConnection(_connectionString);
                foreach (var chunk in SqlInClauseBatch.Chunk(ids))
                {
                    ct.ThrowIfCancellationRequested();
                    var rows = await connection.QueryAsync<CustomerPaymentSlice>(new CommandDefinition(@"
SELECT
    V.CustomerID AS CustomerId,
    V.PaymentDate AS PaymentDate,
    CAST(ISNULL(V.AmountDenar, 0) AS float) AS Amount
FROM dbo.View_CustomersPaymentsDelegate V
WHERE V.CustomerID IN @Ids
  AND V.PaymentDate IS NOT NULL
  AND ISNULL(V.AmountDenar, 0) > 0;",
                        new { Ids = chunk },
                        cancellationToken: ct));
                    merged.AddRange(rows);
                }

                return merged;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return [];
            }
        }
    }
}
