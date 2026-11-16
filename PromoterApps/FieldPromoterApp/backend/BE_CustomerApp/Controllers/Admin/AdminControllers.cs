using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Domain.Notifications;
using BE_CustomerApp.Domain.Orders;
using BE_CustomerApp.Domain.Work;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Controllers.Admin;

/// <summary>Dashboard endpoints. Auth: central gateway JWT of المدير / المراقب (FE_Company localStorage.Token).</summary>
[ApiController]
[Authorize(AuthPolicies.Admin)]
public abstract class AdminControllerBase : ControllerBase
{
    protected AdminActor Actor => AdminActor.From(User);
}

public sealed record ApproveBody(string? Note, string? SalesNotes);
public sealed record DenyBody(string? PublicReason, string? InternalNote);
public sealed record PublicReasonBody(string? PublicReason);

[Route("api/v1/admin/orders")]
public sealed class AdminOrdersController : AdminControllerBase
{
    private readonly OrderService _orders;
    public AdminOrdersController(OrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? cityValue, [FromQuery] string? q,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _orders.AdminListAsync(string.IsNullOrWhiteSpace(status) || status == "all" ? null : status, cityValue, q,
            from is { } f ? Domain.Work.IraqTime.DayStartUtc(f) : null, to is { } t ? Domain.Work.IraqTime.DayStartUtc(t.AddDays(1)) : null,
            Paging.From(page, pageSize), ct));

    /// <summary>Tab badges: { "New": 3, "UnderReview": 1, ..., "all": 40 }.</summary>
    [HttpGet("counts")]
    public async Task<IActionResult> Counts([FromQuery] int? cityValue, CancellationToken ct) => Ok(await _orders.AdminCountsAsync(cityValue, ct));

    /// <summary>Opening a New order moves it to UnderReview (the customer sees «قيد المعالجة»).</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _orders.AdminGetAsync(id, Actor, ct));

    [HttpPost("{id:long}/review")]
    public async Task<IActionResult> Review(long id, CancellationToken ct)
    {
        await _orders.MarkUnderReviewAsync(id, Actor, ct);
        return Ok(await _orders.AdminGetAsync(id, null, ct));
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, ApproveBody body, CancellationToken ct) =>
        Ok(await _orders.ApproveAsync(id, body.Note, body.SalesNotes, Actor, ct));

    [HttpPost("{id:long}/deny")]
    public async Task<IActionResult> Deny(long id, DenyBody body, CancellationToken ct) =>
        Ok(await _orders.DenyAsync(id, body.PublicReason, body.InternalNote, Actor, ct));

    [HttpPost("{id:long}/public-reason")]
    public async Task<IActionResult> PublicReason(long id, PublicReasonBody body, CancellationToken ct) =>
        Ok(await _orders.SetPublicReasonAsync(id, body.PublicReason, Actor, ct));

    [HttpPost("{id:long}/sync")]
    public async Task<IActionResult> Sync(long id, CancellationToken ct)
    {
        await _orders.SyncAsync([id], ct);
        await _orders.SyncSalesProgressAsync([id], ct);
        return Ok(await _orders.AdminGetAsync(id, null, ct));
    }
}

public sealed record PasswordBody(string? Password);

/// <summary>Field agents' accounts (username + password). The app has no sign-up.</summary>
[Route("api/v1/admin/agents")]
public sealed class AdminAgentsController : AdminControllerBase
{
    private readonly AdminAgentsService _agents;
    private readonly WorkTargetService _targets;
    private readonly AuditService _audit;

    public AdminAgentsController(AdminAgentsService agents, WorkTargetService targets, AuditService audit)
    {
        _agents = agents;
        _targets = targets;
        _audit = audit;
    }

    /// <summary>«الأهداف اليومية»: every agent's work time (pin periods) against the time target on a day (default today, Iraq time).</summary>
    [HttpGet("~/api/v1/admin/targets")]
    public async Task<IActionResult> TargetsDay([FromQuery] DateOnly? date, CancellationToken ct) => Ok(await _targets.DayAsync(date, ct));

