using Dapper;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Domain.Work;

/// <summary>An agent's result for one day. Status: met | missed | off (did not work) | none (no target).</summary>
public sealed record WorkTargetDayDto(DateOnly Day, int TargetMinutes, int WorkMinutes, int Pins, int Periods, bool Worked, bool Met, string Status,
    bool Final);

public sealed record WorkTargetAgentDayDto(long UserId, string FullName, string? Username, WorkTargetDayDto Result);

public sealed record WorkTargetDayReportDto(
    DateOnly Date, bool IsToday, int DefaultTargetMinutes, int Met, int Missed, int Off, int NoTarget,
    IReadOnlyList<WorkTargetAgentDayDto> Agents, int QuickGapMinutes, int BreakGapMinutes);

public sealed record WorkTargetHistoryDto(
    long UserId, string FullName, int CurrentTargetMinutes, int? OwnTargetMinutes, int MetDays, int MissedDays, int OffDays,
    IReadOnlyList<WorkTargetDayDto> Days);

/// <summary>One pinned shop on the «المحلات المثبّتة» timeline.</summary>
public sealed record TimelinePinDto(ProspectDto Pin, int PeriodIndex, bool StartsPeriod, bool EndsPeriod, int? SecondsOnShop, bool Quick,
    int? BreakSecondsBefore);

public sealed record TimelinePeriodDto(int Index, DateTime StartAt, DateTime EndAt, int Seconds, int Pins, int QuickPins, bool Open);

/// <summary>«المحلات المثبّتة» for one agent and one Iraq day: pins in order, periods, work time and the time target.</summary>
public sealed record PinTimelineDto(
    long UserId, string FullName, DateOnly Date, bool IsToday, int TargetMinutes, int WorkSeconds, int WorkMinutes, bool Met,
    int QuickPins, int QuickGapMinutes, int BreakGapMinutes, DateTime? FirstPinAt, DateTime? LastPinAt,
    IReadOnlyList<TimelinePeriodDto> Periods, IReadOnlyList<TimelinePinDto> Pins);

/// <summary>
/// «الهدف اليومي» (v4.9): minutes of work, counted only inside pin periods (<see cref="PinPeriods"/>), that an agent should
/// reach each day. The dashboard sets a default (AppSettings DailyWorkMinutesTarget) and optionally an agent's own target.
/// Agents never see the target or the periods. Finished days are frozen into agents.WorkTargetDays by <see cref="CloseDaysAsync"/>
/// (WorkDayCloseWorker) with the target in force that day; today is computed live. Days are Iraq days.
/// </summary>
public sealed class WorkTargetService
{
    private const string LastClosedKey = "WorkTargetLastClosedDay";
    private readonly Db _db;
    private readonly AppSettingsService _settings;
    private readonly ProspectService _prospects;

    public WorkTargetService(Db db, AppSettingsService settings, ProspectService prospects)
    {
        _db = db;
        _settings = settings;
        _prospects = prospects;
    }

    private static DateOnly Today => IraqTime.DateOf(DateTime.UtcNow);

    private static string StatusOf(int target, bool met, bool worked) => target <= 0 ? "none" : met ? "met" : worked ? "missed" : "off";

    private static WorkTargetDayDto ToDay(DateOnly day, int target, int workMinutes, int pins, int periods, bool worked, bool final)
    {
        worked = worked || pins > 0;
        var met = target > 0 && workMinutes >= target;
        return new WorkTargetDayDto(day, target, workMinutes, pins, periods, worked, met, StatusOf(target, met, worked), final);
    }

    private sealed record AgentRow(long UserId, string FullName, string? Username, int Target, bool HadSession);

