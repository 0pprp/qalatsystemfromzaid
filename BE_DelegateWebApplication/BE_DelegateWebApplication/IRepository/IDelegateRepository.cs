using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface IDelegateRepository
    {
        Task<DelegateGetDTO?> GetDelegateLogin(string? asyncID);
        Task<DelegateGetDTO?> GetDelegateCheckLogout(string? asyncID);
        Task<DelegateInfoGetDTO?> GetDelegateTitle(int? delegateId);
        Task<IEnumerable<SelectDelegateGetDTO>?> GetDelegateSelect(int? delegateId);
        Task<bool> IsFollowerListLinked(int fatherId, int childId);
    }
}
