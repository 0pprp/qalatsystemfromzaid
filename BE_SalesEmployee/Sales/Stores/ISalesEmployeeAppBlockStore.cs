namespace BE_SalesEmployee.Sales.Stores;

public sealed class SalesEmployeeAppBlockState
{
    public string CityValue { get; set; } = "";
    public int EmployeeId { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public interface ISalesEmployeeAppBlockStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<bool> IsBlockedAsync(string cityValue, int employeeId, CancellationToken ct = default);
    Task<SalesEmployeeAppBlockState?> GetAsync(string cityValue, int employeeId, CancellationToken ct = default);
    Task SetBlockedAsync(
        string cityValue,
        int employeeId,
        bool isBlocked,
        string? updatedBy,
        CancellationToken ct = default);
}
