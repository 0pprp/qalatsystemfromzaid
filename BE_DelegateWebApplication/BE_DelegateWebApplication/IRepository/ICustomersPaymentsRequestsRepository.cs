using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface ICustomersPaymentsRequestsRepository
    {
        Task<bool?> PostSelectPaymentCustomerTemporary(CustomersPaymentsRequestsPostDTO? customersPaymentsRequestsPostDTO);
        Task<IEnumerable<CustomersPaymentsRequestsGetDTO>?> GetCustomersPaymentsRequestsByDelegateID(int? delegateId);
        Task<bool?> PostSelectPaymentCustomerTemporaryMulti(List<CustomersPaymentsRequestsPostDTO>? customersPaymentsRequestsPostDTO);
    }
}
