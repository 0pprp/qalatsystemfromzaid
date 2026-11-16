using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Notifications;
using BE_CustomerApp.Domain.Orders;
using BE_CustomerApp.Domain.Users;
using BE_CustomerApp.Domain.Work;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Controllers;

public sealed record LoginBody(string? Username, string? Password, DeviceInput? Device);
public sealed record RefreshBody(string? RefreshToken, string? PushToken);

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly TokenService _tokens;
    private readonly NotificationService _notifications;

    public AuthController(AuthService auth, TokenService tokens, NotificationService notifications)
    {
        _auth = auth;
        _tokens = tokens;
        _notifications = notifications;
    }

    /// <summary>Username + password set from the dashboard. There is no sign-up.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginBody body, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(body.Username, body.Password, body.Device, ct));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshBody body, CancellationToken ct) => Ok(await _auth.RefreshAsync(body.RefreshToken, ct));

    [Authorize(AuthPolicies.Customer)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshBody body)
    {
        var userId = User.CustomerId();
        if (!string.IsNullOrWhiteSpace(body.RefreshToken)) await _tokens.RevokeAsync(body.RefreshToken, userId);
        if (!string.IsNullOrWhiteSpace(body.PushToken)) await _notifications.RemoveDeviceAsync(userId, body.PushToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/me")]
[Authorize(AuthPolicies.Customer)]
public sealed class MeController : ControllerBase
{
    private readonly UserService _users;
    private readonly NotificationService _notifications;
    private readonly OrderService _orders;

    public MeController(UserService users, NotificationService notifications, OrderService orders)
    {
        _users = users;
        _notifications = notifications;
        _orders = orders;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _users.GetMeAsync(User.CustomerId(), ct));

    /// <summary>The app's home page: today's and this month's numbers, open orders and the latest orders.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        // The daily target is work time now and agents must not see it: dailyPinTarget stays 0, so 1.4.7–1.4.8 hide their target card.
        return Ok(await _orders.OverviewForAgentAsync(User.CustomerId(), ct));
    }

    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(DeviceInput body, CancellationToken ct)
    {
        await _notifications.RegisterDeviceAsync(User.CustomerId(), body.Platform ?? "android", body.PushToken ?? "", body.AppVersion, ct);
        return NoContent();
    }

    [HttpDelete("devices/{token}")]
    public async Task<IActionResult> RemoveDevice(string token)
    {
        await _notifications.RemoveDeviceAsync(User.CustomerId(), token);
        return NoContent();
    }
}

public sealed record CancelBody(string? Reason);

[ApiController]
[Route("api/v1/orders")]
[Authorize(AuthPolicies.Customer)]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderService _orders;
    private readonly UserService _users;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public OrdersController(OrderService orders, UserService users, IConfiguration config, IWebHostEnvironment env)
    {
        _orders = orders;
        _users = users;
        _config = config;
        _env = env;
    }

    /// <summary>Step 1 of a sale order: upload the shop photo, then send its URL as shopPhotoUrl.</summary>
    [HttpPost("shop-photo")]
    [RequestSizeLimit(9_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 9_000_000)]
    public async Task<IActionResult> ShopPhoto(IFormFile file, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        var url = await _orders.UploadShopPhotoAsync(user.Id, file, MediaStorage.Root(_config, _env), ct);
        return Ok(new { url });
    }

    [HttpPost]
    [EnableRateLimiting("orders")]
    public async Task<IActionResult> Create(CreateOrderInput body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        var order = await _orders.CreateAsync(user, body, idempotencyKey, ct);
        return StatusCode(201, order);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _orders.ListForCustomerAsync(User.CustomerId(), string.IsNullOrWhiteSpace(status) ? null : status, Paging.From(page, pageSize), ct));

    [HttpGet("counts")]
    public async Task<IActionResult> Counts(CancellationToken ct) => Ok(await _orders.CountsForCustomerAsync(User.CustomerId(), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _orders.GetForCustomerAsync(User.CustomerId(), id, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancelBody body, CancellationToken ct) =>
        Ok(await _orders.CancelByCustomerAsync(User.CustomerId(), id, body.Reason, ct));
}

public sealed record WorkPointBody(decimal? Latitude, decimal? Longitude);
public sealed record LocationBatchBody(IReadOnlyList<LocationInput>? Points);

/// <summary>«الدوام»: start/finish the working day and send location points while working.</summary>
[ApiController]
[Route("api/v1/work")]
[Authorize(AuthPolicies.Customer)]
public sealed class WorkController : ControllerBase
{
    private readonly WorkService _work;
    private readonly UserService _users;

    public WorkController(WorkService work, UserService users)
    {
        _work = work;
        _users = users;
    }

    /// <summary>Today's sessions, totals and trail (or another day with ?date=yyyy-MM-dd).</summary>
    [HttpGet("today")]
    public async Task<IActionResult> Today([FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok(await _work.GetDayAsync(User.CustomerId(), date, ct));

    [HttpPost("start")]
    public async Task<IActionResult> Start(WorkPointBody body, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return Ok(await _work.StartAsync(user.Id, body.Latitude, body.Longitude, ct));
    }

    [HttpPost("end")]
    public async Task<IActionResult> End(WorkPointBody body, CancellationToken ct) =>
        Ok(await _work.EndAsync(User.CustomerId(), body.Latitude, body.Longitude, ct));

    [HttpPost("locations")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> Locations(LocationBatchBody body, CancellationToken ct) =>
        Ok(await _work.AddPointsAsync(User.CustomerId(), body.Points, ct));

    /// <summary>Key for the background tracking service of this working day (see <see cref="Track"/>).</summary>
    [HttpPost("tracking-key")]
    public async Task<IActionResult> TrackingKey(CancellationToken ct) =>
        Ok(new { key = await _work.IssueTrackingKeyAsync(User.CustomerId(), ct) });

    /// <summary>
    /// Location points from the phone's background service, which runs while the app is closed. Authenticated with the
    /// day's tracking key (header X-Tracking-Key) instead of the sign-in token. isWorking=false: the day ended, stop.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("track")]
    [RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> Track(LocationBatchBody body, [FromHeader(Name = "X-Tracking-Key")] string? key, CancellationToken ct) =>
        Ok(await _work.AddPointsByKeyAsync(key, body.Points, ct));
}

/// <summary>«المحلات المحتملة»: pins for shops that may buy later, shared between all agents.</summary>
[ApiController]
[Route("api/v1/prospects")]
[Authorize(AuthPolicies.Customer)]
public sealed class ProspectsController : ControllerBase
{
    private readonly ProspectService _prospects;
    private readonly UserService _users;

    public ProspectsController(ProspectService prospects, UserService users)
    {
        _prospects = prospects;
        _users = users;
    }

    /// <summary>Every agent's pins inside the visible map area.</summary>
    [HttpGet]
    public async Task<IActionResult> InArea([FromQuery] decimal minLat, [FromQuery] decimal minLng, [FromQuery] decimal maxLat,
        [FromQuery] decimal maxLng, CancellationToken ct) =>
        Ok(await _prospects.InAreaAsync(User.CustomerId(), minLat, minLng, maxLat, maxLng, ct));

    /// <summary>The agent's own pins as a list (newest first).</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int? interest, [FromQuery] string? q, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await _prospects.MineAsync(User.CustomerId(), interest, q, from, to, Paging.From(page, pageSize, 50), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _prospects.GetAsync(id, User.CustomerId(), ct));

    [HttpPost]
    public async Task<IActionResult> Create(ProspectInput body, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return Ok(await _prospects.CreateAsync(user, body, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, ProspectInput body, CancellationToken ct) =>
        Ok(await _prospects.UpdateAsync(User.CustomerId(), id, body, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _prospects.DeleteOwnAsync(User.CustomerId(), id, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/notifications")]
[Authorize(AuthPolicies.Customer)]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationService _notifications;

    public NotificationsController(NotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _notifications.ListAsync(User.CustomerId(), Paging.From(page, pageSize), ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> Read(long id, CancellationToken ct)
    {
        await _notifications.MarkReadAsync(User.CustomerId(), id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await _notifications.MarkReadAsync(User.CustomerId(), null, ct);
        return NoContent();
    }
}
