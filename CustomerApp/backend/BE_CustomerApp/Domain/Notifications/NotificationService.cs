using System.Text.Json;
using System.Threading.Channels;
using Dapper;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Push;

namespace BE_CustomerApp.Domain.Notifications;

public static class NotificationTypes
{
    public const string Order = "Order";
    public const string System = "System";
}

public sealed record NotificationDto(long Id, string Type, string Title, string Body, JsonElement? Data, DateTime? ReadAt, DateTime CreatedAt);

public sealed record NotificationPage(IReadOnlyList<NotificationDto> Items, int Total, int Page, int PageSize, int UnreadCount);

/// <summary>A message from the dashboard to every active agent («إشعار للمسوّقين»).</summary>
public sealed record BroadcastInput(string? Title, string? Body);

public sealed record BroadcastDto(long Id, string Title, string Body, int Recipients, string CreatedBy, DateTime CreatedAt);

/// <summary>A queued push for one user; delivery happens on a background worker so requests never wait on FCM.</summary>
public sealed record PushJob(long UserId, long NotificationId, string Type, string Title, string Body, IReadOnlyDictionary<string, string> Data);

/// <summary>Every notification is stored (inbox works for web + app even without push) and then pushed to the user's devices.</summary>
public sealed class NotificationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Db _db;
    private readonly Channel<PushJob> _queue;
    private readonly AuditService _audit;

    public NotificationService(Db db, Channel<PushJob> queue, AuditService audit)
    {
        _db = db;
        _queue = queue;
        _audit = audit;
    }

    public async Task NotifyAsync(long userId, string type, string title, string body, IReadOnlyDictionary<string, string>? data = null)
    {
        data ??= new Dictionary<string, string>();
        await using var c = await _db.OpenAsync();
        var id = await c.ExecuteScalarAsync<long>("""
            INSERT agents.Notifications(UserId, Type, Title, Body, DataJson) OUTPUT inserted.Id VALUES (@userId, @type, @title, @body, @json)
            """, new { userId, type, title, body, json = JsonSerializer.Serialize(data, Json) });
        await _queue.Writer.WriteAsync(new PushJob(userId, id, type, title, body, data));
    }

    public async Task<NotificationPage> ListAsync(long userId, Paging paging, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync("""
            SELECT COUNT(*) FROM agents.Notifications WHERE UserId = @userId;
            SELECT COUNT(*) FROM agents.Notifications WHERE UserId = @userId AND ReadAt IS NULL;
            SELECT Id, Type, Title, Body, DataJson, ReadAt, CreatedAt FROM agents.Notifications WHERE UserId = @userId
            ORDER BY Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { userId, paging.Offset, paging.PageSize });
        var total = await multi.ReadSingleAsync<int>();
        var unread = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<(long Id, string Type, string Title, string Body, string? DataJson, DateTime? ReadAt, DateTime CreatedAt)>())
            .Select(r => new NotificationDto(r.Id, r.Type, r.Title, r.Body,
                r.DataJson is null ? null : JsonSerializer.Deserialize<JsonElement>(r.DataJson), r.ReadAt, r.CreatedAt))
            .ToList();
        return new NotificationPage(rows, total, paging.Page, paging.PageSize, unread);
    }

    public async Task MarkReadAsync(long userId, long? id, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync(
            "UPDATE agents.Notifications SET ReadAt = SYSUTCDATETIME() WHERE UserId = @userId AND ReadAt IS NULL AND (@id IS NULL OR Id = @id)",
            new { userId, id });
    }

    public async Task RegisterDeviceAsync(long userId, string platform, string token, string? appVersion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512) throw ApiException.BadRequest("validation", "رمز الجهاز غير صالح");
        platform = platform?.ToLowerInvariant() is "ios" or "android" or "web" ? platform.ToLowerInvariant() : "android";
        await using var c = await _db.OpenAsync(ct);
        // A token belongs to whoever logged in last on that device.
        await c.ExecuteAsync("""
            MERGE agents.Devices AS t USING (SELECT @token AS Token) AS s ON t.Token = s.Token
            WHEN MATCHED THEN UPDATE SET UserId = @userId, Platform = @platform, AppVersion = @appVersion, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (UserId, Token, Platform, AppVersion) VALUES (@userId, @token, @platform, @appVersion);
            """, new { userId, token, platform, appVersion });
    }

    public async Task RemoveDeviceAsync(long userId, string token)
    {
        await using var c = await _db.OpenAsync();
        await c.ExecuteAsync("DELETE agents.Devices WHERE UserId = @userId AND Token = @token", new { userId, token });
    }

    /// <summary>Dashboard message to every active agent (in-app notification + push).</summary>
    public async Task<BroadcastDto> BroadcastAsync(BroadcastInput input, AdminActor actor, CancellationToken ct)
    {
        var title = input.Title?.Trim() ?? "";
        var body = input.Body?.Trim() ?? "";
        var e = new Dictionary<string, string>();
        if (title.Length is 0 or > 150) e["title"] = "أدخل عنواناً (150 حرفاً كحد أقصى)";
        if (body.Length is 0 or > 1000) e["body"] = "أدخل نص الإشعار (1000 حرف كحد أقصى)";
        if (e.Count > 0) throw ApiException.Validation(e);

        var type = NotificationTypes.System;
        var data = new Dictionary<string, string>();
        var dataJson = JsonSerializer.Serialize(data, Json);

        await using var c = await _db.OpenAsync(ct);
        var broadcastId = await c.ExecuteScalarAsync<long>("""
            INSERT agents.Broadcasts(Title, Body, Audience, CreatedBy) OUTPUT inserted.Id
            VALUES (@title, @body, 'all', @createdBy)
            """, new { title, body, createdBy = actor.Name });

        var inserted = (await c.QueryAsync<(long Id, long UserId)>("""
            INSERT agents.Notifications(UserId, Type, Title, Body, DataJson, BroadcastId)
            OUTPUT inserted.Id, inserted.UserId
            SELECT u.Id, @type, @title, @body, @dataJson, @broadcastId FROM agents.Users u
            WHERE u.IsDeleted = 0 AND u.IsSuspended = 0 AND u.Username IS NOT NULL
            """, new { type, title, body, dataJson, broadcastId }, commandTimeout: 300)).ToList();

        await c.ExecuteAsync("UPDATE agents.Broadcasts SET Recipients = @n WHERE Id = @broadcastId", new { n = inserted.Count, broadcastId });
        foreach (var (id, userId) in inserted)
        {
            await _queue.Writer.WriteAsync(new PushJob(userId, id, type, title, body, data), ct);
        }
        await _audit.WriteAsync(actor, "notification.broadcast", "Broadcast", broadcastId, null, new { title, body, recipients = inserted.Count }, c);
        return new BroadcastDto(broadcastId, title, body, inserted.Count, actor.Name, DateTime.UtcNow);
    }

    public async Task<PagedResult<BroadcastDto>> ListBroadcastsAsync(Paging paging, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync("""
            SELECT COUNT(*) FROM agents.Broadcasts;
            SELECT Id, Title, Body, Recipients, CreatedBy, CreatedAt FROM agents.Broadcasts
            ORDER BY Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { paging.Offset, paging.PageSize });
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<BroadcastDto>((await multi.ReadAsync<BroadcastDto>()).ToList(), total, paging.Page, paging.PageSize);
    }
}

