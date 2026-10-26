using System.Text.Json;
using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerIdentity;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers;

/// <summary>
/// Write-only note to المدير (حيدر) from follower. Auth via AsyncId.
/// </summary>
[Route("api/Followers/director-notes")]
[ApiController]
public sealed class FollowerDirectorNotesController : ControllerBase
{
    public const string SourceApp = "follower_application";
    public const string SourceType = "manager_note";
    public const string FollowerRole = "متابع";
    public const string AsyncIdHeader = "X-Async-Id";
    public const int MinLen = 10;
    public const int MaxLen = 2000;

    private readonly IFollowerIdentityService _identity;
    private readonly IDirectorNoteForwarder _forwarder;
    private readonly IConfiguration _configuration;

    public FollowerDirectorNotesController(
        IFollowerIdentityService identity,
        IDirectorNoteForwarder forwarder,
        IConfiguration configuration)
    {
        _identity = identity;
        _forwarder = forwarder;
        _configuration = configuration;
    }

    public sealed class CreateNoteBody
    {
        public string? Message { get; set; }
        public string? AsyncId { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNoteBody? body, CancellationToken ct)
    {
        var asyncId = ReadAsyncId(Request, body?.AsyncId);
        var user = await _identity.ResolveByAsyncIdAsync(asyncId, ct);
        if (user == null || user.UserId <= 0 || !user.IsActive)
            return Unauthorized(new { message = "رمز المتابع غير صحيح" });

        var message = (body?.Message ?? "").Trim();
        if (message.Length < MinLen)
            return BadRequest(new { message = $"أدخل على الأقل {MinLen} أحرف" });
        if (message.Length > MaxLen)
            return BadRequest(new { message = $"الحد الأقصى {MaxLen} حرف" });

        message = message.Replace('<', ' ').Replace('>', ' ');

        var forwarded = await _forwarder.ForwardAsync(new DirectorNoteForwardRequest
        {
            Message = message,
            Subject = "ملاحظة متابع",
            SourceApp = SourceApp,
            SourceType = SourceType,
            CityValue = Trimmed(_configuration["Branch:CityValue"]),
            CityName = Trimmed(_configuration["Branch:CityName"]) ?? Trimmed(user.CityName),
            SenderUserId = user.UserId.ToString(),
            SenderUserName = user.UserName,
            SenderDisplayName = string.IsNullOrWhiteSpace(user.UserName) ? "متابع" : user.UserName.Trim(),
            SenderRole = FollowerRole,
            MetadataJson = JsonSerializer.Serialize(new
            {
                followerUserId = user.UserId,
                branchLink = BranchLinkFromRequest(Request)
            })
        }, ct);

        if (!forwarded.Ok)
            return StatusCode(forwarded.FailureStatusCode, new { message = forwarded.ErrorMessage });

        return Ok(new { message = "تم إرسال الملاحظة إلى المدير بنجاح" });
    }

    private static string? ReadAsyncId(HttpRequest request, string? bodyAsyncId)
    {
        if (request.Headers.TryGetValue(AsyncIdHeader, out var headerVals))
        {
            var header = headerVals.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(header))
                return header.Trim();
        }

        return string.IsNullOrWhiteSpace(bodyAsyncId) ? null : bodyAsyncId.Trim();
    }

    private static string? BranchLinkFromRequest(HttpRequest request)
    {
        var scheme = request.Scheme;
        var host = request.Host.Value;
        if (string.IsNullOrWhiteSpace(host))
            return null;
        return $"{scheme}://{host}/api/";
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
