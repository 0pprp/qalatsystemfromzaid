using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Linq;

namespace BE_DelegateWebApplication.Repository
{
    public class CustomersPaymentsRequestsRepository : ICustomersPaymentsRequestsRepository
    {
        private readonly string _connectionString;

        public CustomersPaymentsRequestsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<bool?> PostSelectPaymentCustomerTemporary(CustomersPaymentsRequestsPostDTO? customersPaymentsRequestsPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.ExecuteAsync("PostSelectPaymentCustomerTemporary",
                new
                {
                    CustomerID = customersPaymentsRequestsPostDTO?.CustomerId,
                    DelegateID = customersPaymentsRequestsPostDTO?.DelegateId,
                    Amount = customersPaymentsRequestsPostDTO?.Amount,
                    Location = customersPaymentsRequestsPostDTO?.Location
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 600);
                return true;
            }
        }

        public async Task<bool?> PostSelectPaymentCustomerTemporaryMulti(List<CustomersPaymentsRequestsPostDTO>? customersPaymentsRequestsPostDTO)
        {
            if (customersPaymentsRequestsPostDTO == null || !customersPaymentsRequestsPostDTO.Any())
                return true;

            var dt = new DataTable();
            dt.Columns.Add("CustomerID", typeof(int));
            dt.Columns.Add("DelegateID", typeof(int));
            dt.Columns.Add("Amount", typeof(double));
            dt.Columns.Add("Location", typeof(string));

            foreach (var item in customersPaymentsRequestsPostDTO)
            {
                dt.Rows.Add(item.CustomerId, item.DelegateId, item.Amount, item.Location);
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.ExecuteAsync("PostSelectPaymentCustomerTemporaryMulti",
                    new { PaymentData = dt.AsTableValuedParameter("dbo.CustomersPaymentsRequestType") },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 1000);

                return true;
            }
        }

        public async Task<IEnumerable<CustomersPaymentsRequestsGetDTO>?> GetCustomersPaymentsRequestsByDelegateID(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersPaymentsRequestsGetDTO>("GetCustomersPaymentsRequestsByDelegateID",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }
    }
}
