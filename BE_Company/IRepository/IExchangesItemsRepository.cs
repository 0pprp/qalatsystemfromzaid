using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IExchangesItemsRepository
    {
        Task<ExchangesItemsGetDTO?> ExchangesItems_Create(ExchangesItemsPostDTO exchangesItemsPostDTO);
        Task<ExchangesItemsGetDTO?> ExchangesItems_Update(int? exchangeItemID, ExchangesItemsPutDTO exchangesItemsPutDTO);
        Task<bool?> ExchangesItems_Delete (int? exchangeItemID, int? userDeleteID);
        Task<IEnumerable<ExchangesItemsGetDTO>?> ExchangesItems_GetAll();
    }
}
