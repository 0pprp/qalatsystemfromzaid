using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IStoresRepository
    {
        Task<StoresGetDTO?> Stores_Create(StoresPostDTO storesPostDTO);
        Task<StoresGetDTO?> Stores_Update(int? userID, StoresPutDTO storesPutDTO);
        Task<bool?> Stores_Delete(int? storeID, int? userDeleteID);
        Task<IEnumerable<StoresGetDTO>?> Stores_GetAll(string? textSearch);
        Task<IEnumerable<StoresDataGetDTO>?> StoresData_GetAll();
    }
}
