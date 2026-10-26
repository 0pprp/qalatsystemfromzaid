using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Server-to-server intake for notes destined to المدير (حيدر).
/// Branch backends prove themselves with X-Sales-Gateway-Key; mobile apps never call this.
/// </summary>
[Route("api/internal/director-notes")]
[ApiController]
public sealed class InternalDirectorNotesController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly DirectorManagerNotesService _notes;
    private readonly IConfiguration _configuration;

    public InternalDirectorNotesController(DirectorManagerNotesService notes, IConfiguration configuration)
    {
        _notes = notes;
        _configuration = configuration;
    }

    public sealed class TrustedDirectorNoteRequest
    {
        public string? Message { get; set; }
        public string? Body { get; set; }
        public string? Subject { get; set; }
        public string? SourceApp { get; set; }
        public string? SourceType { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string? SenderUserId { get; set; }
        public string? SenderUserName { get; set; }
        public string? SenderDisplayName { get; set; }
        public string? SenderRole { get; set; }
        public string? MetadataJson { get; set; }
    }

    public static bool HasValidGatewayKey(HttpRequest request, IConfiguration configuration)
    {
        if (!request.Headers.TryGetValue(GatewayKeyHeader, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expected = configuration["InternalApiKey"];
        return !string.IsNullOrWhiteSpace(expected)
               && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TrustedDirectorNoteRequest? request, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });

        if (request is null)
            return BadRequest(new { message = "نص الملاحظة مطلوب" });

        var result = await _notes.CreateTrustedAsync(
            new TrustedDirectorNoteSender
            {
                SenderUserId = request.SenderUserId,
                SenderUserName = request.SenderUserName,
                SenderDisplayName = request.SenderDisplayName,
                SenderRole = request.SenderRole,
                CityValue = request.CityValue,
                CityName = request.CityName
            },
            new CreateDirectorNoteInput
            {
                Subject = request.Subject,
                Body = string.IsNullOrWhiteSpace(request.Message) ? request.Body : request.Message,
                SourceApp = request.SourceApp,
                SourceType = request.SourceType,
                MetadataJson = request.MetadataJson
            },
            ct);

        if (!result.Ok || result.Note is null)
            return BadRequest(new { message = result.Error });

        var note = result.Note;
        return Ok(new
        {
            id = note.Id,
            status = note.Status,
            createdAtUtc = note.CreatedAtUtc,
            sourceApp = note.SourceApp,
            senderDisplayName = note.SenderDisplayName,
            senderRole = note.SenderRole
        });
    }
}
