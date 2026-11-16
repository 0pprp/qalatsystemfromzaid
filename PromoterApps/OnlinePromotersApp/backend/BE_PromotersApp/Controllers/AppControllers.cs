using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Admin;
using BE_PromotersApp.Domain.Materials;
using BE_PromotersApp.Domain.Notifications;
using BE_PromotersApp.Domain.Orders;
using BE_PromotersApp.Domain.Products;
using BE_PromotersApp.Domain.Users;
using BE_PromotersApp.Domain.Wallet;
using BE_PromotersApp.Infrastructure;
using BE_PromotersApp.Integrations.Gateway;

namespace BE_PromotersApp.Controllers;

public sealed record LoginBody(string? Username, string? Password, DeviceInput? Device);
public sealed record RefreshBody(string? RefreshToken, string? PushToken = null);
public sealed record BranchDto(int CityValue, string Name);

/// <summary>Endpoints the app calls before sign-in.</summary>
[ApiController]
[Route("api/v1")]
public sealed class PublicController : ControllerBase
{
    private readonly BranchCatalog _branches;
    private readonly AppSettingsService _settings;
    private readonly AppReleaseService _release;

    public PublicController(BranchCatalog branches, AppSettingsService settings, AppReleaseService release)
    {
        _branches = branches;
        _settings = settings;
        _release = release;
    }

    [HttpGet("branches")]
    public async Task<IActionResult> Branches(CancellationToken ct) =>
        Ok((await _branches.GetAllAsync(ct)).Select(b => new BranchDto(b.CityValue, b.Name)));

