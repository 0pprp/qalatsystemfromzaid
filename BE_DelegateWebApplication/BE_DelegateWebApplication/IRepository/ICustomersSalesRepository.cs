using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface ICustomersSalesRepository
    {
        Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomerDate(int? customerId, DateTime? dateCreate);
        Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomer(int? customerId);
        Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomerLast(int? delegateId);
        Task<IEnumerable<CustomersSalesGetDTO>?> GetCustomersSalesCustomerName(int? id, string? customerName);
    }
}
