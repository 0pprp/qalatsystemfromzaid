using Dapper;
using Microsoft.Data.SqlClient;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Gateway;

namespace BE_CustomerApp.Domain.Work;

/// <summary>Iraq has no daylight saving: a work date is the UTC+3 calendar day.</summary>
public static class IraqTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(utc + Offset);

    /// <summary>UTC instant at which the Iraq day starts.</summary>
    public static DateTime DayStartUtc(DateOnly date) => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);
}

public sealed record LocationInput(decimal Latitude, decimal Longitude, float? Accuracy, float? Speed, int? Battery, DateTime RecordedAt);

public sealed record TrackPointDto(decimal Lat, decimal Lng, DateTime At, float? Accuracy);

public sealed record WorkSessionDto(long Id, DateTime StartedAt, DateTime? EndedAt, int Minutes, string? EndedBy,
    decimal? StartLat, decimal? StartLng, decimal? EndLat, decimal? EndLng);

/// <summary>One agent's day: sessions, totals and the location trail.</summary>
public sealed record WorkDayDto(
    string Date, bool IsWorking, WorkSessionDto? CurrentSession, IReadOnlyList<WorkSessionDto> Sessions, int TotalMinutes,
    DateTime? FirstStartedAt, DateTime? LastEndedAt, IReadOnlyList<TrackPointDto> Track);

public sealed record LocationBatchResult(int Accepted, bool IsWorking);

/// <summary>Dashboard attendance row: one agent on one date.</summary>
public sealed record AttendanceRowDto(
    long UserId, string Name, string? Username, string Phone, int? CityValue, string? CityName, string Date,
    DateTime? FirstStartedAt, DateTime? LastEndedAt, int TotalMinutes, int SessionsCount, bool IsWorking,
    TrackPointDto? LastPoint, int PointsCount);

public sealed record AgentDayDto(AttendanceRowDto Summary, IReadOnlyList<WorkSessionDto> Sessions, IReadOnlyList<TrackPointDto> Track);

public sealed record LiveAgentDto(long UserId, string Name, string Phone, string? CityName, DateTime StartedAt, TrackPointDto? LastPoint);

/// <summary>
/// «الدوام»: an agent starts the working day, the app sends location points while it is open, and the agent (or the
/// nightly auto-close) finishes it. The dashboard reads per-day attendance and trails.
/// </summary>
public sealed class WorkService
{
    private const int MaxBatch = 500;
    private const int MaxTrackPoints = 5000;

    private readonly Db _db;
    private readonly IClock _clock;
    private readonly BranchCatalog _branches;
    private readonly DailyTargetService _dailyTarget;

    public WorkService(Db db, IClock clock, BranchCatalog branches, DailyTargetService dailyTarget)
    {
        _db = db;
        _clock = clock;
        _branches = branches;
        _dailyTarget = dailyTarget;
    }

    // ---------------- Agent ----------------

    public async Task<WorkDayDto> GetDayAsync(long userId, DateOnly? date, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await LoadDayAsync(userId, date ?? IraqTime.DateOf(_clock.UtcNow), c);
    }

