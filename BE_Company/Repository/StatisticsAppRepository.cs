using BE_Company.DTO;
using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Repository
{
    public class StatisticsAppRepository : IStatisticsAppRepository
    {
        private readonly string _connectionString;

        public StatisticsAppRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<StatisticsAppGetDTO> StatisticsApp_GetAll()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<StatisticsAppGetDTO>("StatisticsApp_GetAll", commandType: CommandType.StoredProcedure);
                return result;
            }
        }
    }
}
