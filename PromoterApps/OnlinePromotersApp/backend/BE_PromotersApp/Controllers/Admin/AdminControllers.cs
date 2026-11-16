using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Admin;
using BE_PromotersApp.Domain.Materials;
using BE_PromotersApp.Domain.Notifications;
using BE_PromotersApp.Domain.Orders;
using BE_PromotersApp.Domain.Products;
using BE_PromotersApp.Domain.Wallet;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Controllers.Admin;

/// <summary>Dashboard API («المروّجون أونلاين»): المدير and المراقب, with the central gateway JWT.</summary>
[ApiController]
[Authorize(AuthPolicies.Admin)]
public abstract class AdminControllerBase : ControllerBase
{
    protected AdminActor Actor => AdminActor.From(User);

    /// <summary>Iraq calendar day → UTC range [from, to).</summary>
    protected static (DateTime? From, DateTime? To) IraqRange(DateOnly? from, DateOnly? to) =>
        (from is { } f ? IraqTime.DayStartUtc(f) : null, to is { } t ? IraqTime.DayStartUtc(t.AddDays(1)) : null);
}

[Route("api/v1/admin")]
public sealed class AdminOverviewController : AdminControllerBase
{
    private readonly AdminUsersService _users;
    private readonly AuditService _audit;

    public AdminOverviewController(AdminUsersService users, AuditService audit)
    {
        _users = users;
        _audit = audit;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var (f, t) = IraqRange(from, to);
        return Ok(await _users.OverviewAsync(f, t, ct));
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] string? entity, [FromQuery] string? actor, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (f, t) = IraqRange(from, to);
        return Ok(await _audit.SearchAsync(Text.NullIfBlank(entity), Text.NullIfBlank(actor), f, t, Paging.From(page, pageSize, 50), ct));
    }
}

[Route("api/v1/admin/users")]
public sealed class AdminUsersController : AdminControllerBase
{
    private readonly AdminUsersService _users;
    private readonly WalletService _wallet;

