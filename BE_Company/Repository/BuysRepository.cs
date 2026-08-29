using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class BuysRepository : IBuysRepository
    {
        private readonly string _connectionString;

        public BuysRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<IEnumerable<BuysGetDTO>> Buys_GetByDateByTextSearch(DateTime? fromDate, DateTime toDate, string? textSearch)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<BuysGetDTO>("Buys_GetByDateByTextSearch",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    TextSearch = textSearch
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<BuysGetDTO?> Buys_Create(BuysPostDTO buysPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                foreach (var item in buysPostDTO.Contents.Where(c => c.ItemID != null))
                {
                    await SelectItemBuyTemporaryPost_Create(new SelectItemBuyTemporaryPostDTO()
                    {
                        ItemID = item.ItemID,
                        Quantity = item.Quantity,
                        UserCreateID = buysPostDTO.UserCreateID
                    });
                }
                return await connection.QueryFirstOrDefaultAsync<BuysGetDTO>("Buys_Create",
                new
                {
                    SupplierID = buysPostDTO.SupplierID,
                    StoreID = buysPostDTO.StoreID,
                    BoxID = buysPostDTO.BoxID,
                    UserID = buysPostDTO.UserCreateID,
                    DateCreate = buysPostDTO.Date,
                    AmountPaidDenar = buysPostDTO.TotalAmountSpent / 1448,
                    FinalAmountTotalDenar = buysPostDTO.FinalTotalItemCostDenar / 1448,
                    AmountTotalDenar = buysPostDTO.AmountTotalDenar / 1448,
                    RemainingAmountDenar = buysPostDTO.RemainingAmountDenar / 1448,
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool?> SelectItemBuyTemporaryPost_Create(SelectItemBuyTemporaryPostDTO selectItemBuyTemporaryPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<bool>("SelectItemBuyTemporaryPost_Create",
                new
                {
                    ItemID = selectItemBuyTemporaryPostDTO.ItemID,
                    Quantity = selectItemBuyTemporaryPostDTO.Quantity,
                    UserCreateID = selectItemBuyTemporaryPostDTO.UserCreateID,
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool?> Buys_Delete(int? buyID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Buys_Delete",
                new
                {
                    BuyID = buyID,
                    UserID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> Buys_UpdateDate(int? buyID, BuysPutDateCreateDTO buysPutDateCreate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Buys_UpdateDate",
                new
                {
                    BuyID = buyID,
                    UpdateCreateID = buysPutDateCreate.UpdateCreateID,
                    DateCreate = buysPutDateCreate.DateCreate,
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }
    }
}