    /// <summary>Every active agent on a day, computed now from their pins.</summary>
    private async Task<List<WorkTargetAgentDayDto>> LiveAsync(DateOnly day, long? onlyUserId, bool final, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var fromUtc = IraqTime.DayStartUtc(day);
        var toUtc = IraqTime.DayStartUtc(day.AddDays(1));
        await using var c = await _db.OpenAsync(ct);
        var agents = (await c.QueryAsync<AgentRow>("""
            SELECT u.Id AS UserId, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName) AS FullName, u.Username,
                   COALESCE(u.DailyWorkMinutesTarget, @defaultTarget) AS Target,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM agents.WorkSessions w WHERE w.UserId = u.Id AND w.WorkDate = @day) THEN 1 ELSE 0 END AS BIT) AS HadSession
            FROM agents.Users u
            WHERE u.IsDeleted = 0 AND u.Username IS NOT NULL AND u.IsSuspended = 0 AND (@onlyUserId IS NULL OR u.Id = @onlyUserId)
            ORDER BY u.FirstName, u.Id
            """, new { defaultTarget = settings.DailyWorkMinutesTarget, day = day.ToDateTime(TimeOnly.MinValue), onlyUserId })).ToList();
        var pins = (await c.QueryAsync<(long UserId, long Id, DateTime CreatedAt)>("""
            SELECT CreatedByUserId AS UserId, Id, CreatedAt FROM agents.Prospects
            WHERE IsDeleted = 0 AND CreatedAt >= @fromUtc AND CreatedAt < @toUtc AND (@onlyUserId IS NULL OR CreatedByUserId = @onlyUserId)
            """, new { fromUtc, toUtc, onlyUserId })).ToLookup(p => p.UserId);
        return agents.Select(a =>
        {
            var r = PinPeriods.Compute(pins[a.UserId].Select(p => new PinPeriods.Pin(p.Id, p.CreatedAt)));
            return new WorkTargetAgentDayDto(a.UserId, a.FullName, a.Username,
                ToDay(day, a.Target, r.WorkMinutes, r.Pins.Count, r.Periods.Count, a.HadSession, final));
        }).ToList();
    }

    /// <summary>Dashboard «المحلات المثبّتة»: one agent's pins on a day as a timeline of periods.</summary>
    public async Task<PinTimelineDto> TimelineAsync(long userId, DateOnly? date, CancellationToken ct)
    {
        var day = date ?? Today;
        var settings = await _settings.GetAsync(ct);
        int target;
        string fullName;
        await using (var c = await _db.OpenAsync(ct))
        {
            var user = await c.QuerySingleOrDefaultAsync<(string FullName, int? Own)>("""
                SELECT CONCAT(FirstName, ' ', FatherName, ' ', GrandfatherName) AS FullName, DailyWorkMinutesTarget AS Own
                FROM agents.Users WHERE Id = @userId AND IsDeleted = 0
                """, new { userId });
            if (user.FullName is null) throw ApiException.NotFound("المسوّق غير موجود");
            fullName = user.FullName;
            // A finished day keeps the target it was judged with.
            var frozen = await c.ExecuteScalarAsync<int?>("SELECT TargetMinutes FROM agents.WorkTargetDays WHERE UserId = @userId AND [Day] = @day",
                new { userId, day = day.ToDateTime(TimeOnly.MinValue) });
            target = frozen ?? user.Own ?? settings.DailyWorkMinutesTarget;
        }

        var pins = await _prospects.ForAgentDayAsync(userId, IraqTime.DayStartUtc(day), IraqTime.DayStartUtc(day.AddDays(1)), ct);
        var r = PinPeriods.Compute(pins.Select(p => new PinPeriods.Pin(p.Id, p.CreatedAt)));
        var byId = pins.ToDictionary(p => p.Id);
        var isToday = day == Today;
        var now = DateTime.UtcNow;
        var periods = r.Periods.Select(p => new TimelinePeriodDto(p.Index, p.StartAt, p.EndAt, p.Seconds, p.Pins, p.QuickPins,
            // Today's last period stays open until 15 minutes pass without a pin.
            Open: isToday && p.Index == r.Periods.Count - 1 && now - p.EndAt < PinPeriods.BreakGap)).ToList();
        return new PinTimelineDto(userId, fullName, day, isToday, target, r.WorkSeconds, r.WorkMinutes, target > 0 && r.WorkMinutes >= target,
            r.QuickPins, (int)PinPeriods.QuickGap.TotalMinutes, (int)PinPeriods.BreakGap.TotalMinutes,
            r.Pins.Count > 0 ? r.Pins[0].At : null, r.Pins.Count > 0 ? r.Pins[^1].At : null, periods,
            r.Pins.Select(x => new TimelinePinDto(byId[x.Id], x.PeriodIndex, x.StartsPeriod, x.EndsPeriod, x.SecondsOnShop, x.Quick,
                x.BreakSecondsBefore)).ToList());
    }

