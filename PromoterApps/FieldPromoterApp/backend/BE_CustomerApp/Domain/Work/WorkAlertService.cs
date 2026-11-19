using Dapper;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Gateway;

namespace BE_CustomerApp.Domain.Work;

public sealed record WorkAlertDto(
    long Id, long UserId, string Name, string Phone, string? CityName, string Type, decimal? Latitude, decimal? Longitude,
    DateTime SinceAt, DateTime LastSeenAt, int Minutes, DateTime CreatedAt, DateTime? ResolvedAt, bool IsOpen, string? SeenBy, DateTime? SeenAt,
    int? WorkMinutes = null, int? TargetMinutes = null);

public sealed record WorkAlertsSummary(int Unseen, IReadOnlyList<WorkAlertDto> Latest);

/// <summary>
/// Raises a dashboard alert when a working agent stays within <see cref="RadiusMeters"/> of one place for
/// <see cref="StationaryMinutes"/> (based on the points the phone sent, so a phone that stopped sending raises nothing).
/// The alert stays open while the agent is still there and resolves when they move on or finish the day.
/// <para><see cref="ClosedEarly"/> alerts (the agent ended the day by hand before the daily work-time target) are written
/// by <see cref="DailyTargetService"/>; they are never open, have a position only when the phone sent one, and carry
/// WorkMinutes / TargetMinutes.</para>
/// </summary>
public sealed class WorkAlertService
{
    public const string Stationary = "Stationary";
    public const string ClosedEarly = "ClosedEarly";

    private readonly Db _db;
    private readonly IClock _clock;
    private readonly BranchCatalog _branches;

    public WorkAlertService(Db db, IClock clock, BranchCatalog branches, IConfiguration config)
    {
        _db = db;
        _clock = clock;
        _branches = branches;
        StationaryMinutes = Math.Clamp(config.GetValue("WorkAlerts:StationaryMinutes", 60), 5, 600);
        RadiusMeters = Math.Clamp(config.GetValue("WorkAlerts:RadiusMeters", 150), 30, 2000);
    }

    public int StationaryMinutes { get; }
    public int RadiusMeters { get; }

    /// <summary>The newest point must be this recent for the agent to count as still there.</summary>
    private static readonly TimeSpan FreshPoint = TimeSpan.FromMinutes(20);

    private sealed record Point(decimal Latitude, decimal Longitude, DateTime RecordedAt);

    /// <summary>One pass over every open session. Returns the number of new alerts.</summary>
    public async Task<int> DetectAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        await using var c = await _db.OpenAsync(ct);

        // Days that were finished: close their alerts.
        await c.ExecuteAsync("""
            UPDATE a SET ResolvedAt = COALESCE(w.EndedAt, SYSUTCDATETIME())
            FROM agents.WorkAlerts a JOIN agents.WorkSessions w ON w.Id = a.SessionId
            WHERE a.ResolvedAt IS NULL AND w.EndedAt IS NOT NULL
            """);

