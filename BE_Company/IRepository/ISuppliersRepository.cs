using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface ISuppliersRepository
    {
        Task<SuppliersGetDTO?> Suppliers_Create(SuppliersPostDTO suppliersPostDTO);
        Task<SuppliersGetDTO?> Suppliers_Update(int? supplierID, SuppliersPutDTO suppliersPutDTO);
        Task<bool?> Suppliers_Delete(int? supplierID, int? userDeleteID);
        Task<IEnumerable<SuppliersGetDTO>?> Suppliers_GetAll(string? textSearch);
    }
}