    /// <summary>Dashboard «الأهداف اليومية»: every agent's work time against the target on a day.</summary>
    public async Task<WorkTargetDayReportDto> DayAsync(DateOnly? date, CancellationToken ct)
    {
        var day = date ?? Today;
        var settings = await _settings.GetAsync(ct);
        List<WorkTargetAgentDayDto> agents;
        await using (var c = await _db.OpenAsync(ct))
        {
            agents = (await c.QueryAsync<(long UserId, string FullName, string? Username, int TargetMinutes, int WorkMinutes, int Pins, int Periods,
                    bool Worked, bool Met)>("""
                SELECT d.UserId, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName) AS FullName, u.Username,
                       d.TargetMinutes, d.WorkMinutes, d.Pins, d.Periods, d.Worked, d.Met
                FROM agents.WorkTargetDays d JOIN agents.Users u ON u.Id = d.UserId
                WHERE d.[Day] = @day ORDER BY u.FirstName, u.Id
                """, new { day = day.ToDateTime(TimeOnly.MinValue) }))
                .Select(r => new WorkTargetAgentDayDto(r.UserId, r.FullName, r.Username,
                    new WorkTargetDayDto(day, r.TargetMinutes, r.WorkMinutes, r.Pins, r.Periods, r.Worked, r.Met,
                        StatusOf(r.TargetMinutes, r.Met, r.Worked), true)))
                .ToList();
        }
        // Today, or a past day not frozen yet: compute it now.
        if (agents.Count == 0 && day >= await FirstOpenDayAsync(ct) && day <= Today) agents = await LiveAsync(day, null, false, ct);
        int Count(string s) => agents.Count(a => a.Result.Status == s);
        return new WorkTargetDayReportDto(day, day == Today, settings.DailyWorkMinutesTarget, Count("met"), Count("missed"), Count("off"),
            Count("none"), agents, (int)PinPeriods.QuickGap.TotalMinutes, (int)PinPeriods.BreakGap.TotalMinutes);
    }

    /// <summary>Dashboard: one agent's days (newest first, default the last 90), with today live.</summary>
    public async Task<WorkTargetHistoryDto> HistoryAsync(long userId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var toDay = to ?? Today;
        var fromDay = from ?? toDay.AddDays(-89);
        List<WorkTargetDayDto> days;
        (string FullName, int? Own) user;
        await using (var c = await _db.OpenAsync(ct))
        {
            user = await c.QuerySingleOrDefaultAsync<(string FullName, int? Own)>("""
                SELECT CONCAT(FirstName, ' ', FatherName, ' ', GrandfatherName) AS FullName, DailyWorkMinutesTarget AS Own
                FROM agents.Users WHERE Id = @userId AND IsDeleted = 0
                """, new { userId });
            if (user.FullName is null) throw ApiException.NotFound("المسوّق غير موجود");
            days = (await c.QueryAsync<(DateTime Day, int TargetMinutes, int WorkMinutes, int Pins, int Periods, bool Worked, bool Met)>("""
                SELECT [Day], TargetMinutes, WorkMinutes, Pins, Periods, Worked, Met FROM agents.WorkTargetDays
                WHERE UserId = @userId AND [Day] BETWEEN @from AND @to ORDER BY [Day] DESC
                """, new { userId, from = fromDay.ToDateTime(TimeOnly.MinValue), to = toDay.ToDateTime(TimeOnly.MinValue) }))
                .Select(d => new WorkTargetDayDto(DateOnly.FromDateTime(d.Day), d.TargetMinutes, d.WorkMinutes, d.Pins, d.Periods, d.Worked, d.Met,
                    StatusOf(d.TargetMinutes, d.Met, d.Worked), true))
                .ToList();
        }
        var firstOpen = await FirstOpenDayAsync(ct);
        for (var d = toDay; d >= firstOpen && d >= fromDay; d = d.AddDays(-1))
        {
            if (days.Any(x => x.Day == d) || d > Today) continue;
            var live = (await LiveAsync(d, userId, false, ct)).FirstOrDefault();
            if (live is not null) days.Add(live.Result);
        }
        days = days.OrderByDescending(x => x.Day).ToList();
        var counted = days.Where(x => x.Final || x.Day < Today).ToList(); // today is still going
        return new WorkTargetHistoryDto(userId, user.FullName, user.Own ?? settings.DailyWorkMinutesTarget, user.Own,
            counted.Count(x => x.Status == "met"), counted.Count(x => x.Status == "missed"), counted.Count(x => x.Status == "off"), days);
    }

