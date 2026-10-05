using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Stores;

namespace BE_SalesEmployee.MainFollower.Services;

public sealed class CommitteeService
{
    private readonly ICommitteeStore _store;

    public CommitteeService(ICommitteeStore store)
    {
        _store = store;
    }

    public async Task<CommitteeRecord> CreateCommitteeAsync(
        string createdByUserName,
        string createdByDisplayName,
        string name,
        IEnumerable<string?> memberUserNames,
        CancellationToken ct)
    {
        var members = CommitteeRules.NormalizeMembers(memberUserNames);
        var err = CommitteeRules.ValidateMembers(members);
        if (err != null)
        {
            throw new CommitteeException(err);
        }

        var trimmedName = (name ?? "").Trim();
        if (trimmedName.Length == 0)
        {
            throw new CommitteeException("اسم اللجنة مطلوب.");
        }

        return await _store.InsertCommitteeAsync(
            trimmedName,
            (createdByUserName ?? "").Trim(),
            (createdByDisplayName ?? "").Trim(),
            members,
            DateTime.UtcNow,
            ct);
    }

    public async Task<MissionRecord> CreateMissionAsync(
        string createdByUserName,
        int committeeId,
        IEnumerable<string?> cityValues,
        string? note,
        CancellationToken ct)
    {
        var cities = CommitteeRules.NormalizeCities(cityValues);
        var err = CommitteeRules.ValidateCities(cities);
        if (err != null)
        {
            throw new CommitteeException(err);
        }

        var committee = await _store.GetCommitteeAsync(committeeId, ct)
                        ?? throw new CommitteeException("اللجنة غير موجودة.");

        return await _store.InsertMissionAsync(
            committee.Id,
            committee.Name,
            committee.MemberUserNames,
            cities,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            (createdByUserName ?? "").Trim(),
            DateTime.UtcNow,
            ct);
    }

    public Task<IReadOnlyList<MissionRecord>> ListMissionsForMemberAsync(string memberUserName, CancellationToken ct) =>
        _store.ListMissionsForMemberAsync((memberUserName ?? "").Trim(), ct);

    public Task<IReadOnlyList<MissionRecord>> ListAllMissionsAsync(CancellationToken ct) =>
        _store.ListAllMissionsAsync(ct);
}
