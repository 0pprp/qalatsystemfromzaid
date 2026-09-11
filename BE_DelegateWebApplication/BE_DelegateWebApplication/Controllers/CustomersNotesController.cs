using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    /// <summary>
    /// Shared customer notes keyed by CustomerId (dbo.CustomerNotes).
    /// </summary>
    [Route("api/customers")]
    [ApiController]
    public class CustomersNotesController : ControllerBase
    {
        private readonly IDelegateRepository _delegateRepository;
        private readonly IFollowerActionsRepository _followerActions;
        private readonly ISharedCustomerNotesService _notes;

        public CustomersNotesController(
            IDelegateRepository delegateRepository,
            IFollowerActionsRepository followerActions,
            ISharedCustomerNotesService notes)
        {
            _delegateRepository = delegateRepository;
            _followerActions = followerActions;
            _notes = notes;
        }

        [HttpGet("{customerId:int}/notes")]
        public async Task<IActionResult> List(int customerId, [FromQuery] string? asyncId, CancellationToken ct)
        {
            var auth = await AuthenticateDelegate(asyncId);
            if (auth == null)
            {
                return Unauthorized(new { message = "رمز المندوب غير صحيح" });
            }

            var denied = await EnsureDelegateOwnsCustomer(auth.DelegateId, customerId, ct);
            if (denied != null)
            {
                return denied;
            }

            var rows = await _notes.ListAsync(customerId, ct);
            return Ok(rows.Select(SharedCustomerNotesMapper.ToApi));
        }

        [HttpPost("{customerId:int}/notes")]
        public async Task<IActionResult> Add(int customerId, [FromBody] DelegateCustomerNoteCreateDTO? body, CancellationToken ct)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.AsyncId))
            {
                return BadRequest(new { message = "رمز المندوب مطلوب" });
            }

            var auth = await AuthenticateDelegate(body.AsyncId);
            if (auth == null)
            {
                return Unauthorized(new { message = "رمز المندوب غير صحيح" });
            }

            var denied = await EnsureDelegateOwnsCustomer(auth.DelegateId, customerId, ct);
            if (denied != null)
            {
                return denied;
            }

            try
            {
                var saved = await _notes.AddAsync(
                    customerId,
                    body.NoteText ?? "",
                    createdByUserId: null,
                    createdByName: auth.DelegateName?.Trim() is { Length: > 0 } n ? n : "مندوب",
                    ct);
                return Ok(SharedCustomerNotesMapper.ToApi(saved));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<DelegateGetDTO?> AuthenticateDelegate(string? asyncId)
        {
            if (string.IsNullOrWhiteSpace(asyncId))
            {
                return null;
            }

            return await _delegateRepository.GetDelegateLogin(asyncId.Trim().TrimEnd('/'));
        }

        private async Task<IActionResult?> EnsureDelegateOwnsCustomer(int delegateId, int customerId, CancellationToken ct)
        {
            if (customerId <= 0)
            {
                return BadRequest(new { message = "الزبون مطلوب" });
            }

            var scope = await _followerActions.GetCustomerScopeAsync(customerId, ct);
            if (scope == null)
            {
                return NotFound(new { message = "الزبون غير موجود" });
            }

            if (scope.Value.DelegateId == delegateId)
            {
                return null;
            }

            var children = await _delegateRepository.GetDelegateSelect(delegateId) ?? [];
            if (children.Any(c =>
                    c.DelegateChildId == scope.Value.DelegateId || c.DelegateId == scope.Value.DelegateId))
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden, new { message = "الزبون خارج نطاق قوائم المندوب" });
        }
    }

    public sealed class DelegateCustomerNoteCreateDTO
    {
        public string? AsyncId { get; set; }
        public string? NoteText { get; set; }
    }

    public static class SharedCustomerNotesMapper
    {
        public static object ToApi(SharedCustomerNoteDto n) => new
        {
            noteId = n.NoteId,
            id = n.NoteId,
            customerId = n.CustomerId,
            noteText = n.NoteText,
            createdByUserId = n.CreatedByUserId,
            createdByName = n.CreatedByName,
            createdAtUtc = n.CreatedAtUtc,
        };
    }
}
