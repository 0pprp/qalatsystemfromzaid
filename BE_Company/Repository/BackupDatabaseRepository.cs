using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Repository
{
    public class BackupDatabaseRepository : IBackupDatabaseRepository
    {
        private readonly string _connectionString;

        public BackupDatabaseRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<string?> BackupDatabase()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var parameters = new DynamicParameters();
                string? databaseName = GetDatabaseName();
                parameters.Add("@DatabaseName", databaseName);
                parameters.Add("@BackupFilePath", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);
                await connection.ExecuteAsync("BackupDatabaseWithPath", parameters, commandType: CommandType.StoredProcedure);
                string backupFilePath = parameters.Get<string>("@BackupFilePath");
                return backupFilePath;
            }
        }

        private string? GetDatabaseName()
        {
            var builder = new SqlConnectionStringBuilder(_connectionString);    
            return builder.InitialCatalog;
        }
    }
}
