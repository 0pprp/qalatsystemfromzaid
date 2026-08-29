using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface ICustomersRepository
    {
        Task<IEnumerable<CustomersGetDTO>?> Customers_GetAll(int? delegateID, string? textSearch, string? showType);
        Task<IEnumerable<CustomersGetDTO>?> Customers_GetByLastPaymentDate(DateTime? fromDate, DateTime? toDate);
        Task<CustomersGetDTO?> CustomersSalesCustomer_Create(CustomersSalesPostDTO customersSalesPostDTO);
        Task<CustomersGetDTO?> Customers_Update(int? customerID, CustomersPutDTO customersPutDTO);
        Task<CustomersInfoSimpleGetDTO?> Customers_InfoSimple(int? customerID);
        Task<CustomersGetDTO?> Customers_Move(int? customerID,int? delegateID,int? userUpdateID);
        Task<bool?> Customers_MoveLegal(int? customerID,int? userID);
        Task<IEnumerable<CustomersGetDTO>?> Customers_GetAllZero(DateTime? fromDate,DateTime? toDate);
        Task<IEnumerable<CustomerWeekPaymentsGetDTO>?> Customers_GetWeekReceipt(int? delegateID, string? showType);
        Task<IEnumerable<CustomerMonthPaymentsGetDTO>?> Customers_GetMonthReceipt(int? delegateID, string? showType);
        Task<IEnumerable<CustomersFollowGetDTO>?> Customers_Follow(int? delegateID, DateTime? paymentDate, string? showType);
    }
}
 