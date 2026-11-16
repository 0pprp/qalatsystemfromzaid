namespace BE_SalesEmployee.Services;

public sealed class InMemoryOversightAccountStore : IOversightAccountStore
{
    private readonly object _gate = new();
    private readonly List<OversightCommitteeRecord> _committees = [];
    private readonly List<OversightAccountRecord> _accounts = [];
    private int _nextCommittee = 1;
    private int _nextAccount = 1;

    public Task<IReadOnlyList<OversightCommitteeRecord>> ListCommitteesAsync(CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<OversightCommitteeRecord>>(_committees.ToList());
    }

    public Task<OversightCommitteeRecord> CreateCommitteeAsync(
        string name, IReadOnlyList<string> cityValues, string? createdBy, CancellationToken ct)
    {
        lock (_gate)
        {
            var row = new OversightCommitteeRecord
            {
                Id = _nextCommittee++,
                Name = name.Trim(),
                CreatedBy = createdBy,
                CityValues = CleanCities(cityValues),
            };
            _committees.Add(row);
            return Task.FromResult(row);
        }
    }

    public Task<bool> SetCommitteeBranchesAsync(int committeeId, IReadOnlyList<string> cityValues, CancellationToken ct)
    {
        lock (_gate)
        {
            var index = _committees.FindIndex(c => c.Id == committeeId);
            if (index < 0)
                return Task.FromResult(false);
            var current = _committees[index];
            var updated = new OversightCommitteeRecord
            {
                Id = current.Id,
                Name = current.Name,
                CreatedBy = current.CreatedBy,
                CityValues = CleanCities(cityValues),
            };
            _committees[index] = updated;
            for (var i = 0; i < _accounts.Count; i++)
            {
                if (_accounts[i].CommitteeId != committeeId)
                    continue;
                var account = _accounts[i];
                _accounts[i] = CopyAccount(account, updated.CityValues, updated.Name);
            }
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<OversightAccountRecord>> ListAccountsAsync(CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<OversightAccountRecord>>(_accounts.Select(WithoutPassword).ToList());
    }

    public Task<OversightAccountRecord?> FindByUserNameAsync(string userName, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = Key(userName);
            var row = _accounts.FirstOrDefault(a => Key(a.UserName) == key);
            return Task.FromResult(row);
        }
    }

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = Key(userName);
            return Task.FromResult(_accounts.Any(a => Key(a.UserName) == key));
        }
    }

    public Task<OversightAccountRecord> CreateAccountAsync(
        string userName,
        string displayName,
        string password,
        string role,
        int? committeeId,
        string? createdBy,
        CancellationToken ct)
    {
        lock (_gate)
        {
            IReadOnlyList<string> cities = Array.Empty<string>();
            string? committeeName = null;
            if (committeeId is int id)
            {
                var committee = _committees.FirstOrDefault(c => c.Id == id);
                cities = committee?.CityValues ?? Array.Empty<string>();
                committeeName = committee?.Name;
            }

            var row = new OversightAccountRecord
            {
                Id = _nextAccount++,
                UserName = userName.Trim(),
                DisplayName = displayName.Trim(),
                Password = password,
                Role = role,
                CommitteeId = committeeId,
                CommitteeName = committeeName,
                CityValues = cities,
            };
            _accounts.Add(row);
            return Task.FromResult(WithoutPassword(row));
        }
    }

    public Task<bool> DeleteAccountAsync(int id, CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult(_accounts.RemoveAll(a => a.Id == id) > 0);
    }

    public Task<bool> DeleteCommitteeAsync(int committeeId, CancellationToken ct)
    {
        lock (_gate)
            return Task.FromResult(_committees.RemoveAll(c => c.Id == committeeId) > 0);
    }

    private static OversightAccountRecord CopyAccount(
        OversightAccountRecord account, IReadOnlyList<string> cities, string? committeeName) =>
        new()
        {
            Id = account.Id,
            UserName = account.UserName,
            DisplayName = account.DisplayName,
            Password = account.Password,
            Role = account.Role,
            CommitteeId = account.CommitteeId,
            CommitteeName = committeeName,
            CityValues = cities,
        };

    private static OversightAccountRecord WithoutPassword(OversightAccountRecord account) =>
        new()
        {
            Id = account.Id,
            UserName = account.UserName,
            DisplayName = account.DisplayName,
            Password = "",
            Role = account.Role,
            CommitteeId = account.CommitteeId,
            CommitteeName = account.CommitteeName,
            CityValues = account.CityValues,
        };

    internal static string Key(string userName) =>
        DirectorAccountService.NormalizeUserName(userName).ToLowerInvariant();

    private static IReadOnlyList<string> CleanCities(IReadOnlyList<string> cityValues) =>
        cityValues
            .Select(v => (v ?? "").Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
