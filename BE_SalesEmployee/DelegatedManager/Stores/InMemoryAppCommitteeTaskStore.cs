using System.Collections.Concurrent;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryAppCommitteeTaskStore : IAppCommitteeTaskStore
{
    private readonly ConcurrentDictionary<int, AppCommitteeTask> _rows = new();
    private int _nextId = 1;

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AppCommitteeTask>> ListAllAsync(CancellationToken ct = default)
    {
        var list = _rows.Values
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .Select(r => r.Clone())
            .ToList();
        return Task.FromResult<IReadOnlyList<AppCommitteeTask>>(list);
    }

    public Task<IReadOnlyList<AppCommitteeTask>> ListEnabledAsync(CancellationToken ct = default)
    {
        var list = _rows.Values
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .Select(r => r.Clone())
            .ToList();
        return Task.FromResult<IReadOnlyList<AppCommitteeTask>>(list);
    }

    public Task<AppCommitteeTask?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var row) ? row.Clone() : null);

    public Task<AppCommitteeTask> InsertAsync(AppCommitteeTask row, CancellationToken ct = default)
    {
        var copy = row.Clone();
        copy.Id = Interlocked.Increment(ref _nextId);
        var now = DateTime.UtcNow;
        if (copy.CreatedAtUtc == default) copy.CreatedAtUtc = now;
        if (copy.UpdatedAtUtc == default) copy.UpdatedAtUtc = now;
        _rows[copy.Id] = copy.Clone();
        return Task.FromResult(copy);
    }

    public Task UpdateAsync(AppCommitteeTask row, CancellationToken ct = default)
    {
        if (!_rows.ContainsKey(row.Id))
            throw new KeyNotFoundException($"Committee task {row.Id} not found");
        var copy = row.Clone();
        copy.UpdatedAtUtc = DateTime.UtcNow;
        _rows[copy.Id] = copy;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(int id, CancellationToken ct = default)
    {
        _rows.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
