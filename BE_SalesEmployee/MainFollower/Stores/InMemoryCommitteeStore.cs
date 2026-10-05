using BE_SalesEmployee.MainFollower.Domain;

namespace BE_SalesEmployee.MainFollower.Stores;

public sealed class InMemoryCommitteeStore : ICommitteeStore
{
    private readonly object _gate = new();
    private int _committeeSeq;
    private int _missionSeq;
    private readonly List<CommitteeRecord> _committees = [];
    private readonly List<MissionRecord> _missions = [];

    public Task<CommitteeRecord> InsertCommitteeAsync(
        string name,
        string createdByUserName,
        string createdByDisplayName,
        IReadOnlyList<string> memberUserNames,
        DateTime createdAtUtc,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var row = new CommitteeRecord
            {
                Id = ++_committeeSeq,
                Name = name,
                CreatedByUserName = createdByUserName,
                CreatedByDisplayName = createdByDisplayName,
                CreatedAtUtc = createdAtUtc,
                MemberUserNames = memberUserNames.ToList()
            };
            _committees.Add(row);
            return Task.FromResult(Clone(row));
        }
    }

    public Task<CommitteeRecord?> GetCommitteeAsync(int committeeId, CancellationToken ct)
    {
        lock (_gate)
        {
            var row = _committees.FirstOrDefault(c => c.Id == committeeId);
            return Task.FromResult(row is null ? null : Clone(row));
        }
    }

    public Task<MissionRecord> InsertMissionAsync(
        int committeeId,
        string committeeName,
        IReadOnlyList<string> memberUserNames,
        IReadOnlyList<string> cityValues,
        string? note,
        string createdByUserName,
        DateTime createdAtUtc,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var row = new MissionRecord
            {
                Id = ++_missionSeq,
                CommitteeId = committeeId,
                CommitteeName = committeeName,
                Status = CommitteeMissionStatuses.Active,
                Note = note,
                CreatedByUserName = createdByUserName,
                CreatedAtUtc = createdAtUtc,
                CityValues = cityValues.ToList(),
                MemberUserNames = memberUserNames.ToList()
            };
            _missions.Add(row);
            return Task.FromResult(Clone(row));
        }
    }

    public Task<IReadOnlyList<MissionRecord>> ListMissionsForMemberAsync(string memberUserName, CancellationToken ct)
    {
        lock (_gate)
        {
            var rows = _missions
                .Where(m => m.MemberUserNames.Any(u =>
                    string.Equals(u, memberUserName, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(m => m.CreatedAtUtc)
                .Select(Clone)
                .ToList();
            return Task.FromResult<IReadOnlyList<MissionRecord>>(rows);
        }
    }

    public Task<IReadOnlyList<MissionRecord>> ListAllMissionsAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<MissionRecord>>(
                _missions.OrderByDescending(m => m.CreatedAtUtc).Select(Clone).ToList());
        }
    }

    private static CommitteeRecord Clone(CommitteeRecord row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        CreatedByUserName = row.CreatedByUserName,
        CreatedByDisplayName = row.CreatedByDisplayName,
        CreatedAtUtc = row.CreatedAtUtc,
        MemberUserNames = row.MemberUserNames.ToList()
    };

    private static MissionRecord Clone(MissionRecord row) => new()
    {
        Id = row.Id,
        CommitteeId = row.CommitteeId,
        CommitteeName = row.CommitteeName,
        Status = row.Status,
        Note = row.Note,
        CreatedByUserName = row.CreatedByUserName,
        CreatedAtUtc = row.CreatedAtUtc,
        CityValues = row.CityValues.ToList(),
        MemberUserNames = row.MemberUserNames.ToList()
    };
}
