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
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersPaymentsGetDTO>("CustomersPayments_GetByCustomerID",
                new
                {
                    CustomerID = customerID,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> CustomersPayments_Delete(int? customerPaymentID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("CustomersPayments_Delete",
                new
                {
                    CustomerPaymentID = customerPaymentID,
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<CustomersPaymentsGetDTO?> CustomersPayments_Create(CustomersPaymentsPostDTO customersPaymentsPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<CustomersPaymentsGetDTO>("CustomersPayments_Create",
                new
                {
                    UserID = customersPaymentsPostDTO.UserCreateID,
                    CustomerID = customersPaymentsPostDTO.CustomerID,
                    DateCreate = customersPaymentsPostDTO.PaymentDate,
                    Amount = customersPaymentsPostDTO.PaymentAmount / 1448,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
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
