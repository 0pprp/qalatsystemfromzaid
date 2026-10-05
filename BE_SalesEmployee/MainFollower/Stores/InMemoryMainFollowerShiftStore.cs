using BE_SalesEmployee.MainFollower.Domain;

namespace BE_SalesEmployee.MainFollower.Stores;

public interface IMainFollowerShiftStore
{
    Task<MainFollowerShiftRecord?> GetActiveAsync(string userName, CancellationToken ct);
    Task<MainFollowerShiftRecord> StartAsync(MainFollowerShiftRecord row, CancellationToken ct);
    Task<MainFollowerShiftRecord?> EndAsync(
        string userName,
        double? endLat,
        double? endLng,
        DateTime endedAtUtc,
        CancellationToken ct);
    Task<MainFollowerLocationPoint?> LastPointAsync(string userName, CancellationToken ct);
    Task<MainFollowerLocationPoint> AddPointAsync(MainFollowerLocationPoint point, CancellationToken ct);
    Task<IReadOnlyList<MainFollowerLocationPoint>> ListTrailAsync(
        string userName,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct);
    Task<IReadOnlyList<(MainFollowerShiftRecord? Active, MainFollowerLocationPoint? Last)>> SnapshotAsync(
        IEnumerable<string> userNames,
        CancellationToken ct);
}

public sealed class InMemoryMainFollowerShiftStore : IMainFollowerShiftStore
{
    private readonly object _gate = new();
    private int _shiftSeq;
    private long _pointSeq;
    private readonly List<MainFollowerShiftRecord> _shifts = [];
    private readonly List<MainFollowerLocationPoint> _points = [];

    public Task<MainFollowerShiftRecord?> GetActiveAsync(string userName, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = (userName ?? "").Trim();
            var row = _shifts.LastOrDefault(s =>
                string.Equals(s.UserName, key, StringComparison.OrdinalIgnoreCase)
                && s.EndedAtUtc == null);
            return Task.FromResult(row == null ? null : Clone(row));
        }
    }

    public Task<MainFollowerShiftRecord> StartAsync(MainFollowerShiftRecord row, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = (row.UserName ?? "").Trim();
            if (_shifts.Any(s =>
                    string.Equals(s.UserName, key, StringComparison.OrdinalIgnoreCase)
                    && s.EndedAtUtc == null))
            {
                throw new CommitteeException("لديك دوام نشط بالفعل.");
            }

            row.Id = ++_shiftSeq;
            row.UserName = key;
            _shifts.Add(Clone(row));
            return Task.FromResult(Clone(row));
        }
    }

    public Task<MainFollowerShiftRecord?> EndAsync(
        string userName,
        double? endLat,
        double? endLng,
        DateTime endedAtUtc,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var key = (userName ?? "").Trim();
            var row = _shifts.LastOrDefault(s =>
                string.Equals(s.UserName, key, StringComparison.OrdinalIgnoreCase)
                && s.EndedAtUtc == null);
            if (row == null) return Task.FromResult<MainFollowerShiftRecord?>(null);
            row.EndedAtUtc = endedAtUtc;
            row.EndLatitude = endLat;
            row.EndLongitude = endLng;
            return Task.FromResult<MainFollowerShiftRecord?>(Clone(row));
        }
    }

    public Task<MainFollowerLocationPoint?> LastPointAsync(string userName, CancellationToken ct)
    {
        lock (_gate)
        {
            var key = (userName ?? "").Trim();
            var row = _points.LastOrDefault(p =>
                string.Equals(p.UserName, key, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(row == null ? null : Clone(row));
        }
    }

    public Task<MainFollowerLocationPoint> AddPointAsync(MainFollowerLocationPoint point, CancellationToken ct)
    {
        lock (_gate)
        {
            point.Id = ++_pointSeq;
            point.UserName = (point.UserName ?? "").Trim();
            _points.Add(Clone(point));
            return Task.FromResult(Clone(point));
        }
    }

    public Task<IReadOnlyList<MainFollowerLocationPoint>> ListTrailAsync(
        string userName,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var key = (userName ?? "").Trim();
            var q = _points.Where(p =>
                string.Equals(p.UserName, key, StringComparison.OrdinalIgnoreCase));
            if (fromUtc != null) q = q.Where(p => p.RecordedAtUtc >= fromUtc);
            if (toUtc != null) q = q.Where(p => p.RecordedAtUtc <= toUtc);
            return Task.FromResult<IReadOnlyList<MainFollowerLocationPoint>>(
                q.OrderBy(p => p.RecordedAtUtc).Select(Clone).ToList());
        }
    }

    public Task<IReadOnlyList<(MainFollowerShiftRecord? Active, MainFollowerLocationPoint? Last)>> SnapshotAsync(
        IEnumerable<string> userNames,
        CancellationToken ct)
    {
        lock (_gate)
        {
            var result = new List<(MainFollowerShiftRecord? Active, MainFollowerLocationPoint? Last)>();
            foreach (var raw in userNames)
            {
                var key = (raw ?? "").Trim();
                var active = _shifts.LastOrDefault(s =>
                    string.Equals(s.UserName, key, StringComparison.OrdinalIgnoreCase)
                    && s.EndedAtUtc == null);
                var last = _points.LastOrDefault(p =>
                    string.Equals(p.UserName, key, StringComparison.OrdinalIgnoreCase));
                result.Add((active == null ? null : Clone(active), last == null ? null : Clone(last)));
            }

            return Task.FromResult<IReadOnlyList<(MainFollowerShiftRecord? Active, MainFollowerLocationPoint? Last)>>(result);
        }
    }

    private static MainFollowerShiftRecord Clone(MainFollowerShiftRecord r) => new()
    {
        Id = r.Id,
        UserName = r.UserName,
        DisplayName = r.DisplayName,
        StartedAtUtc = r.StartedAtUtc,
        EndedAtUtc = r.EndedAtUtc,
        StartLatitude = r.StartLatitude,
        StartLongitude = r.StartLongitude,
        EndLatitude = r.EndLatitude,
        EndLongitude = r.EndLongitude
    };

    private static MainFollowerLocationPoint Clone(MainFollowerLocationPoint p) => new()
    {
        Id = p.Id,
        ShiftId = p.ShiftId,
        UserName = p.UserName,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        AccuracyMeters = p.AccuracyMeters,
        RecordedAtUtc = p.RecordedAtUtc
    };
}
