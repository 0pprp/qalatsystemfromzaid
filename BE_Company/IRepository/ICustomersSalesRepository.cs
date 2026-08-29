using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface ICustomersSalesRepository
    {
        Task<CustomersSalesGetDTO?> CustomersSales_Create(CustomersSalesPostDTO storesPostDTO);
        Task<bool?> CustomersSales_Delete(int? customerSaleID,int? userDeleteID);
        Task<IEnumerable<CustomersSalesGetDTO>?> CustomersSales_GetAll(DateTime? fromDate,DateTime? toDate,int? delegateID,string? customerName,string? itemName,string? saleName);
        Task<CustomersGetDTO?> Customers_GetByCustomerID(int? customerID);
        Task<CustomersSalesGetDTO?> CustomersSales_GetByID(int? customerSaleID);
        Task<bool?> CustomersSales_UpdateDiscount(int? customerSaleID, DiscountDTO discountDTO);
        Task<IEnumerable<CustomersSalesGetDTO>?> CustomersSales_GetByCustomerIDNew(int? customerID, DateTime? fromDate, DateTime? toDate);
    }
}
