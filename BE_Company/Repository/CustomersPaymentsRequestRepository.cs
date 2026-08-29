using BE_Company.DTO;
using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class CustomersPaymentsRequestRepository : ICustomersPaymentsRequestRepository
    {
        private readonly string _connectionString;

        public CustomersPaymentsRequestRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<IEnumerable<CustomersPaymentsRequestGetDTO>?> CustomersPaymentsRequest_GetAll(string? customerName, int? delegateID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersPaymentsRequestGetDTO>("CustomersPaymentsRequest_GetAll",
                new
                {
                    CustomerName = customerName,
                    DelegateID = delegateID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> CustomersPaymentsRequest_Delete(int? customersPaymentsRequestID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteScalarAsync<bool>("CustomersPaymentsRequest_Delete",
                new
                {
                    CustomersPaymentsRequestID = customersPaymentsRequestID,
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> CustomersPaymentsRequest_DeleteAll(int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteScalarAsync<bool>("CustomersPaymentsRequest_DeleteAll",
                new
                {
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> CustomersPaymentsRequest_DeleteSame()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteScalarAsync<bool>("CustomersPaymentsRequest_DeleteSame", commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> CustomersPaymentsRequest_Approve(int? customersPaymentsRequestID, int? userCreateID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteScalarAsync<bool>("CustomersPaymentsRequest_Approve",
                new
                {
                    CustomersPaymentsRequestID = customersPaymentsRequestID,
                    UserCreateID = userCreateID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> CustomersPaymentsRequest_ChangeDate(DateTime? paymentDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteScalarAsync<bool>("CustomersPaymentsRequest_ChangeDate",
                new
                {
                    PaymentDate = paymentDate,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> CustomersPaymentsRequest_ApproveAll(int? userCreateID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteScalarAsync<bool>(
                    "CustomersPaymentsRequest_ApproveAll",
                    new
                    {
                        UserCreateID = userCreateID
                    },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 600  
                );
                return result;
            }
        }

        public async Task<bool?> CustomersPaymentsRequest_DeleteSelect(string? paymentlist, int? userID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteScalarAsync<bool>("CustomersPaymentsRequest_DeleteSelect",
                new
                {
                    Paymentlist = paymentlist,
                    UserID = userID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }
    }
}
