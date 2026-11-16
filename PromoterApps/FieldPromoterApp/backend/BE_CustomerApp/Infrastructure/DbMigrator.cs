using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_CustomerApp.Infrastructure;

/// <summary>
/// Applies embedded Infrastructure/Migrations/NNNN_*.sql scripts in order, once each, at startup.
/// Scripts may contain GO batch separators. Applied names are recorded in agents.__CustomerAppMigrations.
/// </summary>
public sealed partial class DbMigrator
{
    private readonly Db _db;
    private readonly ILogger<DbMigrator> _logger;

    public DbMigrator(Db db, ILogger<DbMigrator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await EnsureDatabaseExistsAsync(ct);
        await using var connection = await _db.OpenAsync(ct);
        // All app objects live in their own schema, so the app can share an existing (company) database
        // without touching its dbo tables.
        await connection.ExecuteAsync("IF SCHEMA_ID('agents') IS NULL EXEC('CREATE SCHEMA agents AUTHORIZATION dbo')");
        await connection.ExecuteAsync("""
            IF OBJECT_ID('agents.__CustomerAppMigrations') IS NULL
                CREATE TABLE agents.__CustomerAppMigrations (
                    Name VARCHAR(200) NOT NULL PRIMARY KEY,
                    AppliedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
            """);
        var applied = (await connection.QueryAsync<string>("SELECT Name FROM agents.__CustomerAppMigrations")).ToHashSet();

        var assembly = typeof(DbMigrator).Assembly;
        var scripts = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Migrations.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        foreach (var resource in scripts)
        {
            var name = resource[(resource.IndexOf(".Migrations.", StringComparison.Ordinal) + ".Migrations.".Length)..];
            if (applied.Contains(name)) continue;

            var sql = await ReadResourceAsync(assembly, resource);
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            foreach (var batch in BatchSeparator().Split(sql).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await connection.ExecuteAsync(new CommandDefinition(batch, transaction: tx, commandTimeout: 300, cancellationToken: ct));
            }
            await connection.ExecuteAsync("INSERT agents.__CustomerAppMigrations(Name) VALUES (@name)", new { name }, tx);
            await tx.CommitAsync(ct);
            _logger.LogInformation("Applied migration {Name}", name);
        }
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(_db.ConnectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database)) return;
        builder.InitialCatalog = "master";
        try
        {
            await using var master = new SqlConnection(builder.ConnectionString);
            await master.OpenAsync(ct);
            var exists = await master.ExecuteScalarAsync<int?>("SELECT 1 FROM sys.databases WHERE name = @database", new { database });
            if (exists is null)
            {
                await master.ExecuteAsync($"CREATE DATABASE [{database.Replace("]", "]]")}]");
                _logger.LogInformation("Created database {Database}", database);
            }
        }
        catch (SqlException ex)
        {
            // No rights on master in production is fine as long as the database already exists.
            _logger.LogWarning(ex, "Could not verify/create database {Database}; assuming it exists.", database);
        }
    }

    private static async Task<string> ReadResourceAsync(Assembly assembly, string resource)
    {
        await using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex BatchSeparator();
}