    /// <summary>Dashboard: an agent's own target in minutes (null = the default).</summary>
    public async Task SetAgentTargetAsync(long userId, int? minutes, AdminActor actor, AuditService audit, CancellationToken ct)
    {
        if (minutes is < 0 or > 1440) throw ApiException.Validation(new() { ["dailyWorkMinutesTarget"] = "الهدف بين 0 و24 ساعة" });
        await using var c = await _db.OpenAsync(ct);
        var updated = await c.ExecuteAsync("""
            UPDATE agents.Users SET DailyWorkMinutesTarget = @minutes, UpdatedAt = SYSUTCDATETIME() WHERE Id = @userId AND IsDeleted = 0
            """, new { userId, minutes });
        if (updated == 0) throw ApiException.NotFound("المسوّق غير موجود");
        await audit.WriteAsync(actor, "agent.target", "User", userId, null, new { dailyWorkMinutesTarget = minutes });
    }

    private async Task<DateOnly> FirstOpenDayAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var last = await c.ExecuteScalarAsync<string?>("SELECT [Value] FROM agents.AppSettings WHERE [Key] = @k", new { k = LastClosedKey });
        return DateOnly.TryParse(last, out var d) ? d.AddDays(1) : Today;
    }

    /// <summary>Freezes every finished day since the last run. The first run only starts the record.</summary>
    public async Task<int> CloseDaysAsync(CancellationToken ct)
    {
        var yesterday = Today.AddDays(-1);
        await using var c = await _db.OpenAsync(ct);
        var last = await c.ExecuteScalarAsync<string?>("SELECT [Value] FROM agents.AppSettings WHERE [Key] = @k", new { k = LastClosedKey });
        if (!DateOnly.TryParse(last, out var lastClosed))
        {
            await SaveLastClosedAsync(c, yesterday);
            return 0;
        }
        var rows = 0;
        for (var day = lastClosed.AddDays(1); day <= yesterday; day = day.AddDays(1))
        {
            foreach (var a in await LiveAsync(day, null, true, ct))
            {
                var d = a.Result;
                if (d.TargetMinutes <= 0 && !d.Worked) continue; // nothing to record
                rows += await c.ExecuteAsync("""
                    IF NOT EXISTS (SELECT 1 FROM agents.WorkTargetDays WHERE UserId = @UserId AND [Day] = @Day)
                        INSERT agents.WorkTargetDays(UserId, [Day], TargetMinutes, WorkMinutes, Pins, Periods, Worked, Met)
                        VALUES (@UserId, @Day, @TargetMinutes, @WorkMinutes, @Pins, @Periods, @Worked, @Met)
                    """, new { a.UserId, Day = day.ToDateTime(TimeOnly.MinValue), d.TargetMinutes, d.WorkMinutes, d.Pins, d.Periods, d.Worked, d.Met });
            }
            await SaveLastClosedAsync(c, day);
        }
        return rows;
    }

    private static Task SaveLastClosedAsync(Microsoft.Data.SqlClient.SqlConnection c, DateOnly day) => c.ExecuteAsync("""
        MERGE agents.AppSettings AS t USING (SELECT @k AS [Key], @v AS [Value]) AS s ON t.[Key] = s.[Key]
        WHEN MATCHED THEN UPDATE SET [Value] = s.[Value], UpdatedAt = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES (s.[Key], s.[Value]);
        """, new { k = LastClosedKey, v = day.ToString("yyyy-MM-dd") });
}
