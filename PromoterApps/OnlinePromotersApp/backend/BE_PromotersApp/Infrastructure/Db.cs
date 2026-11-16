using System.Data;
using Microsoft.Data.SqlClient;

namespace BE_PromotersApp.Infrastructure;

/// <summary>Opens connections to the promoters app database (ConnectionStrings:PromotersApp).</summary>
public sealed class Db
{
    private readonly string _connectionString;

    public Db(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PromotersApp")
            ?? throw new InvalidOperationException("ConnectionStrings:PromotersApp is not configured.");
    }

    public string ConnectionString => _connectionString;

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>Runs <paramref name="work"/> inside a transaction; commits when it returns, rolls back on exception.</summary>
    public async Task<T> InTransactionAsync<T>(
        Func<SqlConnection, SqlTransaction, Task<T>> work,
        IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(isolation, ct);
        try
        {
            var result = await work(connection, tx);
            await tx.CommitAsync(ct);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
