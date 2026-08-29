using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IDelegatesRepository
    {
        Task<DelegatesGetDTO?> Delegates_Create(DelegatesPostDTO delegatesPostDTO);
        Task<DelegatesGetDTO?> Delegates_Update(DelegatesPutDTO delegatesPutDTO);
        Task<bool?> Delegates_Delete(int? delegateID,int? userDeleteID);
        Task<IEnumerable<DelegatesGetDTO>?> Delegates_GetAll(string? delegateName);
        Task<IEnumerable<DelegatesDataGetDTO>?> Delegates_GetDataAll();
        Task<IEnumerable<StatisticsGetDTO>?> Delegates_Statistics(DateTime? fromDate,DateTime? toDate);
        Task<IEnumerable<DelegatesDashboardDataGetDTO>?> Delegates_GetDashboardData(DateTime? fromDate,DateTime? toDate);
        Task<IEnumerable<NoStatisticsGetDTO>?> Delegates_NoStatistics(DateTime? fromDate,DateTime? toDate);
        Task<IEnumerable<SelectDelegateGetDTO>?> SelectDelegate_GetByDelegateID(int? delegateID);
        Task<SelectDelegateGetDTO?> SelectDelegate_Create(SelectDelegatePostDTO selectDelegatePostDTO);
        Task<bool?> SelectDelegate_Delete(int? selectDelegateID);
    }
}
