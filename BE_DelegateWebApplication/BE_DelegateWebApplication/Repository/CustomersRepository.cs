using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_DelegateWebApplication.Repository
{
    public class CustomersRepository : ICustomersRepository
    {
        private readonly string _connectionString;

        public CustomersRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<CustomerGetDTO?> GetCustomersDataInfo(int? customerId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<CustomerGetDTO>("GetCustomersDataInfo",
                new
                {
                    CustomerID = customerId
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomerGetDTO>?> GetCustomersDelegateAll(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomerGetDTO>("GetCustomersDelegateAll",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 600);
                return result;
            }
        }

        public async Task<IEnumerable<CustomerGetDTO>?> GetCustomersByDelegatePermissions(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomerGetDTO>("GetCustomersByDelegatePermissions",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 600);
                return result;
            }
        }

        public async Task<DateReceiptsWeekGetDTO?> GetDateWeek()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<DateReceiptsWeekGetDTO>("GetDateWeek",
                commandType: CommandType.StoredProcedure,
                commandTimeout: 600);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersFollowGetDTO>?> Customers_Follow(int? delegateID, DateTime? paymentDate, string? showType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersFollowGetDTO>(
                    "Customers_Follow",
                    new
                    {
                        DelegateID = delegateID,
                        PaymentDate = paymentDate,
                        ShowType = showType
                    },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 600);
                return result;
            }
        }
    }
}