/// <summary>Drains the push queue and fans out to each of the user's devices; dead tokens are deleted.</summary>
public sealed class PushDispatcher : BackgroundService
{
    private readonly Channel<PushJob> _queue;
    private readonly IPushSender _push;
    private readonly Db _db;
    private readonly ILogger<PushDispatcher> _logger;

    public PushDispatcher(Channel<PushJob> queue, IPushSender push, Db db, ILogger<PushDispatcher> logger)
    {
        _queue = queue;
        _push = push;
        _db = db;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!_push.Enabled) continue;
            try
            {
                await using var c = await _db.OpenAsync(stoppingToken);
                var tokens = await c.QueryAsync<string>("SELECT Token FROM agents.Devices WHERE UserId = @UserId", new { job.UserId });
                var data = new Dictionary<string, string>(job.Data) { ["type"] = job.Type, ["notificationId"] = job.NotificationId.ToString() };
                foreach (var token in tokens)
                {
                    var result = await _push.SendAsync(token, new PushMessage(job.Title, job.Body, data), stoppingToken);
                    if (result == PushResult.InvalidToken)
                        await c.ExecuteAsync("DELETE agents.Devices WHERE Token = @token", new { token });
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Push delivery failed for user {UserId}", job.UserId);
            }
        }
    }
}
