using System.Text.Json;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers;

/// <summary>
/// Write-only note to المدير (حيدر). Auth via AsyncId. Does not use CentralComplaints / المفوض.
/// </summary>
[Route("api/delegate/director-notes")]
[ApiController]
public sealed class DelegateDirectorNotesController : ControllerBase
{
    public const string SourceApp = "delegate_application";
    public const string SourceType = "manager_note";
    public const string DelegateRole = "مندوب";
    public const int MinLen = 10;
    public const int MaxLen = 2000;

    private readonly IDelegateRepository _delegates;
    private readonly IDirectorNoteForwarder _forwarder;
    private readonly IConfiguration _configuration;

    public DelegateDirectorNotesController(
        IDelegateRepository delegates,
        IDirectorNoteForwarder forwarder,
        IConfiguration configuration)
    {
        _delegates = delegates;
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
        var asyncId = DelegateComplaintAuth.ReadAsyncIdCredential(Request, body?.AsyncId);
        var expectedDelegateId = DelegateComplaintAuth.ReadExpectedDelegateId(Request);
        var authResult = await DelegateComplaintAuth.AuthenticateAsync(
            _delegates, asyncId, expectedDelegateId, ct);

        if (!authResult.Ok)
            return StatusCode(authResult.StatusCode, new { message = authResult.ErrorMessage });

        var auth = authResult.Delegate!;
        var sender = DelegateComplaintAuth.MapSender(auth);
        var message = (body?.Message ?? "").Trim();
        if (message.Length < MinLen)
            return BadRequest(new { message = $"أدخل على الأقل {MinLen} أحرف" });
        if (message.Length > MaxLen)
            return BadRequest(new { message = $"الحد الأقصى {MaxLen} حرف" });

        message = message.Replace('<', ' ').Replace('>', ' ');

        var forwarded = await _forwarder.ForwardAsync(new DirectorNoteForwardRequest
        {
            Message = message,
            Subject = "ملاحظة مندوب",
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
                branchLink = DelegateComplaintAuth.BranchLinkFromRequest(Request)
            })
        }, ct);

        if (!forwarded.Ok)
            return StatusCode(forwarded.FailureStatusCode, new { message = forwarded.ErrorMessage });

        return Ok(new { message = "تم إرسال الملاحظة إلى المدير بنجاح" });
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
