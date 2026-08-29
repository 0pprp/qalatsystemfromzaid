using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IItemsRepository
    {
        Task<ItemsGetDTO?> Items_Create(ItemsPostDTO itemsPostDTO);
        Task<ItemsGetDTO?> Items_Update(int? itemID,ItemsPutDTO itemsPutDTO);
        Task<bool?> Items_Delete(int? itemID,int? userDeleteID);
        Task<IEnumerable<ItemsGetDTO>> Items_GetAll(int? storeID,string? itemName,string? showType);
        Task<IEnumerable<ItemsBuyDataDTO>> Items_GetByItemBuy(int? storeID);
        Task<IEnumerable<ItemsSaleDataDTO>> Items_GetByItemSale(int? storeID);
    }
}
