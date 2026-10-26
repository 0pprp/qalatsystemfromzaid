using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IAppCommitteeTaskStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AppCommitteeTask>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AppCommitteeTask>> ListEnabledAsync(CancellationToken ct = default);
    Task<AppCommitteeTask?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AppCommitteeTask> InsertAsync(AppCommitteeTask row, CancellationToken ct = default);
    Task UpdateAsync(AppCommitteeTask row, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
