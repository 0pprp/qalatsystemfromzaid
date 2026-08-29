using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class SuppliersRepository : ISuppliersRepository
    {
        private readonly string _connectionString;

        public SuppliersRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<SuppliersGetDTO?> Suppliers_Create(SuppliersPostDTO suppliersPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<SuppliersGetDTO>("Suppliers_Create",
                new
                {
                    SupplierName = suppliersPostDTO.SupplierName,
                    Address = suppliersPostDTO.Address,
                    PhoneNumber = suppliersPostDTO.PhoneNumber,
                    Notes = suppliersPostDTO.Notes,
                    UserCreateID = suppliersPostDTO.UserCreateID
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<SuppliersGetDTO?> Suppliers_Update(int? supplierID, SuppliersPutDTO suppliersPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<SuppliersGetDTO>("Suppliers_Update",
                new
                {
                    SupplierID = supplierID,
                    SupplierName = suppliersPutDTO.SupplierName,
                    Address = suppliersPutDTO.Address,
                    PhoneNumber = suppliersPutDTO.PhoneNumber,
                    Notes = suppliersPutDTO.Notes,
                    UserUpdateID = suppliersPutDTO.UserUpdateID
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool?> Suppliers_Delete(int? supplierID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Suppliers_Delete",
                new
                {
                    SupplierID = supplierID,
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<IEnumerable<SuppliersGetDTO>?> Suppliers_GetAll(string? textSearch)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryAsync<SuppliersGetDTO>("Suppliers_GetAll",
                new
                {
                    TextSearch = textSearch,
                },
                commandType: CommandType.StoredProcedure);
            }
        }
    }
}
