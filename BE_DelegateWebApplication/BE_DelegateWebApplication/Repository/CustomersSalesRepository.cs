using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_DelegateWebApplication.Repository
{
    public class CustomersSalesRepository : ICustomersSalesRepository
    {
        private readonly string _connectionString;

        public CustomersSalesRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomerDate(int? customerId, DateTime? dateCreate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = (await connection.QueryAsync<CustomersSalesGetDTO>("GetCustomersSalesCustomerDate",
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

        public async Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomer(int? customerId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = (await connection.QueryAsync<CustomersSalesGetDTO>("GetCustomersSalesCustomer",
                new
                {
                    CustomerID = customerId
                },
                commandType: CommandType.StoredProcedure)).ToList();
                await LegalCustomerFlagStamp.ApplyAsync(connection, result, r => r.CustomerId, (r, legal) => r.IsLegal = legal);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomerLast(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = (await connection.QueryAsync<CustomersSalesGetDTO>("GetCustomersSalesCustomerLast",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure)).ToList();
                await LegalCustomerFlagStamp.ApplyAsync(connection, result, r => r.CustomerId, (r, legal) => r.IsLegal = legal);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomerName(int? id, string? customerName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = (await connection.QueryAsync<CustomersSalesGetDTO>("GetCustomersSalesCustomerName",
                new
                {
                    DelegateID = id,
                    CustomerName = customerName
                },
                commandType: CommandType.StoredProcedure)).ToList();
                await LegalCustomerFlagStamp.ApplyAsync(connection, result, r => r.CustomerId, (r, legal) => r.IsLegal = legal);
                return result;
            }
        }
    }
}
