using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface ICustomersPaymentsRepository
    {
        Task<CustomersPaymentsGetDTO?> CustomersPayments_Create(CustomersPaymentsPostDTO customersPaymentsPostDTO);
        Task<bool?> CustomersPayments_Delete(int? customerPaymentID, int? userID);
        Task<IEnumerable<CustomersPaymentsGetDTO>?> CustomersPayments_GetAll(DateTime? fromDate, DateTime? toDate, int? delegateID, string? textSearch);
        Task<IEnumerable<CustomersPaymentsGetDTO>?> CustomersPayments_GetByCustomerID(int? customerID);
        Task<bool?> CustomersPayments_ChangePaymentDate(string? paymentlist, DateTime? newDate, int? userID);
        Task<bool?> CustomersPayments_DeleteSelect(string? paymentlist, int? userID);
    }
}
