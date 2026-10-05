using BE_SalesEmployee.MainFollower.Domain;

namespace BE_SalesEmployee.MainFollower.Stores;

public interface IStaffReviewStore
{
    Task<StaffReviewRecord> InsertAsync(StaffReviewRecord row, CancellationToken ct);
    Task<IReadOnlyList<StaffReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct);
    Task<IReadOnlyList<StaffReviewRecord>> ListForCityAsync(string cityValue, CancellationToken ct);
    Task<IReadOnlyList<StaffReviewRecord>> ListForTargetAsync(
        string targetType,
        int targetId,
        string? cityValue,
        CancellationToken ct);
}
