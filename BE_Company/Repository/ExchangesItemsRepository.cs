using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class ExchangesItemsRepository : IExchangesItemsRepository
    {
        private readonly string _connectionString;

        public ExchangesItemsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<ExchangesItemsGetDTO?> ExchangesItems_Create(ExchangesItemsPostDTO exchangesItemsPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<ExchangesItemsGetDTO>("ExchangesItems_Create",
                new
                {
                    UserCreateID = exchangesItemsPostDTO.UserCreateID,
                    ExchangeItemName = exchangesItemsPostDTO.ExchangeItemName,
                    LimitAmount = exchangesItemsPostDTO.LimitAmount
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<ExchangesItemsGetDTO?> ExchangesItems_Update(int? exchangeItemID, ExchangesItemsPutDTO exchangesItemsPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<ExchangesItemsGetDTO>("ExchangesItems_Update",
                new
                {
                    ExchangeItemID = exchangeItemID,
                    UserUpdateID = exchangesItemsPutDTO.UserUpdateID,
                    ExchangeItemName = exchangesItemsPutDTO.ExchangeItemName,
                    LimitAmount = exchangesItemsPutDTO.LimitAmount
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> ExchangesItems_Delete(int? exchangeItemID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("ExchangesItems_Delete",
                new { ExchangeItemID = exchangeItemID, UserDeleteID = userDeleteID },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<IEnumerable<ExchangesItemsGetDTO>?> ExchangesItems_GetAll()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<ExchangesItemsGetDTO>("ExchangesItems_GetAll",
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }
    }
}
