using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface ICustomersRepository
    {
        Task<CustomerGetDTO?> GetCustomersDataInfo(int? customerId);
        Task<IEnumerable<CustomerGetDTO>?> GetCustomersDelegateAll(int? delegateId);
        Task<IEnumerable<CustomerGetDTO>?> GetCustomersByDelegatePermissions(int? delegateId);
        Task<DateReceiptsWeekGetDTO?> GetDateWeek();
        Task<IEnumerable<CustomersFollowGetDTO>?> Customers_Follow(int? delegateID, DateTime? paymentDate, string? showType);
    }
}
