using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_DelegateWebApplication.Repository
{
    public class DelegateRepository : IDelegateRepository
    {
        private readonly string _connectionString;

        public DelegateRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<DelegateGetDTO?> GetDelegateLogin(string? asyncID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<DelegateGetDTO>("GetDelegateLogin",
                new
                {
                    AsyncID = asyncID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<DelegateGetDTO?> GetDelegateCheckLogout(string? asyncID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<DelegateGetDTO>("GetDelegateCheckLogout",
                new
                {
                    AsyncID = asyncID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<DelegateInfoGetDTO?> GetDelegateTitle(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<DelegateInfoGetDTO>("GetDelegateTitle",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<SelectDelegateGetDTO>?> GetDelegateSelect(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<SelectDelegateGetDTO>("GetDelegateSelect",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 600);
                return result;
            }
        }

        public async Task<bool> IsFollowerListLinked(int fatherId, int childId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<LinkedFlagDTO>(
                    "Followers_IsLinked",
                    new { FatherID = fatherId, ChildID = childId },
                    commandType: CommandType.StoredProcedure);
                return result != null && result.IsLinked == 1;
            }
        }
    }
}
