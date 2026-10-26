using System.Collections.Concurrent;

namespace BE_SalesEmployee.Sales.Stores;

public sealed class InMemorySalesEmployeeAppBlockStore : ISalesEmployeeAppBlockStore
{
    private readonly ConcurrentDictionary<string, SalesEmployeeAppBlockState> _map = new(StringComparer.OrdinalIgnoreCase);

    private static string Key(string city, int id) => $"{city.Trim()}|{id}";

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<bool> IsBlockedAsync(string cityValue, int employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue) || employeeId <= 0)
            return Task.FromResult(false);
        return Task.FromResult(
            _map.TryGetValue(Key(cityValue, employeeId), out var row) && row.IsBlocked);
    }

    public Task<SalesEmployeeAppBlockState?> GetAsync(
        string cityValue, int employeeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue) || employeeId <= 0)
            return Task.FromResult<SalesEmployeeAppBlockState?>(null);
        _map.TryGetValue(Key(cityValue, employeeId), out var row);
        return Task.FromResult(row);
    }

    public Task SetBlockedAsync(
        string cityValue,
        int employeeId,
        bool isBlocked,
        string? updatedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue) || employeeId <= 0)
            throw new ArgumentException("cityValue and employeeId required");
        var state = new SalesEmployeeAppBlockState
        {
            CityValue = cityValue.Trim(),
            EmployeeId = employeeId,
            IsBlocked = isBlocked,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = updatedBy,
        };
        _map[Key(cityValue, employeeId)] = state;
        return Task.CompletedTask;
    }
}
