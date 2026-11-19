using Dapper;
using Microsoft.Data.SqlClient;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Domain.Notifications;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Domain.Work;

/// <summary>For the app's «إنهاء الدوام» warning. The target itself is never sent to the agent.</summary>
public sealed record TargetStatusDto(bool HasTarget, bool Reached, int WorkMinutes, string WorkedText);

/// <summary>The day the agent reached the target on: shops pinned, sale orders sent and sales made that (Iraq) day.</summary>
public sealed record TargetSummaryDto(long Id, string Date, bool IsToday, bool IsYesterday, int Stores, int Orders, int Sales);

/// <summary>
/// What an agent is told about the daily work-time target (<see cref="WorkTargetService"/>, time inside pin periods):
/// <list type="bullet">
/// <item>reaching it: a notification, and a day summary the app shows once when it is next opened (no hours in either);</item>
/// <item><see cref="IdleAfter"/> without a pin while it is not reached: a notification with the time worked so far;</item>
/// <item>ending the day by hand before reaching it: the app warns first (<see cref="StatusAsync"/>), and the dashboard
/// gets a <see cref="WorkAlertService.ClosedEarly"/> alert.</item>
/// </list>
/// Agents without a target (0) get none of this.
/// </summary>
public sealed class DailyTargetService
{
    public const string Reached = "Reached";
    public const string Idle = "Idle";

    public static readonly TimeSpan IdleAfter = TimeSpan.FromHours(2);

    /// <summary>An idle notice is only sent this long after it became due, so a restart never sends hours-old ones.</summary>
    private static readonly TimeSpan IdleWindow = TimeSpan.FromMinutes(30);

    /// <summary>A summary nobody opened is dropped after this many days.</summary>
    private const int SummaryDays = 7;

    private readonly Db _db;
    private readonly IClock _clock;
    private readonly AppSettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly ILogger<DailyTargetService> _logger;

