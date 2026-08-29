using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IAccountsRepository
    {
        Task<IEnumerable<BoxsGetDTO>?> Boxs_GetAll();
        Task<IEnumerable<BoxsDataGetDTO>?> Boxs_GetAllData();
        Task<IEnumerable<AddToBoxsGetDTO>?> AddToBoxs_GetByDateByBoxID(DateTime? fromDate, DateTime? toDate, int? boxID);
        Task<IEnumerable<WithdrawalFromBoxsGetDTO>?> WithdrawalFromBoxs_GetByDateByBoxID(DateTime? fromDate, DateTime? toDate, int? boxID);
        Task<IEnumerable<TransferBoxsGetDTO>?> TransferBoxs_GetByDate(DateTime? fromDate, DateTime? toDate);
        Task<bool?> Boxs_Delete(int? boxID, int? userDeleteID);
        Task<bool?> AddToBox_Delete(int? addToBoxID, int? userDeleteID);
        Task<bool?> WithdrawalFromBox_Delete(int? withdrawalFromBoxID, int? userDeleteID);
        Task<bool?> TransferBoxs_Delete(int? transferBoxID, int? userDeleteID);
        Task<bool?> AddToBox_Create(AccountsPostDTO accountsPostDTO);
        Task<bool?> WithdrawalFromBox_Create(AccountsPostDTO accountsPostDTO);
        Task<bool?> TransferBoxs_Create(AccountsPostDTO accountsPostDTO);
        Task<bool?> Boxs_Create(BoxsPostDTO boxsPostDTO);
        Task<bool?> Boxs_Update(int? boxID,BoxsPutDTO boxsPutDTO);
    }
}
