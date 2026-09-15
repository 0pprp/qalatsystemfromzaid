using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Server-to-server complaint intake for branch backends that have no gateway JWT.
/// The caller proves itself with the shared internal key and asserts an already-authenticated sender;
/// mobile apps never reach this route directly.
/// </summary>
[Route("api/internal/complaints")]
[ApiController]
public sealed class InternalComplaintsController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly CentralComplaintsService _complaints;
    private readonly IConfiguration _configuration;

    public InternalComplaintsController(CentralComplaintsService complaints, IConfiguration configuration)
    {
        _complaints = complaints;
        _configuration = configuration;
    }

    public sealed class TrustedComplaintRequest
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

    /// <summary>Fails closed: an unset <c>InternalApiKey</c> rejects every caller.</summary>
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
    public async Task<IActionResult> Create([FromBody] TrustedComplaintRequest? request, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        if (request is null)
        {
            return BadRequest(new { message = "نص الشكوى مطلوب" });
        }

        var result = await _complaints.CreateTrustedAsync(
            new TrustedComplaintSender
            {
                SenderUserId = request.SenderUserId,
                SenderUserName = request.SenderUserName,
                SenderDisplayName = request.SenderDisplayName,
                SenderRole = request.SenderRole,
                CityValue = request.CityValue,
                CityName = request.CityName
            },
            new CreateComplaintInput
            {
                Subject = request.Subject,
                Body = string.IsNullOrWhiteSpace(request.Message) ? request.Body : request.Message,
                SourceApp = request.SourceApp,
                SourceType = request.SourceType,
                MetadataJson = request.MetadataJson
            },
            ct);

        if (!result.Ok || result.Complaint is null)
        {
            return BadRequest(new { message = result.Error });
        }

        var complaint = result.Complaint;
        return Ok(new
        {
            id = complaint.Id,
            status = complaint.Status,
            createdAtUtc = complaint.CreatedAtUtc,
            sourceApp = complaint.SourceApp,
            senderDisplayName = complaint.SenderDisplayName,
            senderRole = complaint.SenderRole
        });
    }
}
