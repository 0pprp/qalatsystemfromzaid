using BE_Company.DTO;
using BE_Company.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    /// <summary>
    /// Central shared notes: GET/POST /api/customers/{customerId}/notes
    /// Same dbo.CustomerNotes store as CustomerDecisions/Notes.
    /// </summary>
    [Authorize]
    [Route("api/customers")]
    [ApiController]
    public class CustomersNotesController : ControllerBase
    {
        private readonly ISharedCustomerNotesService _notes;

        public CustomersNotesController(ISharedCustomerNotesService notes)
        {
            _notes = notes;
        }

        private string GetUserType() => HttpContext.Items["UserType"] as string ?? string.Empty;

        private int? GetAuthenticatedUserId()
        {
            if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
            {
                return parsedUserId;
            }

            return null;
        }

        private bool CanReadNotes()
        {
            var t = GetUserType();
            return t is "محاسب رئيسي" or "مدير مبيعات" or "مدير فرع";
        }

        private bool CanWriteNotes()
        {
            var t = GetUserType();
            return t is "محاسب رئيسي" or "مدير فرع";
        }

        [HttpGet("{customerId:int}/notes")]
        public async Task<IActionResult> List(int customerId, CancellationToken ct)
        {
            if (!CanReadNotes())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مسموح بقراءة ملاحظات الزبون" });
            }

            var rows = await _notes.ListAsync(customerId, ct);
            return Ok(rows.Select(Map));
        }

        [HttpPost("{customerId:int}/notes")]
        public async Task<IActionResult> Add(int customerId, [FromBody] CustomerNoteTextBody? body, CancellationToken ct)
        {
            if (!CanWriteNotes())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مسموح بكتابة ملاحظة" });
            }

            var userId = GetAuthenticatedUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("User is not authenticated.");
            }

            try
            {
                var saved = await _notes.AddAsync(customerId, body?.NoteText ?? "", userId, null, ct);
                return Ok(Map(saved));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private static object Map(SharedCustomerNoteDto n) => new
        {
            noteId = n.NoteId,
            noteID = n.NoteId,
            customerId = n.CustomerId,
            customerID = n.CustomerId,
            noteText = n.NoteText,
            createdByUserId = n.CreatedByUserId,
            userID = n.CreatedByUserId,
            createdByName = n.CreatedByName,
            userName = n.CreatedByName,
            userType = n.UserType,
            createdAtUtc = n.CreatedAtUtc,
            createdDate = n.CreatedDate ?? n.CreatedAtUtc,
        };
    }

    public sealed class CustomerNoteTextBody
    {
        public string? NoteText { get; set; }
    }
}
