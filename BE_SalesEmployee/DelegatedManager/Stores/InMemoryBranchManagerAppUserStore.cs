using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryBranchManagerAppUserStore : IBranchManagerAppUserStore
{
    private readonly object _gate = new();
    private readonly List<BranchManagerAppUser> _rows = [];

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<BranchManagerAppUser>> ListAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<BranchManagerAppUser>>(
                _rows.OrderBy(r => r.DisplayName).ThenBy(r => r.UserName).Select(r => r.Clone()).ToList());
        }
    }

    public Task<BranchManagerAppUser?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            return Task.FromResult(row?.Clone());
        }
    }

    public Task<BranchManagerAppUser?> FindByUserNameAsync(string userName, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.FirstOrDefault(r =>
                string.Equals(r.UserName.Trim(), userName.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(row?.Clone());
        }
    }

    public Task<BranchManagerAppUser> UpsertAsync(BranchManagerAppUser user, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = user.Clone();
            if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
            if (row.CreatedAtUtc == default) row.CreatedAtUtc = DateTime.UtcNow;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.Cities = row.Cities
                .Where(c => !string.IsNullOrWhiteSpace(c.CityValue))
                .GroupBy(c => c.CityValue.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First().Clone())
                .ToList();

            var idx = _rows.FindIndex(r => r.Id == row.Id);
            if (idx >= 0) _rows[idx] = row;
            else _rows.Add(row);
            return Task.FromResult(row.Clone());
        }
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var n = _rows.RemoveAll(r => r.Id == id);
            return Task.FromResult(n > 0);
        }
    }
}
