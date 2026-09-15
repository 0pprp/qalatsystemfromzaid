using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Generic intake for the central inbox. Any authenticated gateway user may file a complaint.</summary>
[Authorize]
[Route("api/complaints")]
[ApiController]
public class ComplaintsController : ControllerBase
{
    private readonly CentralComplaintsService _complaints;

    public ComplaintsController(CentralComplaintsService complaints)
    {
        _complaints = complaints;
    }

    public sealed class CreateComplaintRequest
    {
        public string? Subject { get; set; }
        public string? Body { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string? SourceApp { get; set; }
        public string? SourceType { get; set; }
        public JsonElement? Metadata { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateComplaintRequest request, CancellationToken ct)
    {
        var sender = TokenService.FromPrincipal(User);
        var result = await _complaints.CreateAsync(sender, new CreateComplaintInput
        {
            Subject = request.Subject,
            Body = request.Body,
            CityValue = request.CityValue,
            CityName = request.CityName,
            SourceApp = request.SourceApp,
            SourceType = request.SourceType,
            MetadataJson = request.Metadata?.GetRawText()
        }, ct);

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
