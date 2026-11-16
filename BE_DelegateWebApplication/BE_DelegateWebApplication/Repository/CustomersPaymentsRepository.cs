using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_DelegateWebApplication.Repository
{
    public class CustomersPaymentsRepository : ICustomersPaymentsRepository
    {
        private readonly string _connectionString;

        public CustomersPaymentsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<IEnumerable<CustomersPaymentGetDTO>?> GetCustomersPaymentsCustomerDate(int? customerId, DateTime? dateCreate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = (await connection.QueryAsync<CustomersPaymentGetDTO>("GetCustomersPaymentsCustomerDate",
                new
                {
                    CustomerID = customerId,
                    DateCreate = dateCreate
                },
                commandType: CommandType.StoredProcedure)).ToList();
                await LegalCustomerFlagStamp.ApplyAsync(connection, result, r => r.CustomerId, (r, legal) => r.IsLegal = legal);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersPaymentGetDTO>?> GetCustomersPaymentsCustomer(int? customerId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = (await connection.QueryAsync<CustomersPaymentGetDTO>("GetCustomersPaymentsCustomer",
                new
                {
                    CustomerID = customerId
                },
                commandType: CommandType.StoredProcedure)).ToList();
                await LegalCustomerFlagStamp.ApplyAsync(connection, result, r => r.CustomerId, (r, legal) => r.IsLegal = legal);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersPaymentGetDTO>?> GetCustomersPaymentsCustomerName(int? id, string? customerName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = (await connection.QueryAsync<CustomersPaymentGetDTO>("GetCustomersPaymentsByCustomerNameByDelegateID",
                new
                {
                    DelegateID = id,
                    CustomerName = customerName,
                },
                commandType: CommandType.StoredProcedure)).ToList();
                await LegalCustomerFlagStamp.ApplyAsync(connection, result, r => r.CustomerId, (r, legal) => r.IsLegal = legal);
                return result;
            }
        }
    }
}
