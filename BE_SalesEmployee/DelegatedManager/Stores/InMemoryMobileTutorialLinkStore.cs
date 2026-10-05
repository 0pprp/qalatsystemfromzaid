using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryMobileTutorialLinkStore : IMobileTutorialLinkStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, MobileAppTutorialLink> _rows =
        new(StringComparer.OrdinalIgnoreCase);

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<MobileAppTutorialLink>> ListAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<MobileAppTutorialLink>>(
                _rows.Values.OrderBy(r => r.TitleAr, StringComparer.OrdinalIgnoreCase).Select(r => r.Clone()).ToList());
        }
    }

    public Task<MobileAppTutorialLink?> GetAsync(string appKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(appKey))
        {
            return Task.FromResult<MobileAppTutorialLink?>(null);
        }

        lock (_gate)
        {
            return Task.FromResult(_rows.TryGetValue(appKey.Trim(), out var row) ? row.Clone() : null);
        }
    }

    public Task UpsertAsync(MobileAppTutorialLink row, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(row.AppKey))
        {
            throw new ArgumentException("AppKey is required", nameof(row));
        }

        var clone = row.Clone();
        clone.AppKey = clone.AppKey.Trim();
        clone.UpdatedAtUtc = clone.UpdatedAtUtc == default ? DateTime.UtcNow : clone.UpdatedAtUtc;
        lock (_gate)
        {
            _rows[clone.AppKey] = clone;
        }

        return Task.CompletedTask;
    }
}
