using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BE_Company.Repository
{
    public class StoresRepository : IStoresRepository
    {
        private readonly string _connectionString;

        public StoresRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<StoresGetDTO?> Stores_Create(StoresPostDTO storesPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<StoresGetDTO>("Stores_Create",
                new
                {
                    UserCreateID = storesPostDTO.UserCreateID,
                    StoreName = storesPostDTO.StoreName,
                    StorePlace = storesPostDTO.StorePlace,
                    Notes = storesPostDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<StoresGetDTO?> Stores_Update(int? storeID, StoresPutDTO storesPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<StoresGetDTO>("Stores_Update",
                new
                {
                    StoreID = storeID,
                    UserUpdateID = storesPutDTO.UserUpdateID,
                    StoreName = storesPutDTO.StoreName,
                    StorePlace = storesPutDTO.StorePlace,
                    Notes = storesPutDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool?> Stores_Delete(int? storeID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Stores_Delete",
                new
                {
                    StoreID = storeID,
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<IEnumerable<StoresGetDTO>?> Stores_GetAll(string? textSearch)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<StoresGetDTO>("Stores_GetAll",
                new
                {
                    TextSearch = textSearch,
                }, 
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<StoresDataGetDTO>?> StoresData_GetAll()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<StoresDataGetDTO>("StoresData_GetAll",
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }
    }
}
