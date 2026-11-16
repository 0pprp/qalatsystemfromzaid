using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerIdentity;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers;

/// <summary>
/// Follower (متابع) shop GPS + shop photo for the customers of his assigned lists.
/// Every call is scoped to lists linked to the follower (FollowerUserLists).
/// </summary>
[Route("api/Followers/ShopCapture")]
[ApiController]
public sealed class FollowerShopCaptureController : ControllerBase
{
    private readonly IFollowerShopCaptureService _capture;
    private readonly IFollowerIdentityService _identity;
    private readonly IDelegateRepository _delegates;
    private readonly IFollowerActionsRepository _actions;

    public FollowerShopCaptureController(
        IFollowerShopCaptureService capture,
        IFollowerIdentityService identity,
        IDelegateRepository delegates,
        IFollowerActionsRepository actions)
    {
        _capture = capture;
        _identity = identity;
        _delegates = delegates;
        _actions = actions;
    }

    public sealed class LocationBody
    {
        public string? AsyncId { get; set; }
        public int ListId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
    }

    [HttpGet("Lists")]
    public async Task<IActionResult> Lists([FromQuery] string? asyncId, CancellationToken ct)
    {
        var userId = await FollowerUserIdAsync(asyncId);
        if (userId == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
        return Ok(new { items = await _capture.GetListsAsync(userId.Value, ct) });
    }

    [HttpGet("Lists/{listId:int}/Customers")]
    public async Task<IActionResult> ListCustomers(int listId, [FromQuery] string? asyncId, CancellationToken ct)
    {
        var userId = await FollowerUserIdAsync(asyncId);
        if (userId == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
        if (listId <= 0 || !FollowerAuthorization.CanAccessAssignedList(await _delegates.IsFollowerListLinked(userId.Value, listId)))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه القائمة غير مرتبطة بحساب المتابع" });
        }

        return Ok(new { items = await _capture.GetListCustomersAsync(listId, ct) });
    }

    [HttpPut("Customers/{customerId:int}/Location")]
    public async Task<IActionResult> SaveLocation(int customerId, [FromBody] LocationBody? body, CancellationToken ct)
    {
        body ??= new LocationBody();
        var userId = await FollowerUserIdAsync(body.AsyncId);
        if (userId == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
        var denied = await EnsureCustomerInScope(userId.Value, body.ListId, customerId, ct);
        if (denied != null) return denied;
        if (!FollowerShopCaptureService.IsValidIraqPoint(body.Latitude, body.Longitude))
        {
            return BadRequest(new { message = "الموقع غير صالح" });
        }

        await _capture.SaveLocationAsync(customerId, body.Latitude, body.Longitude, body.Accuracy, userId.Value, ct);
        return Ok(new { saved = true });
    }

    [HttpPut("Customers/{customerId:int}/Photo")]
    [RequestSizeLimit(FollowerShopCaptureService.MaxPhotoBytes + 64 * 1024)]
    public async Task<IActionResult> SavePhoto(
        int customerId,
        [FromForm] string? asyncId,
        [FromForm] int listId,
        IFormFile? photo,
        CancellationToken ct)
    {
        var userId = await FollowerUserIdAsync(asyncId);
        if (userId == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
        var denied = await EnsureCustomerInScope(userId.Value, listId, customerId, ct);
        if (denied != null) return denied;
        if (photo == null || photo.Length == 0 || photo.Length > FollowerShopCaptureService.MaxPhotoBytes)
        {
            return BadRequest(new { message = "الصورة مطلوبة (بحد أقصى 6 ميغابايت)" });
        }

        await using var ms = new MemoryStream();
        await photo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var type = DetectImageType(bytes);
        if (type == null)
        {
            return BadRequest(new { message = "الملف ليس صورة" });
        }

        await _capture.SavePhotoAsync(customerId, bytes, type, userId.Value, ct);
        return Ok(new { saved = true });
    }

    [HttpGet("Customers/{customerId:int}/Photo")]
    public async Task<IActionResult> GetPhoto(int customerId, [FromQuery] string? asyncId, [FromQuery] int listId, CancellationToken ct)
    {
        var userId = await FollowerUserIdAsync(asyncId);
        if (userId == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
        var denied = await EnsureCustomerInScope(userId.Value, listId, customerId, ct);
        if (denied != null) return denied;
        var photo = await _capture.GetPhotoAsync(customerId, ct);
        return photo == null ? NotFound(new { message = "لا توجد صورة" }) : File(photo.Value.Bytes, photo.Value.ContentType);
    }

    /// <summary>JPEG / PNG / WebP by magic bytes — never trust the client content type.</summary>
    public static string? DetectImageType(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "image/webp";
        return null;
    }

    private async Task<int?> FollowerUserIdAsync(string? asyncId)
    {
        var user = await _identity.ResolveByAsyncIdAsync(asyncId);
        return user == null || user.UserId <= 0 || !user.IsActive ? null : user.UserId;
    }

    private async Task<IActionResult?> EnsureCustomerInScope(int userId, int listId, int customerId, CancellationToken ct)
    {
        if (listId <= 0 || customerId <= 0)
        {
            return BadRequest(new { message = "القائمة والزبون مطلوبان" });
        }

        var linked = await _delegates.IsFollowerListLinked(userId, listId);
        var scope = await _actions.GetCustomerScopeAsync(customerId, ct);
        if (scope == null)
        {
            return NotFound(new { message = "الزبون غير موجود" });
        }

        return FollowerAuthorization.CanNoteCustomer(linked, scope.Value.DelegateId, listId)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "الزبون خارج نطاق القوائم المسندة لك" });
    }
}
