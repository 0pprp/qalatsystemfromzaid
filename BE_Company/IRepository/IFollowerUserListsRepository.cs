using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IFollowerUserListsRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task<IReadOnlyList<FollowerListOptionDTO>> GetAvailableListsAsync(CancellationToken ct = default);
        Task<IReadOnlyList<int>> GetAssignedListIdsAsync(int userId, CancellationToken ct = default);
        Task ReplaceAssignmentsAsync(int userId, IEnumerable<int> listIds, CancellationToken ct = default);
        Task ClearAssignmentsAsync(int userId, CancellationToken ct = default);
    }
}
