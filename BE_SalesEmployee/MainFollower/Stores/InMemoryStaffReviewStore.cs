using BE_SalesEmployee.MainFollower.Domain;

namespace BE_SalesEmployee.MainFollower.Stores;

public sealed class InMemoryStaffReviewStore : IStaffReviewStore
{
    private readonly object _gate = new();
    private int _seq;
    private readonly List<StaffReviewRecord> _rows = [];

    public Task<StaffReviewRecord> InsertAsync(StaffReviewRecord row, CancellationToken ct)
    {
        lock (_gate)
        {
            var saved = Clone(row);
            saved.Id = ++_seq;
            _rows.Add(saved);
            return Task.FromResult(Clone(saved));
        }
    }

    public Task<IReadOnlyList<StaffReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StaffReviewRecord>>(
                _rows.Where(r => r.MissionId == missionId)
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .Select(Clone)
                    .ToList());
        }
    }

    public Task<IReadOnlyList<StaffReviewRecord>> ListForCityAsync(string cityValue, CancellationToken ct)
    {
        var city = (cityValue ?? "").Trim();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<StaffReviewRecord>>(
                _rows.Where(r => string.Equals(r.CityValue, city, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.CreatedAtUtc)
                    .Select(Clone)
                    .ToList());
        }
    }

    public Task<IReadOnlyList<StaffReviewRecord>> ListForTargetAsync(
        string targetType,
        int targetId,
        string? cityValue,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var city = (cityValue ?? "").Trim();
            var rows = _rows
                .Where(r =>
                    string.Equals(r.TargetType, targetType, StringComparison.OrdinalIgnoreCase)
                    && r.TargetId == targetId
                    && (city.Length == 0
                        || string.Equals(r.CityValue, city, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(r => r.CreatedAtUtc)
                .Select(Clone)
                .ToList();
            return Task.FromResult<IReadOnlyList<StaffReviewRecord>>(rows);
        }
    }

    private static StaffReviewRecord Clone(StaffReviewRecord row) => new()
    {
        Id = row.Id,
        MissionId = row.MissionId,
        CommitteeName = row.CommitteeName,
        CityValue = row.CityValue,
        TargetType = row.TargetType,
        TargetId = row.TargetId,
        TargetName = row.TargetName,
        ListId = row.ListId,
        Score = row.Score,
        UserNote = row.UserNote,
        FeedNote = row.FeedNote,
        ReviewerUserName = row.ReviewerUserName,
        CreatedAtUtc = row.CreatedAtUtc
    };
}
