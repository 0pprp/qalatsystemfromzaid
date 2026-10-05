using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IMobileTutorialLinkStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MobileAppTutorialLink>> ListAsync(CancellationToken ct = default);
    Task<MobileAppTutorialLink?> GetAsync(string appKey, CancellationToken ct = default);
    Task UpsertAsync(MobileAppTutorialLink row, CancellationToken ct = default);
}
