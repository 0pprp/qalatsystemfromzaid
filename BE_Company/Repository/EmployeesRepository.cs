using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class EmployeesRepository : IEmployeesRepository
    {
        private readonly string _connectionString;

        public EmployeesRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<EmployeesGetDTO?> Employees_Create(EmployeesPostDTO employeesPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<EmployeesGetDTO>("Employees_Create",
                new
                {
                    UserCreateID = employeesPostDTO.UserCreateID,
                    EmployeeName = employeesPostDTO.EmployeeName,
                    Address = employeesPostDTO.Address,
                    PhoneNumber = employeesPostDTO.PhoneNumber,
                    Notes = employeesPostDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<EmployeesGetDTO?> Employees_Update(int? employeeID, EmployeesPutDTO employeesPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<EmployeesGetDTO>("Employees_Update",
                new
                {
                    EmployeeID = employeeID,
                    UserUpdateID = employeesPutDTO.UserUpdateID,
                    EmployeeName = employeesPutDTO.EmployeeName,
                    Address = employeesPutDTO.Address,
                    PhoneNumber = employeesPutDTO.PhoneNumber,
                    Notes = employeesPutDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> Employees_Delete(int? employeeID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<EmployeesGetDTO>("Employees_Delete",
                new { EmployeeID = employeeID, UserDeleteID = userDeleteID },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<IEnumerable<EmployeesGetDTO>?> Employees_GetAll()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<EmployeesGetDTO>("Employees_GetAll",
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }
    }
}
