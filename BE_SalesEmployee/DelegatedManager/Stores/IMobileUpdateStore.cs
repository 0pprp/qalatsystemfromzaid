using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IMobileUpdateStore
{
    Task<MobileAppRelease?> GetAsync(string appKey, CancellationToken ct = default);

    Task UpsertAsync(MobileAppRelease release, CancellationToken ct = default);
}
