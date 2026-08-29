using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class ItemsRepository : IItemsRepository
    {
        private readonly string _connectionString;

        public ItemsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<ItemsGetDTO?> Items_Create(ItemsPostDTO itemsPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<ItemsGetDTO>("Items_Create",
                new
                {
                    StoreID = itemsPostDTO.StoreID,
                    UserCreateID = itemsPostDTO.UserCreateID,
                    ItemName = itemsPostDTO.ItemName,
                    ItemPriceDenar = itemsPostDTO.ItemPriceDenar,
                    ItemCostDenar = itemsPostDTO.ItemCostDenar,
                    AmountDayDenar = itemsPostDTO.AmountDayDenar,
                    Quantity = itemsPostDTO.Quantity,
                    NotificationNumber = itemsPostDTO.NotificationNumber,
                    Notes = itemsPostDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<ItemsGetDTO?> Items_Update(int? itemID, ItemsPutDTO itemsPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<ItemsGetDTO>("Items_Update",
                new
                {
                    ItemID = itemID,
                    StoreID = itemsPutDTO.StoreID,
                    UserUpdateID = itemsPutDTO.UserUpdateID,
                    ItemName = itemsPutDTO.ItemName,
                    ItemPriceDenar = itemsPutDTO.ItemPriceDenar,
                    ItemCostDenar = itemsPutDTO.ItemCostDenar,
                    AmountDayDenar = itemsPutDTO.AmountDayDenar,
                    Quantity = itemsPutDTO.Quantity,
                    NotificationNumber = itemsPutDTO.NotificationNumber,
                    Notes = itemsPutDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool?> Items_Delete(int? itemID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Items_Delete",
                new
                {
                    ItemID = itemID,
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return result > 0;
            }
        }

        public async Task<IEnumerable<ItemsGetDTO>> Items_GetAll(int? storeID, string? itemName, string? showType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryAsync<ItemsGetDTO>("Items_GetAll",
                new
                {
                    StoreID = storeID,
                    ItemName = itemName,
                    ShowType = showType
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<IEnumerable<ItemsBuyDataDTO>> Items_GetByItemBuy(int? storeID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<ItemsBuyDataDTO>("Items_GetByItemBuy",
                new
                {
                    StoreID = storeID,
                },
                commandType: CommandType.StoredProcedure);
                var resultFinal = result.Select(c => 
                {
                    decimal itemCostDenar = Math.Round(decimal.Parse(c.ItemCostDenar.ToString()));
                    decimal itemPriceDenar = Math.Round(decimal.Parse(c.ItemPriceDenar.ToString()));
                    string itemCostDenarFinal = itemCostDenar.ToString("N0");
                    string itemPriceDenarFinal = itemPriceDenar.ToString("N0");
                    c.ItemName = c.ItemName + " - سعر الشراء  (" + itemCostDenarFinal + " دع)" + " - سعر البيع  (" + itemPriceDenarFinal + " دع)";
                    c.ItemCostDenar = (double)itemCostDenar;
                    return c;
                });
                return resultFinal;
            }
        }

        public async Task<IEnumerable<ItemsSaleDataDTO>> Items_GetByItemSale(int? storeID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<ItemsSaleDataDTO>("Items_GetByItemSale",
                new
                {
                    StoreID = storeID,
                },
                commandType: CommandType.StoredProcedure);
                var resultFinal = result.Select(c =>
                {
                    decimal itemPriceDenar = Math.Round(decimal.Parse(c.ItemPriceDenar.ToString()));
                    decimal amountDayDenar = Math.Round(decimal.Parse(c.AmountDayDenar.ToString()));
                    string itemCostDenarFinal = itemPriceDenar.ToString("N0");
                    c.ItemName = c.ItemName + " - سعر البيع  (" + itemCostDenarFinal + " دع)" + " - الكمية  (" + c.Quantity + ")";
                    c.ItemPriceDenar = (double)itemPriceDenar;
                    c.AmountDayDenar = (double)amountDayDenar;
                    return c;
                });
                return resultFinal;
            }
        }
    }
}
