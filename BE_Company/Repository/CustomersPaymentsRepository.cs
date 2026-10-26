using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class CustomersPaymentsRepository : ICustomersPaymentsRepository
    {
        private readonly string _connectionString;

        public CustomersPaymentsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<IEnumerable<CustomersPaymentsGetDTO>?> CustomersPayments_GetAll(DateTime? fromDate, DateTime? toDate, int? delegateID, string? textSearch)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersPaymentsGetDTO>("CustomersPayments_GetAll",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DelegateID = delegateID,
                    TextSearch = textSearch
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersPaymentsGetDTO>?> CustomersPayments_GetByCustomerID(int? customerID)
        {
            return await CustomersPayments_GetByCustomerID(customerID, null, null);
        }

        /// <summary>
        /// Portal path: filter dates in SQL (not in-memory) when bounds are provided.
        /// Falls back to classic SP when both dates are null.
        /// </summary>
        public async Task<IEnumerable<CustomersPaymentsGetDTO>?> CustomersPayments_GetByCustomerID(
            int? customerID,
            DateTime? fromDate,
            DateTime? toDate)
        {
            using var connection = new SqlConnection(_connectionString);
            if (fromDate == null && toDate == null)
            {
                var result = await connection.QueryAsync<CustomersPaymentsGetDTO>(
                    "CustomersPayments_GetByCustomerID",
                    new { CustomerID = customerID },
                    commandType: CommandType.StoredProcedure);
                return result;
            }

            // Prefer view when present; filter PaymentDate server-side.
            const string sql = """
SELECT *
FROM dbo.View_CustomersPayments
WHERE CustomerID = @CustomerID
  AND (@FromDate IS NULL OR PaymentDate >= @FromDate)
  AND (@ToDate IS NULL OR PaymentDate <= @ToDate)
ORDER BY PaymentDate DESC;
""";
            try
            {
                return await connection.QueryAsync<CustomersPaymentsGetDTO>(sql, new
                {
                    CustomerID = customerID,
                    FromDate = fromDate,
                    ToDate = toDate
                });
            }
            catch (SqlException)
            {
                // View missing on older branch DBs — fetch all via SP then filter (documented fallback).
                var all = await connection.QueryAsync<CustomersPaymentsGetDTO>(
                    "CustomersPayments_GetByCustomerID",
                    new { CustomerID = customerID },
                    commandType: CommandType.StoredProcedure);
                var filtered = all.AsEnumerable();
                if (fromDate.HasValue)
                    filtered = filtered.Where(p => p.PaymentDate >= fromDate.Value);
                if (toDate.HasValue)
                    filtered = filtered.Where(p => p.PaymentDate <= toDate.Value);
                return filtered.OrderByDescending(p => p.PaymentDate);
            }
        }

        public async Task<bool?> CustomersPayments_Delete(int? customerPaymentID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await CustomersPayments_Delete(customerPaymentID, userDeleteID, connection, null);
            }
        }

        public async Task<bool?> CustomersPayments_Delete(
            int? customerPaymentID,
            int? userDeleteID,
            IDbConnection connection,
            IDbTransaction? transaction,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "CustomersPayments_Delete",
                new
                {
                    CustomerPaymentID = customerPaymentID,
                    UserDeleteID = userDeleteID
                },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
            return true;
        }

        public async Task<CustomersPaymentsGetDTO?> CustomersPayments_Create(CustomersPaymentsPostDTO customersPaymentsPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await CustomersPayments_Create(customersPaymentsPostDTO, connection, null);
            }
        }

        public Task<CustomersPaymentsGetDTO?> CustomersPayments_Create(
            CustomersPaymentsPostDTO customersPaymentsPostDTO,
            IDbConnection connection,
            IDbTransaction? transaction,
            CancellationToken cancellationToken = default)
        {
            return connection.QuerySingleOrDefaultAsync<CustomersPaymentsGetDTO>(new CommandDefinition(
                "CustomersPayments_Create",
                new
                {
                    UserID = customersPaymentsPostDTO.UserCreateID,
                    CustomerID = customersPaymentsPostDTO.CustomerID,
                    DateCreate = customersPaymentsPostDTO.PaymentDate,
                    Amount = customersPaymentsPostDTO.PaymentAmount / 1448,
                },
                transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        }

        public async Task<bool?> CustomersPayments_ChangePaymentDate(string? paymentlist, DateTime? newDate, int? userID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("CustomersPayments_ChangePaymentDate",
                new
                {
                    Paymentlist = paymentlist,
                    NewDate = newDate,
                    UserID = userID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> CustomersPayments_DeleteSelect(string? paymentlist, int? userID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("CustomersPayments_DeleteSelect",
                new
                {
                    Paymentlist = paymentlist,
                    UserID = userID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }
    }
}