    public AdminUsersController(AdminUsersService users, WalletService wallet)
    {
        _users = users;
        _wallet = wallet;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? kind, [FromQuery] long? parentId,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _users.SearchAsync(q, status, kind, parentId, Paging.From(page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _users.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(AdminUserInput body, CancellationToken ct)
    {
        var id = await _users.CreateAsync(body, Actor, ct);
        return StatusCode(201, await _users.GetAsync(id, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, AdminUserInput body, CancellationToken ct)
    {
        await _users.UpdateAsync(id, body, Actor, ct);
        return Ok(await _users.GetAsync(id, ct));
    }

    [HttpPost("{id:long}/password")]
    public async Task<IActionResult> Password(long id, PasswordInput body, CancellationToken ct)
    {
        await _users.SetPasswordAsync(id, body.Password, Actor, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/suspend")]
    public async Task<IActionResult> Suspend(long id, SuspendInput body, CancellationToken ct)
    {
        await _users.SetSuspendedAsync(id, body, Actor, ct);
        return Ok(await _users.GetAsync(id, ct));
    }

    [HttpPost("{id:long}/full")]
    public async Task<IActionResult> Full(long id, FullInput body, CancellationToken ct)
    {
        await _users.SetFullAsync(id, body.IsFull, Actor, ct);
        return Ok(await _users.GetAsync(id, ct));
    }

    [HttpPost("{id:long}/cut")]
    public async Task<IActionResult> Cut(long id, CutInput body, CancellationToken ct)
    {
        await _users.SetCutAsync(id, body.CutPercent, Actor, ct);
        return Ok(await _users.GetAsync(id, ct));
    }

    [HttpGet("{id:long}/wallet")]
    public async Task<IActionResult> Wallet(long id, CancellationToken ct) => Ok(await _wallet.SummaryAsync(id, ct));

    [HttpGet("{id:long}/wallet/entries")]
    public async Task<IActionResult> Entries(long id, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _wallet.AdminEntriesAsync(id, Paging.From(page, pageSize), ct));

    [HttpPost("{id:long}/wallet/adjust")]
    public async Task<IActionResult> Adjust(long id, AdjustmentInput body, CancellationToken ct) => Ok(await _wallet.AdjustAsync(id, body, Actor, ct));
}

[Route("api/v1/admin/products")]
public sealed class AdminProductsController : AdminControllerBase
{
    private readonly ProductService _products;

    public AdminProductsController(ProductService products) => _products = products;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _products.AdminListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(ProductInput body, CancellationToken ct) => StatusCode(201, await _products.CreateAsync(body, Actor, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductInput body, CancellationToken ct) => Ok(await _products.UpdateAsync(id, body, Actor, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, Actor, ct);
        return NoContent();
    }
}

[Route("api/v1/admin/orders")]
public sealed class AdminOrdersController : AdminControllerBase
{
    private readonly OrderService _orders;

    public AdminOrdersController(OrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] long? userId, [FromQuery] string? q, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
    {
        var (f, t) = IraqRange(from, to);
        return Ok(await _orders.AdminListAsync(status, userId, q, f, t, Paging.From(page, pageSize), ct));
    }

    [HttpGet("counts")]
    public async Task<IActionResult> Counts(CancellationToken ct) => Ok(await _orders.AdminCountsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _orders.AdminGetAsync(id, ct));

    /// <summary>Sends waiting orders and pulls statuses from the company system now (one order, or all open ones).</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromQuery] long? orderId, CancellationToken ct)
    {
        var forwarded = orderId is { } id ? (await _orders.TryForwardAsync(id, ct) ? 1 : 0) : await _orders.ForwardPendingAsync(ct);
        var changed = await _orders.SyncAsync(orderId is { } oid ? [oid] : null, ct);
        return Ok(new { forwarded, changed });
    }
}

[Route("api/v1/admin/payouts")]
public sealed class AdminPayoutsController : AdminControllerBase
{
    private readonly WalletService _wallet;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AdminPayoutsController(WalletService wallet, IConfiguration config, IWebHostEnvironment env)
    {
        _wallet = wallet;
        _config = config;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] long? userId, [FromQuery] string? q, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _wallet.AdminListAsync(Text.NullIfBlank(status), userId, q, Paging.From(page, pageSize), ct));

    [HttpGet("counts")]
    public async Task<IActionResult> Counts(CancellationToken ct) => Ok(await _wallet.AdminCountsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => Ok(await _wallet.AdminGetAsync(id, ct));

    [HttpGet("{id:long}/messages")]
    public async Task<IActionResult> Messages(long id, [FromQuery] long afterId, CancellationToken ct) =>
        Ok(await _wallet.MessagesSinceAsync(null, id, afterId, ct));

    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> Send(long id, MessageInput body, CancellationToken ct) => Ok(await _wallet.AdminMessageAsync(id, body, Actor, ct));

    /// <summary>A photo or document in the chat: multipart <c>file</c> (15 MB at most) and an optional <c>body</c>.</summary>
    [HttpPost("{id:long}/messages/attachment")]
    [RequestSizeLimit(16_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16_000_000)]
    public async Task<IActionResult> SendAttachment(long id, IFormFile? file, [FromForm] string? body, CancellationToken ct)
    {
        if (file is null) throw ApiException.Validation(new() { ["file"] = "اختر صورة أو مستنداً" });
        return Ok(await _wallet.AdminMessageAsync(id, new MessageInput(body), Actor, ct, file, MediaStorage.ReceiptsRoot(_config, _env)));
    }

    [HttpGet("{id:long}/messages/{messageId:long}/attachment")]
    public async Task<IActionResult> Attachment(long id, long messageId, CancellationToken ct)
    {
        var (path, type, name) = await _wallet.AttachmentAsync(null, id, messageId, MediaStorage.ReceiptsRoot(_config, _env), ct);
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, type, name);
    }

    public sealed record DecisionBody(string? Note);

    /// <summary>«تم التحويل»: multipart form with <c>receipt</c> (picture of the transfer receipt, required) and an optional <c>note</c>.</summary>
    [HttpPost("{id:long}/paid")]
    [RequestSizeLimit(11_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11_000_000)]
    public async Task<IActionResult> Paid(long id, IFormFile? receipt, [FromForm] string? note, CancellationToken ct) =>
        Ok(await _wallet.MarkPaidAsync(id, note, receipt, MediaStorage.ReceiptsRoot(_config, _env), Actor, ct));

    [HttpGet("{id:long}/receipt")]
    public async Task<IActionResult> Receipt(long id, CancellationToken ct)
    {
        var (path, type) = await _wallet.ReceiptAsync(null, id, MediaStorage.ReceiptsRoot(_config, _env), ct);
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, type);
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, DecisionBody body, CancellationToken ct) => Ok(await _wallet.RejectAsync(id, body.Note, Actor, ct));
}

[Route("api/v1/admin/materials")]
public sealed class AdminMaterialsController : AdminControllerBase
{
    private readonly MaterialService _materials;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public AdminMaterialsController(MaterialService materials, IConfiguration config, IWebHostEnvironment env)
    {
        _materials = materials;
        _config = config;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? kind, CancellationToken ct) => Ok(await _materials.ListAsync(kind, ct));

    [HttpPost]
    [RequestSizeLimit(MaterialService.MaxBytes + 1_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaterialService.MaxBytes + 1_000_000)]
    public async Task<IActionResult> Upload(IFormFile? file, [FromForm] string? title, CancellationToken ct) =>
        StatusCode(201, await _materials.UploadAsync(file, title, MediaStorage.Root(_config, _env), Actor, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, MaterialUpdateInput body, CancellationToken ct)
    {
        await _materials.UpdateAsync(id, body, Actor, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _materials.DeleteAsync(id, Actor, ct);
        return NoContent();
    }
}

[Route("api/v1/admin")]
public sealed class AdminSettingsController : AdminControllerBase
{
    private readonly AppSettingsService _settings;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;

    public AdminSettingsController(AppSettingsService settings, NotificationService notifications, AuditService audit)
    {
        _settings = settings;
        _notifications = notifications;
        _audit = audit;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _settings.GetAsync(ct));

    [HttpPut("settings")]
    public async Task<IActionResult> Put(AppSettings body, CancellationToken ct)
    {
        await _settings.SaveAsync(body, ct);
        await _audit.WriteAsync(Actor, "settings.update", "Settings", null, null, body);
        return Ok(await _settings.GetAsync(ct));
    }

    [HttpPost("notifications/broadcast")]
    public async Task<IActionResult> Broadcast(BroadcastInput body, CancellationToken ct) => Ok(await _notifications.BroadcastAsync(body, Actor, ct));

    [HttpGet("notifications/broadcasts")]
    public async Task<IActionResult> Broadcasts([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        Ok(await _notifications.ListBroadcastsAsync(Paging.From(page, pageSize), ct));
}
