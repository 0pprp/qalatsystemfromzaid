using System.Text.Json;

namespace BE_SalesEmployee.Services;

/// <summary>
/// Persists oversight committees and accounts when the gateway has no SQL database.
/// </summary>
public sealed class FileOversightAccountStore : IOversightAccountStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private Snapshot _snapshot;

    public FileOversightAccountStore(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration["OversightAccounts:File"];
        _path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "oversight-accounts.json")
            : configured;
        _snapshot = Load();
    }

    public Task<IReadOnlyList<OversightCommitteeRecord>> ListCommitteesAsync(CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<OversightCommitteeRecord>>(_snapshot.Committees.Select(ToCommittee).ToList());
    }

    public Task<OversightCommitteeRecord> CreateCommitteeAsync(
        string name, IReadOnlyList<string> cityValues, string? createdBy, CancellationToken ct)
    {
        lock (_gate)
        {
            var row = new CommitteeDto
            {
                Id = NextId(_snapshot.Committees.Select(c => c.Id)),
                Name = name.Trim(),
                CreatedBy = createdBy,
                CityValues = Clean(cityValues),
            };
            _snapshot.Committees.Add(row);
            Save();
            return Task.FromResult(ToCommittee(row));
        }
    }

    public Task<bool> SetCommitteeBranchesAsync(int committeeId, IReadOnlyList<string> cityValues, CancellationToken ct)
    {
        lock (_gate)
        {
            var row = _snapshot.Committees.FirstOrDefault(c => c.Id == committeeId);
            if (row is null)
                return Task.FromResult(false);
            row.CityValues = Clean(cityValues);
            Save();
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<OversightAccountRecord>> ListAccountsAsync(CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<OversightAccountRecord>>(_snapshot.Accounts.Select(a => ToAccount(a, hidePassword: true)).ToList());
    }

    public Task<OversightAccountRecord?> FindByUserNameAsync(string userName, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = InMemoryOversightAccountStore.Key(userName);
            var row = _snapshot.Accounts.FirstOrDefault(a => InMemoryOversightAccountStore.Key(a.UserName) == key);
            return Task.FromResult(row is null ? null : ToAccount(row, hidePassword: false));
        }
    }

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = InMemoryOversightAccountStore.Key(userName);
            return Task.FromResult(_snapshot.Accounts.Any(a => InMemoryOversightAccountStore.Key(a.UserName) == key));
        }
    }

    public Task<OversightAccountRecord> CreateAccountAsync(
        string userName, string displayName, string password, string role, int? committeeId, string? createdBy, CancellationToken ct)
    {
        lock (_gate)
        {
            var row = new AccountDto
            {
                Id = NextId(_snapshot.Accounts.Select(a => a.Id)),
                UserName = userName.Trim(),
                DisplayName = displayName.Trim(),
                Password = password,
                Role = role,
                CommitteeId = committeeId,
                CreatedBy = createdBy,
            };
            _snapshot.Accounts.Add(row);
            Save();
            return Task.FromResult(ToAccount(row, hidePassword: true));
        }
    }

    public Task<bool> DeleteAccountAsync(int id, CancellationToken ct)
    {
        lock (_gate)
        {
            var removed = _snapshot.Accounts.RemoveAll(a => a.Id == id) > 0;
            if (removed)
                Save();
            return Task.FromResult(removed);
        }
    }

    public Task<bool> DeleteCommitteeAsync(int committeeId, CancellationToken ct)
    {
        lock (_gate)
        {
            var removed = _snapshot.Committees.RemoveAll(c => c.Id == committeeId) > 0;
            if (removed)
                Save();
            return Task.FromResult(removed);
        }
    }

    private OversightAccountRecord ToAccount(AccountDto row, bool hidePassword)
    {
        var committee = row.CommitteeId is int id
            ? _snapshot.Committees.FirstOrDefault(c => c.Id == id)
            : null;
        return new OversightAccountRecord
        {
            Id = row.Id,
            UserName = row.UserName,
            DisplayName = row.DisplayName,
            Password = hidePassword ? "" : row.Password,
            Role = row.Role,
            CommitteeId = row.CommitteeId,
            CommitteeName = committee?.Name,
            CityValues = committee?.CityValues ?? new List<string>(),
        };
    }

    private static OversightCommitteeRecord ToCommittee(CommitteeDto row) =>
        new()
        {
            Id = row.Id,
            Name = row.Name,
            CreatedBy = row.CreatedBy,
            CityValues = row.CityValues,
        };

    private Snapshot Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new Snapshot();
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<Snapshot>(json) ?? new Snapshot();
        }
        catch
        {
            return new Snapshot();
        }
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_snapshot, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    private static int NextId(IEnumerable<int> ids)
    {
        var max = 0;
        foreach (var id in ids)
            if (id > max)
                max = id;
        return max + 1;
    }

    private static List<string> Clean(IReadOnlyList<string> values) =>
        values.Select(v => (v ?? "").Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private sealed class Snapshot
    {
        public List<CommitteeDto> Committees { get; set; } = [];
        public List<AccountDto> Accounts { get; set; } = [];
    }

    private sealed class CommitteeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? CreatedBy { get; set; }
        public List<string> CityValues { get; set; } = [];
    }

    private sealed class AccountDto
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "";
        public int? CommitteeId { get; set; }
        public string? CreatedBy { get; set; }
    }
}
