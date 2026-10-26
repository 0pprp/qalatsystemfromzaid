using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IMobileTutorialVideoStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MobileAppTutorialVideo>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MobileAppTutorialVideo>> ListByAppAsync(string appKey, bool enabledOnly, CancellationToken ct = default);
    Task<MobileAppTutorialVideo?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MobileAppTutorialVideo> InsertAsync(MobileAppTutorialVideo row, CancellationToken ct = default);
    Task UpdateAsync(MobileAppTutorialVideo row, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
