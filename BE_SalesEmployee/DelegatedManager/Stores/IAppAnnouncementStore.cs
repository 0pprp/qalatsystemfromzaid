using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IAppAnnouncementStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AppAnnouncement>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AppAnnouncement>> ListByAppAsync(string appKey, bool enabledOnly, CancellationToken ct = default);
    Task<AppAnnouncement?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<AppAnnouncement> InsertAsync(AppAnnouncement row, CancellationToken ct = default);
    Task UpdateAsync(AppAnnouncement row, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
