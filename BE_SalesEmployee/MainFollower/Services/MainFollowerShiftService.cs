using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Stores;

namespace BE_SalesEmployee.MainFollower.Services;

public sealed class MainFollowerShiftService
{
    private readonly IMainFollowerShiftStore _store;

    public MainFollowerShiftService(IMainFollowerShiftStore store)
    {
        _store = store;
    }

    public Task<MainFollowerShiftRecord?> GetCurrentAsync(string userName, CancellationToken ct) =>
        _store.GetActiveAsync(userName, ct);

    public async Task<MainFollowerShiftRecord> StartAsync(
        string userName,
        string displayName,
        double? latitude,
        double? longitude,
        CancellationToken ct)
    {
        var key = (userName ?? "").Trim();
        if (key.Length == 0) throw new CommitteeException("اسم المستخدم مطلوب.");

        return await _store.StartAsync(new MainFollowerShiftRecord
        {
            UserName = key,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim(),
            StartedAtUtc = DateTime.UtcNow,
            StartLatitude = latitude,
            StartLongitude = longitude
        }, ct);
    }

    public async Task<MainFollowerShiftRecord> EndAsync(
        string userName,
        double? latitude,
        double? longitude,
        CancellationToken ct)
    {
        var ended = await _store.EndAsync(userName, latitude, longitude, DateTime.UtcNow, ct);
        if (ended == null) throw new CommitteeException("لا يوجد دوام نشط لإنهائه.");
        return ended;
    }

    public async Task<MainFollowerLocationPoint> RecordLocationAsync(
        string userName,
        double latitude,
        double longitude,
        double? accuracyMeters,
        DateTime? recordedAtUtc,
        CancellationToken ct)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new CommitteeException("إحداثيات الموقع غير صالحة.");
        }

        var shift = await _store.GetActiveAsync(userName, ct)
            ?? throw new CommitteeException("يجب بدء الدوام قبل إرسال الموقع.");

        var at = recordedAtUtc?.ToUniversalTime() ?? DateTime.UtcNow;
        var last = await _store.LastPointAsync(userName, ct);
        if (last != null && at - last.RecordedAtUtc < MainFollowerShiftRules.MinLocationInterval)
        {
            throw new CommitteeException("تم تسجيل نقطة موقع مؤخراً، انتظر قليلاً.");
        }

        return await _store.AddPointAsync(new MainFollowerLocationPoint
        {
            ShiftId = shift.Id,
            UserName = shift.UserName,
            Latitude = latitude,
            Longitude = longitude,
            AccuracyMeters = accuracyMeters,
            RecordedAtUtc = at
        }, ct);
    }

    public Task<IReadOnlyList<MainFollowerLocationPoint>> TrailAsync(
        string userName,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct) =>
        _store.ListTrailAsync(userName, fromUtc, toUtc, ct);

    public async Task<IReadOnlyList<object>> LiveSnapshotAsync(
        IEnumerable<(string UserName, string DisplayName)> accounts,
        CancellationToken ct)
    {
        var list = accounts.ToList();
        var snap = await _store.SnapshotAsync(list.Select(a => a.UserName), ct);
        var now = DateTime.UtcNow;
        var rows = new List<object>();
        for (var i = 0; i < list.Count; i++)
        {
            var acc = list[i];
            var (active, last) = snap[i];
            var hasShift = active != null;
            rows.Add(new
            {
                userName = acc.UserName,
                displayName = string.IsNullOrWhiteSpace(acc.DisplayName) ? acc.UserName : acc.DisplayName,
                shiftStatus = hasShift ? "Active" : "Closed",
                shiftId = active?.Id,
                startedAtUtc = active?.StartedAtUtc,
                lastLatitude = last?.Latitude,
                lastLongitude = last?.Longitude,
                lastAccuracy = last?.AccuracyMeters,
                lastLocationAtUtc = last?.RecordedAtUtc,
                locationStatus = MainFollowerShiftRules.LocationStatus(last?.RecordedAtUtc, now, hasShift)
            });
        }

        return rows;
    }
}
