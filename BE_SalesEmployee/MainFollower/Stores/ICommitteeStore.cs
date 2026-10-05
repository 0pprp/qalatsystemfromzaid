using BE_SalesEmployee.MainFollower.Domain;

namespace BE_SalesEmployee.MainFollower.Stores;

public interface ICommitteeStore
{
    Task<CommitteeRecord> InsertCommitteeAsync(
        string name,
        string createdByUserName,
        string createdByDisplayName,
        IReadOnlyList<string> memberUserNames,
        DateTime createdAtUtc,
        CancellationToken ct);

    Task<CommitteeRecord?> GetCommitteeAsync(int committeeId, CancellationToken ct);

    Task<MissionRecord> InsertMissionAsync(
        int committeeId,
        string committeeName,
        IReadOnlyList<string> memberUserNames,
        IReadOnlyList<string> cityValues,
        string? note,
        string createdByUserName,
        DateTime createdAtUtc,
        CancellationToken ct);

    Task<IReadOnlyList<MissionRecord>> ListMissionsForMemberAsync(string memberUserName, CancellationToken ct);

    Task<IReadOnlyList<MissionRecord>> ListAllMissionsAsync(CancellationToken ct);
}