    /// <summary>The published APK's version is the latest and the minimum (see AppReleaseService).</summary>
    [HttpGet("app/config")]
    public async Task<IActionResult> Config(CancellationToken ct)
    {
        var s = await _settings.GetAsync(ct);
        var release = _release.Current();
        var version = release?.Version ?? "0.0.0";
        return Ok(new { minSupportedVersion = version, latestVersion = version, s.SupportPhone, s.SupportWhatsapp, apkUrl = release?.ApkUrl ?? "", s.MinPayout });
    }
}

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

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginBody body, CancellationToken ct) => Ok(await _auth.LoginAsync(body.Username, body.Password, body.Device, ct));

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshBody body, CancellationToken ct) => Ok(await _auth.RefreshAsync(body.RefreshToken, ct));

    [Authorize(AuthPolicies.Customer)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshBody body)
    {
        if (!string.IsNullOrWhiteSpace(body.RefreshToken)) await _tokens.RevokeAsync(body.RefreshToken, User.CustomerId());
        await _notifications.RemoveDeviceAsync(User.CustomerId(), body.PushToken);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/me")]
[Authorize(AuthPolicies.Customer)]
public sealed class MeController : ControllerBase
{
    private readonly UserService _users;
    private readonly OrderService _orders;
    private readonly NotificationService _notifications;

    public MeController(UserService users, OrderService orders, NotificationService notifications)
    {
        _users = users;
        _orders = orders;
        _notifications = notifications;
    }

    /// <summary>The phone's Firebase token, sent after sign-in and whenever Firebase renews it.</summary>
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice(DeviceInput body, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        await _notifications.RegisterDeviceAsync(user.Id, body.Platform, body.PushToken, body.AppVersion, ct);
        return NoContent();
    }

    [HttpDelete("devices/{token}")]
    public async Task<IActionResult> RemoveDevice(string token)
    {
        await _notifications.RemoveDeviceAsync(User.CustomerId(), token);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _users.GetMeAsync(User.CustomerId(), ct));

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return Ok(await _orders.OverviewAsync(user.Id, ct));
    }

    /// <summary>«فريقي»: the accounts this promoter created and what they earned from each.</summary>
    [HttpGet("team")]
    public async Task<IActionResult> Team(CancellationToken ct) => Ok(await _users.GetTeamAsync(User.CustomerId(), ct));

    [HttpPost("team")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> CreateMember(CreateMemberInput body, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return StatusCode(201, await _users.CreateMemberAsync(user.Id, body, ct));
    }

    [HttpPut("team/{id:long}")]
    public async Task<IActionResult> UpdateMember(long id, UpdateMemberInput body, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return Ok(await _users.UpdateMemberAsync(user.Id, id, body, ct));
    }
}

[ApiController]
[Route("api/v1/products")]
[Authorize(AuthPolicies.Customer)]
public sealed class ProductsController : ControllerBase
{
    private readonly ProductService _products;
    private readonly UserService _users;

    public ProductsController(ProductService products, UserService users)
    {
        _products = products;
        _users = users;
    }

    /// <summary>Active products with the promoter's own commission (after their creator's cut).</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _products.ListForUserAsync(await _users.GetActiveAsync(User.CustomerId(), ct), ct));
}

[ApiController]
[Route("api/v1/orders")]
[Authorize(AuthPolicies.Customer)]
public sealed class OrdersController : ControllerBase
{
    private readonly OrderService _orders;
    private readonly UserService _users;

    public OrdersController(OrderService orders, UserService users)
    {
        _orders = orders;
        _users = users;
    }

    [HttpPost]
    [EnableRateLimiting("orders")]
    public async Task<IActionResult> Create(CreateOrderInput body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return StatusCode(201, await _orders.CreateAsync(user, body, idempotencyKey, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _orders.ListForUserAsync(User.CustomerId(), Text.NullIfBlank(status), Paging.From(page, pageSize), ct));

    [HttpGet("counts")]
    public async Task<IActionResult> Counts(CancellationToken ct) => Ok(await _orders.CountsForUserAsync(User.CustomerId(), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _orders.GetForUserAsync(User.CustomerId(), id, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) => Ok(await _orders.CancelAsync(User.CustomerId(), id, ct));
}

[ApiController]
[Route("api/v1/wallet")]
[Authorize(AuthPolicies.Customer)]
public sealed class WalletController : ControllerBase
{
    private readonly WalletService _wallet;
    private readonly UserService _users;

    public WalletController(WalletService wallet, UserService users)
    {
        _wallet = wallet;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(await _wallet.SummaryAsync(User.CustomerId(), ct));

    /// <summary>Every change to the balance, newest first.</summary>
    [HttpGet("entries")]
    public async Task<IActionResult> Entries([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _wallet.EntriesAsync(User.CustomerId(), Paging.From(page, pageSize), ct));

    [HttpGet("payouts")]
    public async Task<IActionResult> Payouts([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _wallet.PayoutsAsync(User.CustomerId(), Paging.From(page, pageSize), ct));

    /// <summary>Withdrawal request to a Qi Card account or a Zain Cash number.</summary>
    [HttpPost("payouts")]
    public async Task<IActionResult> CreatePayout(PayoutInput body, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return StatusCode(201, await _wallet.CreatePayoutAsync(user.Id, body, ct));
    }

    [HttpGet("payouts/{id:long}")]
    public async Task<IActionResult> Payout(long id, CancellationToken ct) => Ok(await _wallet.GetPayoutAsync(User.CustomerId(), id, ct));

    /// <summary>The transfer receipt of one of the promoter's own paid requests.</summary>
    [HttpGet("payouts/{id:long}/receipt")]
    public async Task<IActionResult> Receipt(long id, [FromServices] IConfiguration config, [FromServices] IWebHostEnvironment env, CancellationToken ct)
    {
        var (path, type) = await _wallet.ReceiptAsync(User.CustomerId(), id, MediaStorage.ReceiptsRoot(config, env), ct);
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, type);
    }

    [HttpGet("payouts/{id:long}/messages")]
    public async Task<IActionResult> Messages(long id, [FromQuery] long afterId, CancellationToken ct) =>
        Ok(await _wallet.MessagesSinceAsync(User.CustomerId(), id, afterId, ct));

    [HttpPost("payouts/{id:long}/messages")]
    public async Task<IActionResult> Send(long id, MessageInput body, CancellationToken ct)
    {
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return Ok(await _wallet.UserMessageAsync(user.Id, id, body, ct));
    }

    /// <summary>A photo or document in the chat: multipart <c>file</c> (15 MB at most) and an optional <c>body</c>.</summary>
    [HttpPost("payouts/{id:long}/messages/attachment")]
    [RequestSizeLimit(16_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16_000_000)]
    public async Task<IActionResult> SendAttachment(long id, IFormFile? file, [FromForm] string? body,
        [FromServices] IConfiguration config, [FromServices] IWebHostEnvironment env, CancellationToken ct)
    {
        if (file is null) throw ApiException.Validation(new() { ["file"] = "اختر صورة أو مستنداً" });
        var user = await _users.GetActiveAsync(User.CustomerId(), ct);
        return Ok(await _wallet.UserMessageAsync(user.Id, id, new MessageInput(body), ct, file, MediaStorage.ReceiptsRoot(config, env)));
    }

    [HttpGet("payouts/{id:long}/messages/{messageId:long}/attachment")]
    public async Task<IActionResult> Attachment(long id, long messageId, [FromServices] IConfiguration config, [FromServices] IWebHostEnvironment env, CancellationToken ct)
    {
        var (path, type, name) = await _wallet.AttachmentAsync(User.CustomerId(), id, messageId, MediaStorage.ReceiptsRoot(config, env), ct);
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, type, name);
    }
}

[ApiController]
[Route("api/v1/materials")]
[Authorize(AuthPolicies.Customer)]
public sealed class MaterialsController : ControllerBase
{
    private readonly MaterialService _materials;

    public MaterialsController(MaterialService materials) => _materials = materials;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? kind, CancellationToken ct) => Ok(await _materials.ListAsync(kind, ct));
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

    [HttpGet("unread-count")]
    public async Task<IActionResult> Unread(CancellationToken ct) => Ok(new { count = await _notifications.UnreadCountAsync(User.CustomerId(), ct) });

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
