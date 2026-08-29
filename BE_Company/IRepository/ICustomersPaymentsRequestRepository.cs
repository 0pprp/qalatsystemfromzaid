using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface ICustomersPaymentsRequestRepository
    {
        Task<IEnumerable<CustomersPaymentsRequestGetDTO>?> CustomersPaymentsRequest_GetAll(string? customerName, int? delegateID);
        Task<bool?> CustomersPaymentsRequest_Delete(int? customersPaymentsRequestID, int? userDeleteID);
        Task<bool?> CustomersPaymentsRequest_DeleteAll(int? userDeleteID);
        Task<bool?> CustomersPaymentsRequest_DeleteSame();
        Task<bool?> CustomersPaymentsRequest_Approve(int? customersPaymentsRequestID, int? userCreateID);
        Task<bool?> CustomersPaymentsRequest_ApproveAll(int? userCreateID);
        Task<bool?> CustomersPaymentsRequest_ChangeDate(DateTime? paymentDate);
        Task<bool?> CustomersPaymentsRequest_DeleteSelect(string? paymentlist, int? userID);
    }
}
