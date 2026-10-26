using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IPriceBoardStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PriceBoardEntry>> ListAsync(string? cityValue, CancellationToken ct = default);
    Task<PriceBoardEntry?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<PriceBoardEntry> InsertAsync(PriceBoardEntry row, CancellationToken ct = default);
    Task UpdateAsync(PriceBoardEntry row, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
