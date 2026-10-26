using System.Collections.Concurrent;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryPriceBoardStore : IPriceBoardStore
{
    private readonly ConcurrentDictionary<int, PriceBoardEntry> _rows = new();
    private int _nextId = 1;

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<PriceBoardEntry>> ListAsync(string? cityValue, CancellationToken ct = default)
    {
        IEnumerable<PriceBoardEntry> q = _rows.Values;
        if (!string.IsNullOrWhiteSpace(cityValue))
        {
            var city = cityValue.Trim();
            q = q.Where(r => string.Equals(r.TargetCityValue, city, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<PriceBoardEntry>>(
            q.OrderByDescending(r => r.UpdatedAtUtc).ThenByDescending(r => r.Id).ToList());
    }

    public Task<PriceBoardEntry?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _rows.TryGetValue(id, out var row);
        return Task.FromResult(row);
    }

    public Task<PriceBoardEntry> InsertAsync(PriceBoardEntry row, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var now = DateTime.UtcNow;
        var saved = new PriceBoardEntry
        {
            Id = id,
            ProductName = row.ProductName,
            SalePrice = row.SalePrice,
            DailyInstallment = row.DailyInstallment,
            DownPayment = row.DownPayment,
            TargetCityValue = row.TargetCityValue,
            TargetCityName = row.TargetCityName,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByName = row.CreatedByName,
            UpdatedByName = row.UpdatedByName,
        };
        _rows[id] = saved;
        return Task.FromResult(saved);
    }

    public Task UpdateAsync(PriceBoardEntry row, CancellationToken ct = default)
    {
        if (!_rows.TryGetValue(row.Id, out var existing))
            return Task.CompletedTask;

        existing.ProductName = row.ProductName;
        existing.SalePrice = row.SalePrice;
        existing.DailyInstallment = row.DailyInstallment;
        existing.DownPayment = row.DownPayment;
        existing.TargetCityValue = row.TargetCityValue;
        existing.TargetCityName = row.TargetCityName;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByName = row.UpdatedByName;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_rows.TryRemove(id, out _));
}
