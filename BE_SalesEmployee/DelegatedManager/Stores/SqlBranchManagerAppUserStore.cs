using System.Data;
using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class SqlBranchManagerAppUserStore : IBranchManagerAppUserStore
{
    private int _schemaReady;
    private readonly SqlGatewayConnectionFactory _factory;

    public SqlBranchManagerAppUserStore(SqlGatewayConnectionFactory factory) => _factory = factory;

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _schemaReady, 1, 0) == 1) return;
        try
        {
            using var connection = await _factory.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.BranchManagerAppUsers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BranchManagerAppUsers (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BranchManagerAppUsers PRIMARY KEY,
        UserName NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        DisplayName NVARCHAR(150) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_BmAppUsers_IsActive DEFAULT(1),
        CreatedAtUtc DATETIME2(3) NOT NULL,
        UpdatedAtUtc DATETIME2(3) NULL,
        CreatedByUserName NVARCHAR(100) NULL
    );
    CREATE UNIQUE INDEX UX_BranchManagerAppUsers_UserName ON dbo.BranchManagerAppUsers(UserName);
END
IF OBJECT_ID(N'dbo.BranchManagerAppUserCities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BranchManagerAppUserCities (
        UserId UNIQUEIDENTIFIER NOT NULL,
        CityValue NVARCHAR(100) NOT NULL,
        CityName NVARCHAR(150) NULL,
        CONSTRAINT PK_BranchManagerAppUserCities PRIMARY KEY (UserId, CityValue),
        CONSTRAINT FK_BmAppUserCities_User FOREIGN KEY (UserId)
            REFERENCES dbo.BranchManagerAppUsers(Id) ON DELETE CASCADE
    );
END
""", cancellationToken: ct));
        }
        catch
        {
            Interlocked.Exchange(ref _schemaReady, 0);
            throw;
        }
    }

    public async Task<IReadOnlyList<BranchManagerAppUser>> ListAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var users = (await connection.QueryAsync<BranchManagerAppUser>(new CommandDefinition("""
            SELECT Id, UserName, PasswordHash, DisplayName, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserName
            FROM dbo.BranchManagerAppUsers
            ORDER BY DisplayName, UserName;
            """, cancellationToken: ct))).ToList();
        await AttachCitiesAsync(connection, users, ct);
        return users;
    }

    public async Task<BranchManagerAppUser?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var user = await connection.QuerySingleOrDefaultAsync<BranchManagerAppUser>(new CommandDefinition("""
            SELECT Id, UserName, PasswordHash, DisplayName, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserName
            FROM dbo.BranchManagerAppUsers WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
        if (user is null) return null;
        await AttachCitiesAsync(connection, [user], ct);
        return user;
    }

    public async Task<BranchManagerAppUser?> FindByUserNameAsync(string userName, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var normalized = (userName ?? "").Trim();
        if (normalized.Length == 0) return null;
        using var connection = await _factory.OpenAsync(ct);
        var user = await connection.QuerySingleOrDefaultAsync<BranchManagerAppUser>(new CommandDefinition("""
            SELECT Id, UserName, PasswordHash, DisplayName, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserName
            FROM dbo.BranchManagerAppUsers
            WHERE LOWER(LTRIM(RTRIM(UserName))) = LOWER(@normalized);
            """, new { normalized }, cancellationToken: ct));
        if (user is null) return null;
        await AttachCitiesAsync(connection, [user], ct);
        return user;
    }

    public async Task<BranchManagerAppUser> UpsertAsync(BranchManagerAppUser user, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var row = user.Clone();
        if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
        if (row.CreatedAtUtc == default) row.CreatedAtUtc = DateTime.UtcNow;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UserName = row.UserName.Trim();
        row.DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? row.UserName : row.DisplayName.Trim();

        using var connection = await _factory.OpenAsync(ct);
        using var tx = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition("""
            MERGE dbo.BranchManagerAppUsers AS t
            USING (SELECT @Id AS Id) AS s ON t.Id = s.Id
            WHEN MATCHED THEN UPDATE SET
                UserName = @UserName,
                PasswordHash = @PasswordHash,
                DisplayName = @DisplayName,
                IsActive = @IsActive,
                UpdatedAtUtc = @UpdatedAtUtc
            WHEN NOT MATCHED THEN INSERT
                (Id, UserName, PasswordHash, DisplayName, IsActive, CreatedAtUtc, UpdatedAtUtc, CreatedByUserName)
            VALUES
                (@Id, @UserName, @PasswordHash, @DisplayName, @IsActive, @CreatedAtUtc, @UpdatedAtUtc, @CreatedByUserName);
            """, row, transaction: tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM dbo.BranchManagerAppUserCities WHERE UserId = @Id;
            """, new { row.Id }, transaction: tx, cancellationToken: ct));

        foreach (var city in row.Cities
                     .Where(c => !string.IsNullOrWhiteSpace(c.CityValue))
                     .GroupBy(c => c.CityValue.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First()))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO dbo.BranchManagerAppUserCities (UserId, CityValue, CityName)
                VALUES (@UserId, @CityValue, @CityName);
                """, new
            {
                UserId = row.Id,
                CityValue = city.CityValue.Trim(),
                CityName = string.IsNullOrWhiteSpace(city.CityName) ? null : city.CityName.Trim()
            }, transaction: tx, cancellationToken: ct));
        }

        tx.Commit();
        return (await GetAsync(row.Id, ct))!;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var n = await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM dbo.BranchManagerAppUsers WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
        return n > 0;
    }

    private static async Task AttachCitiesAsync(
        IDbConnection connection,
        List<BranchManagerAppUser> users,
        CancellationToken ct)
    {
        if (users.Count == 0) return;
        var ids = users.Select(u => u.Id).ToArray();
        var rows = await connection.QueryAsync<(Guid UserId, string CityValue, string? CityName)>(
            new CommandDefinition("""
                SELECT UserId, CityValue, CityName
                FROM dbo.BranchManagerAppUserCities
                WHERE UserId IN @ids
                ORDER BY CityName, CityValue;
                """, new { ids }, cancellationToken: ct));
        var map = rows.GroupBy(r => r.UserId).ToDictionary(
            g => g.Key,
            g => g.Select(r => new BranchManagerAppUserCity
            {
                CityValue = r.CityValue,
                CityName = r.CityName
            }).ToList());
        foreach (var user in users)
        {
            user.Cities = map.TryGetValue(user.Id, out var cities) ? cities : [];
        }
    }
}
