using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface ICustomersPaymentsRepository
    {
        Task<IEnumerable<CustomersPaymentGetDTO>?> GetCustomersPaymentsCustomerDate(int? customerId, DateTime? dateCreate);
        Task<IEnumerable<CustomersPaymentGetDTO>?> GetCustomersPaymentsCustomer(int? customerId);
        Task<IEnumerable<CustomersPaymentGetDTO>?> GetCustomersPaymentsCustomerName(int? id, string? customerName);
    }
}
