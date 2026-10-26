using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Director JWT inbox for manager notes from field apps (حيدر).</summary>
[Route("api/director/notes")]
[ApiController]
[Authorize(Policy = SalesPolicies.Director)]
public sealed class DirectorNotesController : ControllerBase
{
    private readonly DirectorManagerNotesService _notes;

    public DirectorNotesController(DirectorManagerNotesService notes)
    {
        _notes = notes;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool unreadOnly,
        [FromQuery] string? cityValue,
        [FromQuery] string? sourceApp,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _notes.InboxAsync(new DirectorNoteInboxQuery
        {
            Page = normalizedPage,
            PageSize = normalizedSize,
            UnreadOnly = unreadOnly,
            CityValue = cityValue,
            SourceApp = sourceApp,
            Search = search
        }, ct);

        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            items = result.Items.Select(ToSummary)
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new { unread = await _notes.UnreadCountAsync(ct) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var note = await _notes.GetAsync(id, ct);
        return note is null
            ? NotFound(new { message = "الملاحظة غير موجودة" })
            : Ok(ToDetail(note));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var note = await _notes.MarkReadAsync(id, ct);
        return note is null
            ? NotFound(new { message = "الملاحظة غير موجودة" })
            : Ok(ToDetail(note));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct) =>
        Ok(new { updated = await _notes.MarkAllReadAsync(ct) });

    private static object ToSummary(DirectorManagerNote n) => new
    {
        id = n.Id,
        sourceApp = n.SourceApp,
        sourceType = n.SourceType,
        senderDisplayName = n.SenderDisplayName,
        senderRole = n.SenderRole,
        cityValue = n.CityValue,
        cityName = n.CityName,
        subject = n.Subject,
        bodyPreview = n.Body.Length <= 120 ? n.Body : n.Body[..120] + "…",
        status = n.Status,
        isUnread = string.Equals(n.Status, ComplaintStatuses.Unread, StringComparison.Ordinal),
        createdAtUtc = n.CreatedAtUtc,
        readAtUtc = n.ReadAtUtc
    };

    private static object ToDetail(DirectorManagerNote n) => new
    {
        id = n.Id,
        sourceApp = n.SourceApp,
        sourceType = n.SourceType,
        senderUserId = n.SenderUserId,
        senderUserName = n.SenderUserName,
        senderDisplayName = n.SenderDisplayName,
        senderRole = n.SenderRole,
        cityValue = n.CityValue,
        cityName = n.CityName,
        subject = n.Subject,
        body = n.Body,
        status = n.Status,
        isUnread = string.Equals(n.Status, ComplaintStatuses.Unread, StringComparison.Ordinal),
        createdAtUtc = n.CreatedAtUtc,
        readAtUtc = n.ReadAtUtc,
        metadataJson = n.MetadataJson
    };
}
