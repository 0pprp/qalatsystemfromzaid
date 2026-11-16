using BE_SalesEmployee.DelegatedManager.Stores;
using Dapper;

namespace BE_SalesEmployee.Services;

public sealed class SqlOversightAccountStore : IOversightAccountStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;
    private readonly SqlGatewayConnectionFactory _factory;

    public SqlOversightAccountStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<OversightCommitteeRecord>> ListCommitteesAsync(CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var committees = (await connection.QueryAsync<CommitteeRow>(new CommandDefinition(
            "SELECT Id, Name, CreatedBy FROM dbo.OversightCommittees ORDER BY Id",
            cancellationToken: ct))).ToList();
        var branches = (await connection.QueryAsync<BranchRow>(new CommandDefinition(
            "SELECT CommitteeId, CityValue FROM dbo.OversightCommitteeBranches",
            cancellationToken: ct))).ToList();
        return committees.Select(c => new OversightCommitteeRecord
        {
            Id = c.Id,
            Name = c.Name,
            CreatedBy = c.CreatedBy,
            CityValues = branches.Where(b => b.CommitteeId == c.Id).Select(b => b.CityValue).ToList(),
        }).ToList();
    }

    public async Task<OversightCommitteeRecord> CreateCommitteeAsync(
        string name, IReadOnlyList<string> cityValues, string? createdBy, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.OversightCommittees (Name, CreatedBy)
OUTPUT INSERTED.Id
VALUES (@Name, @CreatedBy);
""", new { Name = name.Trim(), CreatedBy = createdBy }, cancellationToken: ct));
        await ReplaceBranchesAsync(connection, id, cityValues, ct);
        var created = (await ListCommitteesAsync(ct)).First(c => c.Id == id);
        return created;
    }

    public async Task<bool> SetCommitteeBranchesAsync(int committeeId, IReadOnlyList<string> cityValues, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.OversightCommittees WHERE Id = @Id",
            new { Id = committeeId }, cancellationToken: ct));
        if (exists == 0)
            return false;
        await ReplaceBranchesAsync(connection, committeeId, cityValues, ct);
        return true;
    }

    public async Task<IReadOnlyList<OversightAccountRecord>> ListAccountsAsync(CancellationToken ct)
    {
        var rows = await LoadAccountsAsync(ct, null);
        return rows.Select(HidePassword).ToList();
    }

    public async Task<OversightAccountRecord?> FindByUserNameAsync(string userName, CancellationToken ct)
    {
        var rows = await LoadAccountsAsync(ct, InMemoryOversightAccountStore.Key(userName));
        return rows.FirstOrDefault();
    }

    public async Task<bool> UserNameExistsAsync(string userName, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.OversightAccounts WHERE UserNameKey = @Key",
            new { Key = InMemoryOversightAccountStore.Key(userName) }, cancellationToken: ct));
        return count > 0;
    }

    public async Task<OversightAccountRecord> CreateAccountAsync(
        string userName,
        string displayName,
        string password,
        string role,
        int? committeeId,
        string? createdBy,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.OversightAccounts
    (UserName, UserNameKey, DisplayName, Password, Role, CommitteeId, CreatedBy)
VALUES
    (@UserName, @UserNameKey, @DisplayName, @Password, @Role, @CommitteeId, @CreatedBy);
""", new
        {
            UserName = userName.Trim(),
            UserNameKey = InMemoryOversightAccountStore.Key(userName),
            DisplayName = displayName.Trim(),
            Password = password,
            Role = role,
            CommitteeId = committeeId,
            CreatedBy = createdBy,
        }, cancellationToken: ct));
        var created = await FindByUserNameAsync(userName, ct);
        return HidePassword(created!);
    }

    public async Task<bool> DeleteAccountAsync(int id, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var n = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.OversightAccounts WHERE Id = @Id",
            new { Id = id }, cancellationToken: ct));
        return n > 0;
    }

    public async Task<bool> DeleteCommitteeAsync(int committeeId, CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var n = await connection.ExecuteAsync(new CommandDefinition("""
IF NOT EXISTS (SELECT 1 FROM dbo.OversightAccounts WHERE CommitteeId = @Id)
BEGIN
    DELETE FROM dbo.OversightCommitteeBranches WHERE CommitteeId = @Id;
    DELETE FROM dbo.OversightCommittees WHERE Id = @Id;
END
""", new { Id = committeeId }, cancellationToken: ct));
        return n > 0;
    }

    private async Task<List<OversightAccountRecord>> LoadAccountsAsync(CancellationToken ct, string? userNameKey)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = (await connection.QueryAsync<AccountRow>(new CommandDefinition("""
SELECT a.Id, a.UserName, a.DisplayName, a.Password, a.Role, a.CommitteeId, c.Name AS CommitteeName
FROM dbo.OversightAccounts a
LEFT JOIN dbo.OversightCommittees c ON c.Id = a.CommitteeId
WHERE (@Key IS NULL OR a.UserNameKey = @Key)
ORDER BY a.Id
""", new { Key = userNameKey }, cancellationToken: ct))).ToList();
        if (rows.Count == 0)
            return [];
        var branches = (await connection.QueryAsync<BranchRow>(new CommandDefinition(
            "SELECT CommitteeId, CityValue FROM dbo.OversightCommitteeBranches",
            cancellationToken: ct))).ToList();
        return rows.Select(a => new OversightAccountRecord
        {
            Id = a.Id,
            UserName = a.UserName,
            DisplayName = a.DisplayName,
            Password = a.Password,
            Role = a.Role,
            CommitteeId = a.CommitteeId,
            CommitteeName = a.CommitteeName,
            CityValues = a.CommitteeId is int id
                ? branches.Where(b => b.CommitteeId == id).Select(b => b.CityValue).ToList()
                : Array.Empty<string>(),
        }).ToList();
    }

    private static async Task ReplaceBranchesAsync(
        System.Data.IDbConnection connection, int committeeId, IReadOnlyList<string> cityValues, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.OversightCommitteeBranches WHERE CommitteeId = @Id",
            new { Id = committeeId }, cancellationToken: ct));
        foreach (var city in cityValues.Select(v => (v ?? "").Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.OversightCommitteeBranches (CommitteeId, CityValue) VALUES (@Id, @City)
""", new { Id = committeeId, City = city }, cancellationToken: ct));
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (_schemaReady)
            return;
        lock (SchemaGate)
        {
            if (_schemaReady)
                return;
        }

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.OversightCommittees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OversightCommittees (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OversightCommittees PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        CreatedBy NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_OversightCommittees_At DEFAULT SYSUTCDATETIME()
    );
END
IF OBJECT_ID(N'dbo.OversightCommitteeBranches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OversightCommitteeBranches (
        CommitteeId INT NOT NULL,
        CityValue NVARCHAR(100) NOT NULL,
        CONSTRAINT PK_OversightCommitteeBranches PRIMARY KEY (CommitteeId, CityValue)
    );
END
IF OBJECT_ID(N'dbo.OversightAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OversightAccounts (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OversightAccounts PRIMARY KEY,
        UserName NVARCHAR(200) NOT NULL,
        UserNameKey NVARCHAR(200) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        Password NVARCHAR(500) NOT NULL,
        Role NVARCHAR(80) NOT NULL,
        CommitteeId INT NULL,
        CreatedBy NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_OversightAccounts_At DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_OversightAccounts_UserNameKey UNIQUE (UserNameKey)
    );
END
""", cancellationToken: ct));
        lock (SchemaGate)
            _schemaReady = true;
    }

    private static OversightAccountRecord HidePassword(OversightAccountRecord row) =>
        new()
        {
            Id = row.Id,
            UserName = row.UserName,
            DisplayName = row.DisplayName,
            Password = "",
            Role = row.Role,
            CommitteeId = row.CommitteeId,
            CommitteeName = row.CommitteeName,
            CityValues = row.CityValues,
        };

    private sealed class CommitteeRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string? CreatedBy { get; init; }
    }

    private sealed class BranchRow
    {
        public int CommitteeId { get; init; }
        public string CityValue { get; init; } = "";
    }

    private sealed class AccountRow
    {
        public int Id { get; init; }
        public string UserName { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Password { get; init; } = "";
        public string Role { get; init; } = "";
        public int? CommitteeId { get; init; }
        public string? CommitteeName { get; init; }
    }
}