    /// <summary>An agent's days: met / missed / did not work (default the last 90 days).</summary>
    [HttpGet("{id:long}/targets")]
    public async Task<IActionResult> TargetHistory(long id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _targets.HistoryAsync(id, from, to, ct));

    /// <summary>An agent's own daily target in minutes; null = the default from «إعدادات التطبيق».</summary>
    [HttpPut("{id:long}/target")]
    public async Task<IActionResult> SetTarget(long id, TargetBody body, CancellationToken ct)
    {
        await _targets.SetAgentTargetAsync(id, body.DailyWorkMinutesTarget, Actor, _audit, ct);
        return Ok(await _targets.HistoryAsync(id, null, null, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? status, [FromQuery] int? cityValue, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _agents.SearchAsync(q, status, cityValue, Paging.From(page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _agents.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(AgentInput body, CancellationToken ct) => Ok(await _agents.GetAsync(await _agents.CreateAsync(body, Actor, ct), ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, AgentInput body, CancellationToken ct)
    {
        await _agents.UpdateAsync(id, body, Actor, ct);
        return Ok(await _agents.GetAsync(id, ct));
    }

    [HttpPost("{id:long}/password")]
    public async Task<IActionResult> Password(long id, PasswordBody body, CancellationToken ct)
    {
        await _agents.SetPasswordAsync(id, body.Password, Actor, ct);
        return NoContent();
    }

    /// <summary>Suspending signs the agent out and blocks sign-in.</summary>
    [HttpPost("{id:long}/suspend")]
    public async Task<IActionResult> Suspend(long id, SuspendBody body, CancellationToken ct)
    {
        await _agents.SetSuspendedAsync(id, body.Suspended, body.Reason, Actor, ct);
        return Ok(await _agents.GetAsync(id, ct));
    }
}

/// <summary>Attendance and live location of agents («مراقبة الدوام»).</summary>
[Route("api/v1/admin/attendance")]
public sealed class AdminAttendanceController : AdminControllerBase
{
    private readonly Domain.Work.WorkService _work;
    private readonly Domain.Work.WorkAlertService _alerts;
    private readonly IClock _clock;

    public AdminAttendanceController(Domain.Work.WorkService work, Domain.Work.WorkAlertService alerts, IClock clock)
    {
        _work = work;
        _alerts = alerts;
        _clock = clock;
    }

    /// <summary>Alerts of a day (default today): an agent stayed in one place for an hour. open=true: still there.</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts([FromQuery] DateOnly? date, [FromQuery] bool? open, [FromQuery] int? cityValue, CancellationToken ct) =>
        Ok(await _alerts.ListAsync(date, open == true, cityValue, ct));

    /// <summary>Unacknowledged alerts of the last 24 h, for the dashboard bell (poll every minute).</summary>
    [HttpGet("alerts/unseen")]
    public async Task<IActionResult> UnseenAlerts(CancellationToken ct) => Ok(await _alerts.UnseenAsync(ct));

    [HttpPost("alerts/{id:long}/seen")]
    public async Task<IActionResult> SeenAlert(long id, CancellationToken ct)
    {
        await _alerts.MarkSeenAsync(id, AdminActor.From(User), ct);
        return NoContent();
    }

    [HttpPost("alerts/seen-all")]
    public async Task<IActionResult> SeenAllAlerts(CancellationToken ct)
    {
        await _alerts.MarkSeenAsync(null, AdminActor.From(User), ct);
        return NoContent();
    }

    private DateOnly Today => Domain.Work.IraqTime.DateOf(_clock.UtcNow);

    /// <summary>Every agent on a day (default today): start, finish, total minutes, working now, last position.</summary>
    [HttpGet]
    public async Task<IActionResult> Day([FromQuery] DateOnly? date, [FromQuery] int? cityValue, CancellationToken ct) =>
        Ok(await _work.AttendanceAsync(date ?? Today, cityValue, ct));

    /// <summary>Agents working right now with their latest point (poll every ~30 s for the live map).</summary>
    [HttpGet("live")]
    public async Task<IActionResult> Live([FromQuery] int? cityValue, CancellationToken ct) => Ok(await _work.LiveAsync(cityValue, ct));

    /// <summary>One agent's day: sessions and the full trail.</summary>
    [HttpGet("{userId:long}/day")]
    public async Task<IActionResult> AgentDay(long userId, [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await _work.AgentDayAsync(userId, date ?? Today, ct));

    /// <summary>One agent's worked days in a range (default: last 30 days).</summary>
    [HttpGet("{userId:long}/history")]
    public async Task<IActionResult> History(long userId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await _work.AgentHistoryAsync(userId, from ?? Today.AddDays(-29), to ?? Today, ct));
}

/// <summary>Shops pinned by agents as potential customers.</summary>
[Route("api/v1/admin/prospects")]
public sealed class AdminProspectsController : AdminControllerBase
{
    private readonly Domain.Work.ProspectService _prospects;
    private readonly WorkTargetService _targets;

    public AdminProspectsController(Domain.Work.ProspectService prospects, WorkTargetService targets)
    {
        _prospects = prospects;
        _targets = targets;
    }

    /// <summary>«المحلات المثبّتة»: one agent's pins on an Iraq day (default today) with periods, time per shop and work time.</summary>
    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline([FromQuery] long userId, [FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await _targets.TimelineAsync(userId, date, ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? cityValue, [FromQuery] long? userId, [FromQuery] int? minInterest, [FromQuery] string? q,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _prospects.AdminListAsync(cityValue, userId, minInterest, q,
            from is { } f ? Domain.Work.IraqTime.DayStartUtc(f) : null, to is { } t ? Domain.Work.IraqTime.DayStartUtc(t.AddDays(1)) : null,
            Paging.From(page, pageSize, 100), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _prospects.GetAsync(id, 0, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _prospects.AdminDeleteAsync(id, AdminActor.From(User), ct);
        return NoContent();
    }
}

public sealed record SuspendBody(bool Suspended, string? Reason);

[Route("api/v1/admin")]
public sealed class AdminSystemController : AdminControllerBase
{
    private readonly AppSettingsService _settings;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;
    private readonly ReportsService _reports;

    public AdminSystemController(AppSettingsService settings, AuditService audit, NotificationService notifications, ReportsService reports)
    {
        _settings = settings;
        _audit = audit;
        _notifications = notifications;
        _reports = reports;
    }

    [HttpGet("settings")] public async Task<IActionResult> GetSettings(CancellationToken ct) => Ok(await _settings.GetAsync(ct));

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(AppSettings body, CancellationToken ct)
    {
        var before = await _settings.GetAsync(ct);
        await _settings.SaveAsync(body, ct);
        var after = await _settings.GetAsync(ct);
        await _audit.WriteAsync(Actor, "settings.update", "Settings", null, before, after);
        return Ok(after);
    }

    [HttpPost("notifications/broadcast")]
    public async Task<IActionResult> Broadcast(BroadcastInput body, CancellationToken ct) => Ok(await _notifications.BroadcastAsync(body, Actor, ct));

    [HttpGet("notifications/broadcasts")]
    public async Task<IActionResult> Broadcasts([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _notifications.ListBroadcastsAsync(Paging.From(page, pageSize), ct));

    /// <summary>«نظرة عامة»: totals, per-agent numbers and the most sold items. Default range: this month.</summary>
    [HttpGet("reports/overview")]
    public async Task<IActionResult> Overview([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Ok(await _reports.OverviewAsync(from, to, ct));

    [HttpGet("reports/export")]
    public async Task<IActionResult> Export([FromQuery] string type, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (fileName, content) = await _reports.ExportAsync(type, from, to, ct);
        await _audit.WriteAsync(Actor, "report.export", "Report", type, null, new { from, to });
        return File(content, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] string? entity, [FromQuery] string? actor, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _audit.SearchAsync(string.IsNullOrWhiteSpace(entity) ? null : entity, string.IsNullOrWhiteSpace(actor) ? null : actor,
            from is { } f ? Domain.Work.IraqTime.DayStartUtc(f) : null, to is { } t ? Domain.Work.IraqTime.DayStartUtc(t.AddDays(1)) : null,
            Paging.From(page, pageSize, 50), ct));
}

public sealed record TargetBody(int? DailyWorkMinutesTarget);