        var sessions = (await c.QueryAsync<(long Id, long UserId)>("SELECT Id, UserId FROM agents.WorkSessions WHERE EndedAt IS NULL")).ToList();
        var created = 0;
        var lookBack = now.AddMinutes(-(StationaryMinutes + 120));
        foreach (var s in sessions)
        {
            var points = (await c.QueryAsync<Point>("""
                SELECT Latitude, Longitude, RecordedAt FROM agents.LocationPoints
                WHERE SessionId = @Id AND RecordedAt >= @lookBack ORDER BY RecordedAt DESC
                """, new { s.Id, lookBack })).ToList();
            var open = await c.QuerySingleOrDefaultAsync<(long Id, decimal Latitude, decimal Longitude)>(
                "SELECT TOP 1 Id, Latitude, Longitude FROM agents.WorkAlerts WHERE SessionId = @Id AND ResolvedAt IS NULL AND Type = @type ORDER BY Id DESC",
                new { s.Id, type = Stationary });
            if (points.Count == 0) continue;

            var latest = points[0];
            if (open.Id != 0)
            {
                if (DistanceMeters(latest.Latitude, latest.Longitude, open.Latitude, open.Longitude) > RadiusMeters)
                    await c.ExecuteAsync("UPDATE agents.WorkAlerts SET ResolvedAt = @At WHERE Id = @Id", new { open.Id, At = latest.RecordedAt });
                else
                    await c.ExecuteAsync("UPDATE agents.WorkAlerts SET LastSeenAt = @At WHERE Id = @Id AND LastSeenAt < @At", new { open.Id, At = latest.RecordedAt });
                continue;
            }

            if (now - latest.RecordedAt > FreshPoint) continue;
            var stay = StayStart(points, RadiusMeters);
            if (latest.RecordedAt - stay < TimeSpan.FromMinutes(StationaryMinutes)) continue;

            // Centre of the stay, so a jittery GPS does not resolve the alert at once.
            var inStay = points.TakeWhile(p => p.RecordedAt >= stay).ToList();
            var lat = Math.Round(inStay.Average(p => p.Latitude), 6);
            var lng = Math.Round(inStay.Average(p => p.Longitude), 6);
            created += await c.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM agents.WorkAlerts WITH (UPDLOCK, HOLDLOCK) WHERE SessionId = @Id AND ResolvedAt IS NULL)
                    INSERT agents.WorkAlerts(UserId, SessionId, Type, Latitude, Longitude, SinceAt, LastSeenAt)
                    VALUES (@UserId, @Id, @type, @lat, @lng, @stay, @last)
                """, new { s.Id, s.UserId, type = Stationary, lat, lng, stay, last = latest.RecordedAt });
        }
        return created;
    }

    /// <summary>Start of the current stay: the oldest point of the unbroken run (newest first) within the radius of the newest point.</summary>
    internal static DateTime StayStart(IReadOnlyList<(decimal Lat, decimal Lng, DateTime At)> newestFirst, int radiusMeters)
    {
        var anchor = newestFirst[0];
        var since = anchor.At;
        foreach (var p in newestFirst.Skip(1))
        {
            if (DistanceMeters(p.Lat, p.Lng, anchor.Lat, anchor.Lng) > radiusMeters) break;
            since = p.At;
        }
        return since;
    }

    private static DateTime StayStart(List<Point> newestFirst, int radiusMeters) =>
        StayStart(newestFirst.Select(p => (p.Latitude, p.Longitude, p.RecordedAt)).ToList(), radiusMeters);

    public static double DistanceMeters(decimal lat1, decimal lng1, decimal lat2, decimal lng2)
    {
        const double r = 6_371_000;
        double ToRad(decimal d) => (double)d * Math.PI / 180;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * r * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    // ---------------- Dashboard ----------------

    private const string Select = """
        SELECT a.Id, a.UserId, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName) AS Name, u.Phone, u.CityValue, a.Type,
               a.Latitude, a.Longitude, a.SinceAt, a.LastSeenAt, a.CreatedAt, a.ResolvedAt, a.SeenBy, a.SeenAt, a.WorkMinutes, a.TargetMinutes
        FROM agents.WorkAlerts a JOIN agents.Users u ON u.Id = a.UserId
        """;

    private sealed record Row(long Id, long UserId, string Name, string Phone, int? CityValue, string Type, decimal? Latitude, decimal? Longitude,
        DateTime SinceAt, DateTime LastSeenAt, DateTime CreatedAt, DateTime? ResolvedAt, string? SeenBy, DateTime? SeenAt,
        int? WorkMinutes, int? TargetMinutes);

    /// <summary>Alerts raised on an Iraq date (default today), newest first; openOnly = agent still there.</summary>
    public async Task<IReadOnlyList<WorkAlertDto>> ListAsync(DateOnly? date, bool openOnly, int? cityValue, CancellationToken ct)
    {
        var day = date ?? IraqTime.DateOf(_clock.UtcNow);
        var from = IraqTime.DayStartUtc(day);
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<Row>($"""
            {Select}
            WHERE a.CreatedAt >= @from AND a.CreatedAt < @to AND (@openOnly = 0 OR a.ResolvedAt IS NULL)
              AND (@cityValue IS NULL OR u.CityValue = @cityValue)
            ORDER BY a.CreatedAt DESC
            """, new { from, to = from.AddDays(1), openOnly, cityValue });
        return await MapAsync(rows, ct);
    }

    /// <summary>For the dashboard bell: alerts nobody has acknowledged yet (last 24 h).</summary>
    public async Task<WorkAlertsSummary> UnseenAsync(CancellationToken ct)
    {
        var since = _clock.UtcNow.AddHours(-24);
        await using var c = await _db.OpenAsync(ct);
        var rows = (await c.QueryAsync<Row>($"{Select} WHERE a.SeenAt IS NULL AND a.CreatedAt >= @since ORDER BY a.CreatedAt DESC", new { since })).ToList();
        return new WorkAlertsSummary(rows.Count, await MapAsync(rows.Take(20), ct));
    }

    /// <summary>Acknowledge one alert, or all unseen ones when id is null.</summary>
    public async Task MarkSeenAsync(long? id, AdminActor actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync("""
            UPDATE agents.WorkAlerts SET SeenBy = @Name, SeenAt = SYSUTCDATETIME() WHERE SeenAt IS NULL AND (@id IS NULL OR Id = @id)
            """, new { id, actor.Name });
    }

    private async Task<List<WorkAlertDto>> MapAsync(IEnumerable<Row> rows, CancellationToken ct)
    {
        var list = new List<WorkAlertDto>();
        foreach (var r in rows)
        {
            list.Add(new WorkAlertDto(r.Id, r.UserId, r.Name, r.Phone, r.CityValue is { } cv ? await _branches.NameAsync(cv, ct) : null, r.Type,
                r.Latitude, r.Longitude, r.SinceAt, r.LastSeenAt, (int)Math.Max(0, (r.LastSeenAt - r.SinceAt).TotalMinutes), r.CreatedAt,
                r.ResolvedAt, r.ResolvedAt is null, r.SeenBy, r.SeenAt, r.WorkMinutes, r.TargetMinutes));
        }
        return list;
    }
}
