using System.Text.Json;
using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerIdentity;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers;

/// <summary>
/// Write-only complaint intake for authenticated followers.
/// Identity from Users (متابع) via AsyncId only — never client-supplied sender fields.
/// </summary>
[Route("api/Followers/complaints")]
[ApiController]
public sealed class FollowerComplaintsController : ControllerBase
{
    public const string SourceApp = "follower_application";
    public const string SourceType = "complaint";
    public const string FollowerRole = "متابع";
    public const string AsyncIdHeader = "X-Async-Id";

    private readonly IFollowerIdentityService _identity;
    private readonly IFollowerComplaintsService _complaints;
    private readonly ICentralComplaintForwarder _forwarder;
    private readonly IConfiguration _configuration;

    public FollowerComplaintsController(
        IFollowerIdentityService identity,
        IFollowerComplaintsService complaints,
        ICentralComplaintForwarder forwarder,
        IConfiguration configuration)
    {
        _identity = identity;
        _complaints = complaints;
        _forwarder = forwarder;
        _configuration = configuration;
    }

    public sealed class CreateComplaintBody
    {
        public string? Message { get; set; }
        public string? AsyncId { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateComplaintBody? body, CancellationToken ct)
    {
        var asyncId = ReadAsyncId(Request, body?.AsyncId);
        var user = await _identity.ResolveByAsyncIdAsync(asyncId, ct);
        if (user == null || user.UserId <= 0 || !user.IsActive)
        {
            return Unauthorized(new { message = "رمز المتابع غير صحيح" });
        }

        var message = body?.Message ?? "";
        long localId;
        DateTime createdAtUtc;
        try
        {
            var saved = await _complaints.CreateAsync(
                userId: user.UserId,
                senderDisplayName: user.UserName,
                cityId: user.CityId,
                branchLink: BranchLinkFromRequest(Request),
                messageText: message,
                ct);
            localId = saved.Id;
            createdAtUtc = saved.CreatedAtUtc;
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var forwarded = await _forwarder.ForwardAsync(BuildForwardRequest(user, localId, message), ct);
        if (!forwarded.Ok)
        {
            return StatusCode(forwarded.FailureStatusCode, new { message = forwarded.ErrorMessage });
        }

        return Ok(new
        {
            id = localId,
            createdAtUtc,
            message = "تم إرسال الشكوى بنجاح"
        });
    }

    private CentralComplaintForwardRequest BuildForwardRequest(
        FollowerUserIdentity user,
        long localId,
        string message) => new()
    {
        Message = message,
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
            localComplaintId = localId,
            branchLink = BranchLinkFromRequest(Request)
        })
    };

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
