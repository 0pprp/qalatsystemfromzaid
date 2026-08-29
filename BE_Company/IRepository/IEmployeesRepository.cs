using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IEmployeesRepository
    {
        Task<EmployeesGetDTO?> Employees_Create(EmployeesPostDTO itemsPostDTO);
        Task<EmployeesGetDTO?> Employees_Update(int? employeeID, EmployeesPutDTO itemsPutDTO);
        Task<bool?> Employees_Delete(int? employeeID, int? userDeleteID);
        Task<IEnumerable<EmployeesGetDTO>?> Employees_GetAll();
    }
}
