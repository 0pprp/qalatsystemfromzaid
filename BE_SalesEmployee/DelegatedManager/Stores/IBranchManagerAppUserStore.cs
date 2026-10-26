using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IBranchManagerAppUserStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BranchManagerAppUser>> ListAsync(CancellationToken ct = default);
    Task<BranchManagerAppUser?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BranchManagerAppUser?> FindByUserNameAsync(string userName, CancellationToken ct = default);
    Task<BranchManagerAppUser> UpsertAsync(BranchManagerAppUser user, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