    public DailyTargetService(Db db, IClock clock, AppSettingsService settings, NotificationService notifications,
        ILogger<DailyTargetService> logger)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
        _notifications = notifications;
        _logger = logger;
    }

    private sealed record Progress(long UserId, int Target, int WorkMinutes, int Pins, DateTime? LastPinAt)
    {
        public bool HasTarget => Target > 0;
        public bool IsReached => Target > 0 && WorkMinutes >= Target;
    }

    /// <summary>Today's work time of the active agents who have a target (one agent, or everyone who pinned today).</summary>
    private async Task<List<Progress>> ProgressAsync(SqlConnection c, DateOnly day, long? onlyUserId, CancellationToken ct)
    {
        var settings = await _settings.GetAsync(ct);
        var fromUtc = IraqTime.DayStartUtc(day);
        var toUtc = IraqTime.DayStartUtc(day.AddDays(1));
        var pins = (await c.QueryAsync<(long UserId, long Id, DateTime CreatedAt)>("""
            SELECT CreatedByUserId AS UserId, Id, CreatedAt FROM agents.Prospects
            WHERE IsDeleted = 0 AND CreatedAt >= @fromUtc AND CreatedAt < @toUtc AND (@onlyUserId IS NULL OR CreatedByUserId = @onlyUserId)
            """, new { fromUtc, toUtc, onlyUserId })).ToLookup(p => p.UserId);
        var ids = onlyUserId is { } one ? new List<long> { one } : pins.Select(g => g.Key).ToList();
        if (ids.Count == 0) return [];
        var targets = await c.QueryAsync<(long UserId, int Target)>("""
            SELECT Id AS UserId, COALESCE(DailyWorkMinutesTarget, @defaultTarget) AS Target FROM agents.Users
            WHERE Id IN @ids AND IsDeleted = 0 AND IsSuspended = 0 AND Username IS NOT NULL
            """, new { ids, defaultTarget = settings.DailyWorkMinutesTarget });
        return targets.Select(t =>
        {
            var r = PinPeriods.Compute(pins[t.UserId].Select(p => new PinPeriods.Pin(p.Id, p.CreatedAt)));
            return new Progress(t.UserId, t.Target, r.WorkMinutes, r.Pins.Count, r.Pins.Count > 0 ? r.Pins[^1].At : null);
        }).ToList();
    }

    // ---------------- Agent ----------------

    /// <summary>Asked by the app before «إنهاء الدوام»: has today's target been reached, and the time worked so far.</summary>
    public async Task<TargetStatusDto> StatusAsync(long userId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var p = (await ProgressAsync(c, IraqTime.DateOf(_clock.UtcNow), userId, ct)).FirstOrDefault();
        return p is null
            ? new TargetStatusDto(false, false, 0, WorkedText(0))
            : new TargetStatusDto(p.HasTarget, p.IsReached, p.WorkMinutes, WorkedText(p.WorkMinutes));
    }

    /// <summary>The newest «حققت هدفك اليومي» summary the agent has not closed yet (null when there is none).</summary>
    public async Task<TargetSummaryDto?> PendingSummaryAsync(long userId, CancellationToken ct)
    {
        var today = IraqTime.DateOf(_clock.UtcNow);
        await using var c = await _db.OpenAsync(ct);
        var notice = await c.QuerySingleOrDefaultAsync<(long Id, DateTime Day)>("""
            SELECT TOP 1 Id, [Day] FROM agents.WorkTargetNotices
            WHERE UserId = @userId AND Kind = @kind AND SeenAt IS NULL AND [Day] >= @since ORDER BY [Day] DESC
            """, new { userId, kind = Reached, since = today.AddDays(-SummaryDays).ToDateTime(TimeOnly.MinValue) });
        if (notice.Id == 0) return null;

        var day = DateOnly.FromDateTime(notice.Day);
        var fromUtc = IraqTime.DayStartUtc(day);
        var toUtc = IraqTime.DayStartUtc(day.AddDays(1));
        var n = await c.QuerySingleAsync<(int Stores, int Orders, int Sales)>("""
            SELECT
                (SELECT COUNT(*) FROM agents.Prospects WHERE CreatedByUserId = @userId AND IsDeleted = 0 AND CreatedAt >= @fromUtc AND CreatedAt < @toUtc),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND CreatedAt >= @fromUtc AND CreatedAt < @toUtc),
                (SELECT COUNT(*) FROM agents.Orders WHERE SubmittedByUserId = @userId AND Status IN ('Sold','Finished') AND SoldAt >= @fromUtc AND SoldAt < @toUtc)
            """, new { userId, fromUtc, toUtc });
        return new TargetSummaryDto(notice.Id, day.ToString("yyyy-MM-dd"), day == today, day == today.AddDays(-1), n.Stores, n.Orders, n.Sales);
    }

    /// <summary>The agent closed the summary: it (and any older one) is not shown again.</summary>
    public async Task MarkSummarySeenAsync(long userId, long id, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync("""
            UPDATE n SET SeenAt = SYSUTCDATETIME()
            FROM agents.WorkTargetNotices n
            WHERE n.UserId = @userId AND n.Kind = @kind AND n.SeenAt IS NULL
              AND n.[Day] <= (SELECT [Day] FROM agents.WorkTargetNotices WHERE Id = @id AND UserId = @userId)
            """, new { userId, id, kind = Reached });
    }

    // ---------------- Detection ----------------

    /// <summary>Right after an agent pins a shop: the pin may have completed the target.</summary>
    public async Task CheckAfterPinAsync(long userId, CancellationToken ct)
    {
        try
        {
            var now = _clock.UtcNow;
            var day = IraqTime.DateOf(now);
            await using var c = await _db.OpenAsync(ct);
            foreach (var p in await ProgressAsync(c, day, userId, ct))
                if (p.IsReached) await TellReachedAsync(c, p, day);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Daily target check failed for user {UserId}", userId);
        }
    }

    /// <summary>One pass over everyone who pinned today (worker). Returns the number of notifications sent.</summary>
    public async Task<int> DetectAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var day = IraqTime.DateOf(now);
        await using var c = await _db.OpenAsync(ct);
        var sent = 0;
        foreach (var p in await ProgressAsync(c, day, null, ct))
        {
            if (!p.HasTarget || p.LastPinAt is not { } lastPin) continue;
            if (p.IsReached)
            {
                if (await TellReachedAsync(c, p, day)) sent++;
                continue;
            }
            var idle = now - lastPin;
            if (idle < IdleAfter || idle >= IdleAfter + IdleWindow) continue;
            // The agent who ended the day by hand after that pin was already warned by the app.
            var endedByHand = await c.ExecuteScalarAsync<int>("""
                SELECT COUNT(*) FROM agents.WorkSessions WHERE UserId = @UserId AND EndedBy = 'user' AND EndedAt >= @lastPin
                """, new { p.UserId, lastPin });
            if (endedByHand > 0) continue;
            if (!await InsertNoticeAsync(c, p, day, Idle, lastPin)) continue;
            await _notifications.NotifyAsync(p.UserId, NotificationTypes.System, "لم تحقق هدفك اليومي بعد",
                $"عملت اليوم {WorkedText(p.WorkMinutes)} فقط ولم تصل إلى هدفك اليومي. تابع العمل لتحقيق الهدف.",
                new Dictionary<string, string> { ["kind"] = "target_idle" });
            sent++;
        }
        return sent;
    }

    private async Task<bool> TellReachedAsync(SqlConnection c, Progress p, DateOnly day)
    {
        if (!await InsertNoticeAsync(c, p, day, Reached, IraqTime.DayStartUtc(day))) return false;
        await _notifications.NotifyAsync(p.UserId, NotificationTypes.System, "أحسنت! حققت هدفك اليومي",
            "لقد أكملت هدف العمل لهذا اليوم. افتح التطبيق لمشاهدة ملخص يومك.",
            new Dictionary<string, string> { ["kind"] = "target_reached", ["date"] = day.ToString("yyyy-MM-dd") });
        return true;
    }

    /// <summary>False when the agent was already told (the unique key makes two racing callers agree).</summary>
    private static async Task<bool> InsertNoticeAsync(SqlConnection c, Progress p, DateOnly day, string kind, DateTime refAt)
    {
        try
        {
            return await c.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM agents.WorkTargetNotices WITH (UPDLOCK, HOLDLOCK)
                               WHERE UserId = @UserId AND [Day] = @day AND Kind = @kind AND RefAt = @refAt)
                    INSERT agents.WorkTargetNotices(UserId, [Day], Kind, RefAt, WorkMinutes, TargetMinutes)
                    VALUES (@UserId, @day, @kind, @refAt, @WorkMinutes, @Target)
                """, new { p.UserId, day = day.ToDateTime(TimeOnly.MinValue), kind, refAt, p.WorkMinutes, p.Target }) > 0;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return false;
        }
    }

    /// <summary>
    /// The agent ended the working day by hand. Before the target: the dashboard gets a «أنهى الدوام قبل الهدف» alert.
    /// Never fails the caller.
    /// </summary>
    public async Task OnDayEndedByAgentAsync(long userId, long sessionId, decimal? lat, decimal? lng, DateTime endedAt, CancellationToken ct)
    {
        try
        {
            await using var c = await _db.OpenAsync(ct);
            var p = (await ProgressAsync(c, IraqTime.DateOf(endedAt), userId, ct)).FirstOrDefault();
            if (p is null || !p.HasTarget || p.IsReached) return;
            await c.ExecuteAsync("""
                INSERT agents.WorkAlerts(UserId, SessionId, Type, Latitude, Longitude, SinceAt, LastSeenAt, ResolvedAt, WorkMinutes, TargetMinutes)
                VALUES (@userId, @sessionId, @type, @lat, @lng, @endedAt, @endedAt, @endedAt, @WorkMinutes, @Target)
                """, new { userId, sessionId, type = WorkAlertService.ClosedEarly, lat, lng, endedAt, p.WorkMinutes, p.Target });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Closed-early alert failed for user {UserId}", userId);
        }
    }

    /// <summary>125 → «ساعتين و5 دقائق», 40 → «40 دقيقة», 0 → «0 دقيقة».</summary>
    public static string WorkedText(int minutes)
    {
        minutes = Math.Max(0, minutes);
        var h = minutes / 60;
        var m = minutes % 60;
        static string Count(int n, string one, string two, string few, string many) => n switch
        {
            1 => one,
            2 => two,
            >= 3 and <= 10 => $"{n} {few}",
            _ => $"{n} {many}",
        };
        var hours = Count(h, "ساعة واحدة", "ساعتين", "ساعات", "ساعة");
        var mins = Count(m, "دقيقة واحدة", "دقيقتين", "دقائق", "دقيقة");
        return h == 0 ? mins : m == 0 ? hours : $"{hours} و{mins}";
    }
}
