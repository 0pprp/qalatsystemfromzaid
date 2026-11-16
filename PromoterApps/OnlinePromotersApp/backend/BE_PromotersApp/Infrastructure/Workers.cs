using Dapper;
using BE_PromotersApp.Domain.Orders;

namespace BE_PromotersApp.Infrastructure;

/// <summary>Base for simple periodic jobs.</summary>
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

/// <summary>Sends waiting orders to the company system and pulls status changes (a sale credits the wallets).</summary>
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
    }
}

public sealed class CleanupWorker : PeriodicWorker
{
    public CleanupWorker(IServiceScopeFactory scopes, ILogger<CleanupWorker> logger) : base(scopes, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var c = await services.GetRequiredService<Db>().OpenAsync(ct);
        await c.ExecuteAsync("DELETE promoters.RefreshTokens WHERE ExpiresAt < DATEADD(DAY, -7, SYSUTCDATETIME()) OR RevokedAt < DATEADD(DAY, -30, SYSUTCDATETIME())");
    }
}

/// <summary>Development only (Demo:Seed=true): a demo promoter (promoter1 / 123456) and two products.</summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(Db db, ILogger logger)
    {
        await using var c = await db.OpenAsync();
        if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM promoters.Users") > 0) return;
        await c.ExecuteAsync("""
            INSERT promoters.Users(Username, PasswordHash, Phone, IsFull, CreatedByAdmin, PasswordChangedAt)
            VALUES ('promoter1', @hash, '07700000001', 1, N'Demo seed', SYSUTCDATETIME());
            INSERT promoters.Products(Name, Price, Commission, SortOrder) VALUES (N'ايفون 15', 1450000, 25000, 1), (N'كرسي مساج', 900000, 40000, 2);
            """, new { hash = Auth.PasswordHasher.Hash("123456") });
        logger.LogInformation("Demo promoter seeded: promoter1 / 123456");
    }
}
