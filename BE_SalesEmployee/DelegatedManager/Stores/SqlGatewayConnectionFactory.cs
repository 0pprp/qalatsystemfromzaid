using System.Data;
using Microsoft.Data.SqlClient;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>
/// Opens gateway SQL connections from ConnectionStrings:SalesGateway.
/// When the setting is empty the app stays on the in-memory stores.
/// </summary>
public sealed class SqlGatewayConnectionFactory
{
    private readonly string _connectionString;

    public SqlGatewayConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString(ConfigurationKey) ?? "";
    }

    public const string ConfigurationKey = "SalesGateway";

    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString(ConfigurationKey));

    public async Task<IDbConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                $"ConnectionStrings:{ConfigurationKey} is not configured.");
        }

        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
