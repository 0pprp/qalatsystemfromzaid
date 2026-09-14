using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers;

/// <summary>
/// Write-only complaint intake for authenticated delegates.
/// Identity: GetDelegateLogin(AsyncId) only. No client DelegateId/SenderName/BranchLink.
/// </summary>
[Route("api/delegate/complaints")]
[ApiController]
public sealed class DelegateComplaintsController : ControllerBase
{
    private readonly IDelegateRepository _delegates;
    private readonly IDelegateComplaintsService _complaints;

    public DelegateComplaintsController(
        IDelegateRepository delegates,
        IDelegateComplaintsService complaints)
    {
        _delegates = delegates;
        _complaints = complaints;
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

        try
        {
            var saved = await _complaints.CreateAsync(
                delegateId: sender.DelegateId,
                userId: sender.UserId,
                senderDisplayName: sender.SenderDisplayName,
                cityId: sender.CityId,
                branchLink: DelegateComplaintAuth.BranchLinkFromRequest(Request),
                messageText: body?.Message ?? "",
                ct);

            return Ok(new
            {
                id = saved.Id,
                createdAtUtc = saved.CreatedAtUtc,
                message = "تم إرسال الشكوى بنجاح"
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
