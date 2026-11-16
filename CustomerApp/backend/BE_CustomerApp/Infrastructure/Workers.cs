using Dapper;
using BE_CustomerApp.Domain.Orders;

namespace BE_CustomerApp.Infrastructure;

/// <summary>Base for simple periodic jobs (same Task.Delay pattern as the company system's hosted services).</summary>
public abstract class PeriodicWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    protected readonly ILogger Logger;

    protected PeriodicWorker(IServiceScopeFactory scopes, ILogger logger)
    {
        _scopes = scopes;
        Logger = logger;
    }

    protected abstract TimeSpan Interval { get; }
    protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(ex, "{Worker} run failed", GetType().Name);
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }
}

/// <summary>Polls the company system for status changes of transferred orders (→ Sold creates commissions).</summary>
public sealed class OrderSyncWorker : PeriodicWorker
{
    private readonly TimeSpan _interval;

    public OrderSyncWorker(IServiceScopeFactory scopes, ILogger<OrderSyncWorker> logger, IConfiguration config) : base(scopes, logger) =>
        _interval = TimeSpan.FromSeconds(Math.Max(30, config.GetValue("Gateway:SyncIntervalSeconds", 120)));

    protected override TimeSpan Interval => _interval;

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var orders = services.GetRequiredService<OrderService>();
        var forwarded = await orders.ForwardPendingAsync(ct);
        if (forwarded > 0) Logger.LogInformation("Forwarded {Count} waiting orders to the company system", forwarded);
        var changed = await orders.SyncAsync(null, ct);
        if (changed > 0) Logger.LogInformation("Order sync: {Count} orders changed status", changed);
        var finished = await orders.SyncSalesProgressAsync(null, ct);
        if (finished > 0) Logger.LogInformation("Sale progress: {Count} sales fully paid", finished);
    }
}

public sealed class CleanupWorker : PeriodicWorker
{
    public CleanupWorker(IServiceScopeFactory scopes, ILogger<CleanupWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var c = await services.GetRequiredService<Db>().OpenAsync(ct);
        await c.ExecuteAsync("""
            DELETE agents.OtpRequests WHERE CreatedAt < DATEADD(DAY, -7, SYSUTCDATETIME());
            DELETE agents.RefreshTokens WHERE ExpiresAt < DATEADD(DAY, -7, SYSUTCDATETIME()) OR RevokedAt < DATEADD(DAY, -30, SYSUTCDATETIME());
            """);
    }
}

/// <summary>Closes «دوام» sessions that were left open past midnight (Iraq time).</summary>
public sealed class WorkDayCloseWorker : PeriodicWorker
{
    public WorkDayCloseWorker(IServiceScopeFactory scopes, ILogger<WorkDayCloseWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var closed = await services.GetRequiredService<Domain.Work.WorkService>().AutoCloseAsync(ct);
        if (closed > 0) Logger.LogInformation("Auto-closed {Count} work sessions from previous days", closed);
        // Freeze yesterday's «الهدف اليومي» (work time) results (once per day).
        var targets = await services.GetRequiredService<Domain.Work.WorkTargetService>().CloseDaysAsync(ct);
        if (targets > 0) Logger.LogInformation("Recorded {Count} daily work-time target results", targets);
    }
}

/// <summary>Raises «بقي في نفس المكان» alerts for the dashboard.</summary>
public sealed class WorkAlertWorker : PeriodicWorker
{
    public WorkAlertWorker(IServiceScopeFactory scopes, ILogger<WorkAlertWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(2);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        var raised = await services.GetRequiredService<Domain.Work.WorkAlertService>().DetectAsync(ct);
        if (raised > 0) Logger.LogInformation("Raised {Count} stationary alerts", raised);
    }
}
