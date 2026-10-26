using System.Collections.Concurrent;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryMobileTutorialVideoStore : IMobileTutorialVideoStore
{
    private readonly ConcurrentDictionary<int, MobileAppTutorialVideo> _rows = new();
    private int _nextId = 1;

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<MobileAppTutorialVideo>> ListAllAsync(CancellationToken ct = default)
    {
        var list = _rows.Values
            .OrderBy(r => r.AppKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .Select(r => r.Clone())
            .ToList();
        return Task.FromResult<IReadOnlyList<MobileAppTutorialVideo>>(list);
    }

    public Task<IReadOnlyList<MobileAppTutorialVideo>> ListByAppAsync(
        string appKey,
        bool enabledOnly,
        CancellationToken ct = default)
    {
        var key = (appKey ?? string.Empty).Trim();
        var list = _rows.Values
            .Where(r => string.Equals(r.AppKey, key, StringComparison.OrdinalIgnoreCase))
            .Where(r => !enabledOnly || r.IsEnabled)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .Select(r => r.Clone())
            .ToList();
        return Task.FromResult<IReadOnlyList<MobileAppTutorialVideo>>(list);
    }

    public Task<MobileAppTutorialVideo?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return Task.FromResult(_rows.TryGetValue(id, out var row) ? row.Clone() : null);
    }

    public Task<MobileAppTutorialVideo> InsertAsync(MobileAppTutorialVideo row, CancellationToken ct = default)
    {
        var copy = row.Clone();
        copy.Id = Interlocked.Increment(ref _nextId);
        var now = DateTime.UtcNow;
        if (copy.CreatedAtUtc == default) copy.CreatedAtUtc = now;
        if (copy.UpdatedAtUtc == default) copy.UpdatedAtUtc = now;
        _rows[copy.Id] = copy.Clone();
        return Task.FromResult(copy);
    }

    public Task UpdateAsync(MobileAppTutorialVideo row, CancellationToken ct = default)
    {
        if (!_rows.ContainsKey(row.Id))
        {
            throw new KeyNotFoundException($"Tutorial video {row.Id} not found");
        }

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
