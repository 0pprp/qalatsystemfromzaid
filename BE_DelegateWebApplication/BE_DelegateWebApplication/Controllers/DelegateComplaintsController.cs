using System.Text.Json;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers;

/// <summary>
/// Write-only complaint intake for authenticated delegates.
/// Identity: GetDelegateLogin(AsyncId) only. No client DelegateId/SenderName/BranchLink.
/// The row is kept locally for audit and must also reach the central inbox on BE_SalesEmployee.
/// </summary>
[Route("api/delegate/complaints")]
[ApiController]
public sealed class DelegateComplaintsController : ControllerBase
{
    public const string SourceApp = "delegate_application";
    public const string SourceType = "complaint";
    public const string DelegateRole = "مندوب";

    private readonly IDelegateRepository _delegates;
    private readonly IDelegateComplaintsService _complaints;
    private readonly ICentralComplaintForwarder _forwarder;
    private readonly IConfiguration _configuration;

    public DelegateComplaintsController(
        IDelegateRepository delegates,
        IDelegateComplaintsService complaints,
        ICentralComplaintForwarder forwarder,
        IConfiguration configuration)
    {
        _delegates = delegates;
        _complaints = complaints;
        _forwarder = forwarder;
        _configuration = configuration;
    }

    /// <summary>Final POST contract: message only. Auth via X-Async-Id (preferred) or body.asyncId.</summary>
    public sealed class CreateComplaintBody
    {
        public string? Message { get; set; }

        /// <summary>
        /// Legacy/fallback credential only. Prefer header <c>X-Async-Id</c>.
        /// Never stored; never used as display identity.
        /// </summary>
        public string? AsyncId { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateComplaintBody? body, CancellationToken ct)
    {
        var asyncId = DelegateComplaintAuth.ReadAsyncIdCredential(Request, body?.AsyncId);
        var expectedDelegateId = DelegateComplaintAuth.ReadExpectedDelegateId(Request);
        var authResult = await DelegateComplaintAuth.AuthenticateAsync(
            _delegates, asyncId, expectedDelegateId, ct);

        if (!authResult.Ok)
        {
            return StatusCode(authResult.StatusCode, new { message = authResult.ErrorMessage });
        }

        var auth = authResult.Delegate!;
        var sender = DelegateComplaintAuth.MapSender(auth);
        var message = body?.Message ?? "";

        long localId;
        DateTime createdAtUtc;
        try
        {
            var saved = await _complaints.CreateAsync(
                delegateId: sender.DelegateId,
                userId: sender.UserId,
                senderDisplayName: sender.SenderDisplayName,
                cityId: sender.CityId,
                branchLink: DelegateComplaintAuth.BranchLinkFromRequest(Request),
                messageText: message,
                ct);
            localId = saved.Id;
            createdAtUtc = saved.CreatedAtUtc;
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var forwarded = await _forwarder.ForwardAsync(BuildForwardRequest(sender, localId, message), ct);
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

    /// <summary>
    /// Every identity field comes from the resolved delegate or branch configuration,
    /// so a tampered client body can never change who the central inbox sees.
    /// </summary>
    private CentralComplaintForwardRequest BuildForwardRequest(
        (int DelegateId, int? UserId, string SenderDisplayName, int? CityId) sender,
        long localId,
        string message) => new()
        {
            Message = message,
            SourceApp = SourceApp,
            SourceType = SourceType,
            CityValue = Trimmed(_configuration["Branch:CityValue"]),
            CityName = Trimmed(_configuration["Branch:CityName"]),
            SenderUserId = (sender.UserId ?? sender.DelegateId).ToString(),
            SenderUserName = $"delegate-{sender.DelegateId}",
            SenderDisplayName = sender.SenderDisplayName,
            SenderRole = DelegateRole,
            MetadataJson = JsonSerializer.Serialize(new
            {
                delegateId = sender.DelegateId,
                localComplaintId = localId,
                branchLink = DelegateComplaintAuth.BranchLinkFromRequest(Request)
            })
        };

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