    public async Task<WorkDayDto> StartAsync(long userId, decimal? lat, decimal? lng, CancellationToken ct)
    {
        ValidateCoordinates(lat, lng);
        var now = _clock.UtcNow;
        await using var c = await _db.OpenAsync(ct);
        try
        {
            await c.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM agents.WorkSessions WITH (UPDLOCK, HOLDLOCK) WHERE UserId = @userId AND EndedAt IS NULL)
                    INSERT agents.WorkSessions(UserId, WorkDate, StartedAt, StartLat, StartLng) VALUES (@userId, @date, @now, @lat, @lng)
                """, new { userId, date = IraqTime.DateOf(now).ToDateTime(TimeOnly.MinValue), now, lat, lng });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // Started twice at the same moment: the other call won, nothing to do.
        }
        if (lat is not null && lng is not null)
            await AddPointsAsync(userId, [new LocationInput(lat.Value, lng.Value, null, null, null, now)], ct);
        return await LoadDayAsync(userId, IraqTime.DateOf(now), c);
    }

    public async Task<WorkDayDto> EndAsync(long userId, decimal? lat, decimal? lng, CancellationToken ct)
    {
        ValidateCoordinates(lat, lng);
        var now = _clock.UtcNow;
        if (lat is not null && lng is not null)
            await AddPointsAsync(userId, [new LocationInput(lat.Value, lng.Value, null, null, null, now)], ct);
        await using var c = await _db.OpenAsync(ct);
        var closed = (await c.QueryAsync<long>("""
            UPDATE agents.WorkSessions SET EndedAt = @now, EndLat = @lat, EndLng = @lng, EndedBy = 'user'
            OUTPUT inserted.Id
            WHERE UserId = @userId AND EndedAt IS NULL
            """, new { userId, now, lat, lng })).ToList();
        // Ended before the daily target: the dashboard is told.
        if (closed.Count > 0) await _dailyTarget.OnDayEndedByAgentAsync(userId, closed[0], lat, lng, now, ct);
        return await LoadDayAsync(userId, IraqTime.DateOf(now), c);
    }

    /// <summary>
    /// Stores points for the open session. Points sent while not working are dropped and IsWorking=false tells the
    /// app to stop tracking (e.g. the day was auto-closed at midnight).
    /// </summary>
    /// <summary>
    /// A new key for the phone's background tracking service, valid for the agent's open session only (a new key replaces
    /// the previous one). The service sends points with it while the app is closed; it stops working when the day ends.
    /// </summary>
    public async Task<string> IssueTrackingKeyAsync(long userId, CancellationToken ct)
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        await using var c = await _db.OpenAsync(ct);
        var updated = await c.ExecuteAsync(
            "UPDATE agents.WorkSessions SET TrackingKeyHash = @hash WHERE UserId = @userId AND EndedAt IS NULL",
            new { userId, hash = HashKey(key) });
        if (updated == 0) throw ApiException.Conflict("not_working", "ابدأ الدوام أولاً");
        return key;
    }

    /// <summary>Points from the background tracking service. IsWorking=false tells the service to stop.</summary>
    public async Task<LocationBatchResult> AddPointsByKeyAsync(string? key, IReadOnlyList<LocationInput>? points, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100) return new LocationBatchResult(0, false);
        long userId;
        await using (var c = await _db.OpenAsync(ct))
        {
            userId = await c.ExecuteScalarAsync<long?>(
                "SELECT UserId FROM agents.WorkSessions WHERE TrackingKeyHash = @hash AND EndedAt IS NULL", new { hash = HashKey(key.Trim()) }) ?? 0;
        }
        return userId == 0 ? new LocationBatchResult(0, false) : await AddPointsAsync(userId, points, ct);
    }

    private static string HashKey(string key) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public async Task<LocationBatchResult> AddPointsAsync(long userId, IReadOnlyList<LocationInput>? points, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var session = await c.QuerySingleOrDefaultAsync<(long Id, DateTime StartedAt)>(
            "SELECT Id, StartedAt FROM agents.WorkSessions WHERE UserId = @userId AND EndedAt IS NULL", new { userId });
        if (session.Id == 0) return new LocationBatchResult(0, false);

        var now = _clock.UtcNow;
        var valid = (points ?? [])
            .Take(MaxBatch)
            .Select(p => p with { RecordedAt = DateTime.SpecifyKind(p.RecordedAt.ToUniversalTime(), DateTimeKind.Utc) })
            .Where(p => p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180 && !(p.Latitude == 0 && p.Longitude == 0))
            .Where(p => p.RecordedAt >= session.StartedAt.AddMinutes(-2) && p.RecordedAt <= now.AddMinutes(5))
            .Select(p => new
            {
                userId, sessionId = session.Id, lat = Math.Round(p.Latitude, 6), lng = Math.Round(p.Longitude, 6),
                p.Accuracy, p.Speed, battery = p.Battery is >= 0 and <= 100 ? (byte?)p.Battery : null, p.RecordedAt,
            })
            .ToList();
        if (valid.Count > 0)
        {
            await c.ExecuteAsync("""
                INSERT agents.LocationPoints(UserId, SessionId, Latitude, Longitude, Accuracy, Speed, Battery, RecordedAt)
                VALUES (@userId, @sessionId, @lat, @lng, @Accuracy, @Speed, @battery, @RecordedAt)
                """, valid);
        }
        return new LocationBatchResult(valid.Count, true);
    }

    /// <summary>Closes sessions left open from a previous day at their last point (or the end of that day).</summary>
    public async Task<int> AutoCloseAsync(CancellationToken ct)
    {
        var today = IraqTime.DateOf(_clock.UtcNow).ToDateTime(TimeOnly.MinValue);
        await using var c = await _db.OpenAsync(ct);
        return await c.ExecuteAsync("""
            UPDATE w SET
                EndedAt = COALESCE(
                    (SELECT MAX(p.RecordedAt) FROM agents.LocationPoints p WHERE p.SessionId = w.Id),
                    DATEADD(SECOND, -1, DATEADD(HOUR, -3, DATEADD(DAY, 1, CAST(w.WorkDate AS DATETIME2))))),
                EndedBy = 'auto'
            FROM agents.WorkSessions w
            WHERE w.EndedAt IS NULL AND w.WorkDate < @today
            """, new { today });
    }

    // ---------------- Dashboard ----------------

    /// <summary>Every active agent on a date, including those who did not work (zeros), working ones first.</summary>
    public async Task<IReadOnlyList<AttendanceRowDto>> AttendanceAsync(DateOnly date, int? cityValue, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var agents = (await c.QueryAsync<(long Id, string Name, string? Username, string Phone, int? CityValue)>("""
            SELECT Id, CONCAT(FirstName, ' ', FatherName, ' ', GrandfatherName) AS Name, Username, Phone, CityValue
            FROM agents.Users WHERE IsDeleted = 0 AND Username IS NOT NULL AND (@cityValue IS NULL OR CityValue = @cityValue)
            """, new { cityValue })).ToList();
        var sessions = await LoadSessionsAsync(c, date, null);
        // Last point and point count per agent for the day.
        var stats = (await c.QueryAsync<(long UserId, int Points, decimal Lat, decimal Lng, DateTime At, float? Accuracy)>("""
            WITH x AS (
                SELECT s.UserId, p.Latitude, p.Longitude, p.RecordedAt, p.Accuracy,
                       ROW_NUMBER() OVER (PARTITION BY s.UserId ORDER BY p.RecordedAt DESC) AS rn,
                       COUNT(*) OVER (PARTITION BY s.UserId) AS cnt
                FROM agents.LocationPoints p JOIN agents.WorkSessions s ON s.Id = p.SessionId
                WHERE s.WorkDate = @date)
            SELECT UserId, cnt, Latitude, Longitude, RecordedAt, Accuracy FROM x WHERE rn = 1
            """, new { date = date.ToDateTime(TimeOnly.MinValue) })).ToDictionary(r => r.UserId);

        var rows = new List<AttendanceRowDto>();
        foreach (var a in agents)
        {
            var mine = sessions.Where(s => s.UserId == a.Id).Select(s => s.Session).ToList();
            stats.TryGetValue(a.Id, out var last);
            rows.Add(Summarize(a.Id, a.Name, a.Username, a.Phone, a.CityValue, a.CityValue is { } cv ? await _branches.NameAsync(cv, ct) : null,
                date, mine, last.UserId == 0 ? null : new TrackPointDto(last.Lat, last.Lng, last.At, last.Accuracy), last.Points));
        }
        return rows.OrderByDescending(r => r.IsWorking).ThenByDescending(r => r.TotalMinutes).ThenBy(r => r.Name).ToList();
    }

    public async Task<AgentDayDto> AgentDayAsync(long userId, DateOnly date, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var a = await c.QuerySingleOrDefaultAsync<(long Id, string Name, string? Username, string Phone, int? CityValue)>(
            "SELECT Id, CONCAT(FirstName, ' ', FatherName, ' ', GrandfatherName) AS Name, Username, Phone, CityValue FROM agents.Users WHERE Id = @userId",
            new { userId });
        if (a.Id == 0) throw ApiException.NotFound("المسوّق غير موجود");
        var day = await LoadDayAsync(userId, date, c);
        var summary = Summarize(a.Id, a.Name, a.Username, a.Phone, a.CityValue, a.CityValue is { } cv ? await _branches.NameAsync(cv, ct) : null,
            date, day.Sessions.ToList(), day.Track.Count > 0 ? day.Track[^1] : null, day.Track.Count);
        return new AgentDayDto(summary, day.Sessions, day.Track);
    }

    /// <summary>One agent's worked days in a range (newest first), for the attendance history. Max 93 days.</summary>
    public async Task<IReadOnlyList<AttendanceRowDto>> AgentHistoryAsync(long userId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 92) from = to.AddDays(-92);
        await using var c = await _db.OpenAsync(ct);
        var a = await c.QuerySingleOrDefaultAsync<(long Id, string Name, string? Username, string Phone, int? CityValue)>(
            "SELECT Id, CONCAT(FirstName, ' ', FatherName, ' ', GrandfatherName) AS Name, Username, Phone, CityValue FROM agents.Users WHERE Id = @userId",
            new { userId });
        if (a.Id == 0) throw ApiException.NotFound("المسوّق غير موجود");
        var cityName = a.CityValue is { } cv ? await _branches.NameAsync(cv, ct) : null;
        var now = _clock.UtcNow;
        var sessions = await c.QueryAsync<(DateTime WorkDate, long Id, DateTime StartedAt, DateTime? EndedAt, string? EndedBy, int Points)>("""
            SELECT w.WorkDate, w.Id, w.StartedAt, w.EndedAt, w.EndedBy, (SELECT COUNT(*) FROM agents.LocationPoints p WHERE p.SessionId = w.Id)
            FROM agents.WorkSessions w WHERE w.UserId = @userId AND w.WorkDate BETWEEN @from AND @to
            """, new { userId, from = from.ToDateTime(TimeOnly.MinValue), to = to.ToDateTime(TimeOnly.MinValue) });
        return sessions.GroupBy(s => DateOnly.FromDateTime(s.WorkDate)).OrderByDescending(g => g.Key)
            .Select(g => Summarize(a.Id, a.Name, a.Username, a.Phone, a.CityValue, cityName, g.Key,
                g.Select(s => new WorkSessionDto(s.Id, s.StartedAt, s.EndedAt, (int)Math.Max(0, ((s.EndedAt ?? now) - s.StartedAt).TotalMinutes),
                    s.EndedBy, null, null, null, null)).ToList(), null, g.Sum(s => s.Points)))
            .ToList();
    }

    /// <summary>Agents working right now with their latest point, for the live map.</summary>
    public async Task<IReadOnlyList<LiveAgentDto>> LiveAsync(int? cityValue, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var rows = (await c.QueryAsync<(long UserId, string Name, string Phone, int? CityValue, DateTime StartedAt, decimal? Lat, decimal? Lng, DateTime? At, float? Accuracy)>("""
            SELECT u.Id, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName), u.Phone, u.CityValue, w.StartedAt,
                   p.Latitude, p.Longitude, p.RecordedAt, p.Accuracy
            FROM agents.WorkSessions w JOIN agents.Users u ON u.Id = w.UserId
            OUTER APPLY (SELECT TOP 1 Latitude, Longitude, RecordedAt, Accuracy FROM agents.LocationPoints x WHERE x.SessionId = w.Id ORDER BY x.RecordedAt DESC) p
            WHERE w.EndedAt IS NULL AND (@cityValue IS NULL OR u.CityValue = @cityValue)
            """, new { cityValue })).ToList();
        var result = new List<LiveAgentDto>();
        foreach (var r in rows)
        {
            result.Add(new LiveAgentDto(r.UserId, r.Name, r.Phone, r.CityValue is { } cv ? await _branches.NameAsync(cv, ct) : null, r.StartedAt,
                r.Lat is null ? null : new TrackPointDto(r.Lat.Value, r.Lng!.Value, r.At!.Value, r.Accuracy)));
        }
        return result;
    }

    // ---------------- Helpers ----------------

    private async Task<WorkDayDto> LoadDayAsync(long userId, DateOnly date, SqlConnection c)
    {
        var sessions = (await LoadSessionsAsync(c, date, userId)).Select(s => s.Session).ToList();
        var track = (await c.QueryAsync<TrackPointDto>($"""
            SELECT TOP {MaxTrackPoints} p.Latitude AS Lat, p.Longitude AS Lng, p.RecordedAt AS At, p.Accuracy
            FROM agents.LocationPoints p JOIN agents.WorkSessions s ON s.Id = p.SessionId
            WHERE s.UserId = @userId AND s.WorkDate = @date
            ORDER BY p.RecordedAt
            """, new { userId, date = date.ToDateTime(TimeOnly.MinValue) })).ToList();
        var current = sessions.FirstOrDefault(s => s.EndedAt is null);
        return new WorkDayDto(date.ToString("yyyy-MM-dd"), current is not null, current, sessions, sessions.Sum(s => s.Minutes),
            sessions.Count > 0 ? sessions.Min(s => s.StartedAt) : null,
            current is null && sessions.Count > 0 ? sessions.Max(s => s.EndedAt) : null, track);
    }

    private async Task<List<(long UserId, WorkSessionDto Session)>> LoadSessionsAsync(SqlConnection c, DateOnly date, long? userId)
    {
        var now = _clock.UtcNow;
        var rows = await c.QueryAsync<(long Id, long UserId, DateTime StartedAt, DateTime? EndedAt, string? EndedBy, decimal? StartLat, decimal? StartLng, decimal? EndLat, decimal? EndLng)>("""
            SELECT Id, UserId, StartedAt, EndedAt, EndedBy, StartLat, StartLng, EndLat, EndLng FROM agents.WorkSessions
            WHERE WorkDate = @date AND (@userId IS NULL OR UserId = @userId) ORDER BY StartedAt
            """, new { date = date.ToDateTime(TimeOnly.MinValue), userId });
        return rows.Select(r => (r.UserId, new WorkSessionDto(r.Id, r.StartedAt, r.EndedAt,
            (int)Math.Max(0, ((r.EndedAt ?? now) - r.StartedAt).TotalMinutes), r.EndedBy, r.StartLat, r.StartLng, r.EndLat, r.EndLng))).ToList();
    }

    private static AttendanceRowDto Summarize(long id, string name, string? username, string phone, int? cityValue, string? cityName,
        DateOnly date, List<WorkSessionDto> sessions, TrackPointDto? lastPoint, int points)
    {
        var working = sessions.Any(s => s.EndedAt is null);
        return new AttendanceRowDto(id, name, username, phone, cityValue, cityName, date.ToString("yyyy-MM-dd"),
            sessions.Count > 0 ? sessions.Min(s => s.StartedAt) : null,
            !working && sessions.Count > 0 ? sessions.Max(s => s.EndedAt) : null,
            sessions.Sum(s => s.Minutes), sessions.Count, working, lastPoint, points);
    }

    private static void ValidateCoordinates(decimal? lat, decimal? lng)
    {
        if ((lat is null) != (lng is null) || lat is < -90 or > 90 || lng is < -180 or > 180)
            throw ApiException.Validation(new() { ["latitude"] = "إحداثيات غير صحيحة" });
    }
}
