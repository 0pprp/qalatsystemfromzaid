using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IBuysRepository
    {
        Task<BuysGetDTO?> Buys_Create(BuysPostDTO buysPostDTO);
        Task<bool?> Buys_Delete(int? buyID,int? userDeleteID);
        Task<IEnumerable<BuysGetDTO>> Buys_GetByDateByTextSearch(DateTime? fromDate,DateTime toDate,string? textSearch);
        Task<bool?> Buys_UpdateDate(int? buyID, BuysPutDateCreateDTO buysPutDateCreate);
    }
}
