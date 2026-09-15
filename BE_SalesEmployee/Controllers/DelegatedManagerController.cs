using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.DelegatedManager)]
[Route("api/delegated-manager")]
[ApiController]
public class DelegatedManagerController : ControllerBase
{
    private readonly CentralComplaintsService _complaints;
    private readonly SalesExceptionService _exceptions;

    public DelegatedManagerController(CentralComplaintsService complaints, SalesExceptionService exceptions)
    {
        _complaints = complaints;
        _exceptions = exceptions;
    }

    public sealed class DecisionRequest
    {
        public string? Decision { get; set; }
        public string? Note { get; set; }
    }

    [HttpGet("complaints")]
    public async Task<IActionResult> Complaints(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] bool unreadOnly,
        [FromQuery] string? cityValue,
        [FromQuery] string? sourceApp,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _complaints.InboxAsync(new ComplaintInboxQuery
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

    [HttpGet("complaints/unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) =>
        Ok(new { unread = await _complaints.UnreadCountAsync(ct) });

    [HttpGet("complaints/{id:guid}")]
    public async Task<IActionResult> Complaint(Guid id, CancellationToken ct)
    {
        var complaint = await _complaints.GetAsync(id, ct);
        return complaint is null
            ? NotFound(new { message = "الشكوى غير موجودة" })
            : Ok(ToDetail(complaint));
    }

    [HttpPut("complaints/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var complaint = await _complaints.MarkReadAsync(id, ct);
        return complaint is null
            ? NotFound(new { message = "الشكوى غير موجودة" })
            : Ok(ToDetail(complaint));
    }

    [HttpPut("complaints/read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct) =>
        Ok(new { updated = await _complaints.MarkAllReadAsync(ct) });

    [HttpGet("exceptions")]
    public async Task<IActionResult> Exceptions(
        [FromQuery] string? status,
        [FromQuery] string? cityValue,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _exceptions.ListAsync(new SalesExceptionQuery
        {
            Page = normalizedPage,
            PageSize = normalizedSize,
            Status = status,
            CityValue = cityValue,
            TargetApproverType = TargetApproverTypes.DelegatedManager
        }, ct);

        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            items = result.Items.Select(ToExceptionSummary)
        });
    }

    [HttpGet("exceptions/{id:guid}")]
    public async Task<IActionResult> Exception(Guid id, CancellationToken ct)
    {
        var detail = await _exceptions.GetAsync(id, ct);
        if (detail is null)
        {
            return NotFound(new { message = "الطلب غير موجود" });
        }

        return Ok(new
        {
            request = ToExceptionDetail(detail.Request),
            audit = detail.Audit.Select(a => new
            {
                a.Id,
                a.ActorUserName,
                a.ActorDisplayName,
                a.ActorRole,
                a.PreviousStatus,
                a.NewStatus,
                a.DecisionNote,
                a.CreatedAtUtc
            })
        });
    }

    [HttpPut("exceptions/{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, [FromBody] DecisionRequest request, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var result = await _exceptions.DecideAsync(actor, id, request.Decision, request.Note, ct);
        return result.Outcome switch
        {
            SalesExceptionOutcome.Success => Ok(ToExceptionDetail(result.Request!)),
            SalesExceptionOutcome.NotFound => NotFound(new { message = result.Error }),
            SalesExceptionOutcome.Conflict => Conflict(new { message = result.Error }),
            SalesExceptionOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] string? cityValue, CancellationToken ct)
    {
        var counts = await _exceptions.DashboardCountsAsync(cityValue, ct);
        return Ok(new
        {
            unreadComplaints = await _complaints.UnreadCountAsync(ct),
            exceptions = new
            {
                pending = Count(counts, ExceptionStatuses.Pending),
                approved = Count(counts, ExceptionStatuses.Approved),
                rejected = Count(counts, ExceptionStatuses.Rejected),
                cancelled = Count(counts, ExceptionStatuses.Cancelled),
                total = counts.Values.Sum()
            }
        });
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string status) =>
        counts.TryGetValue(status, out var value) ? value : 0;

    private static object ToSummary(CentralComplaint c) => new
    {
        c.Id,
        c.SourceApp,
        c.SourceType,
        c.SenderDisplayName,
        c.SenderRole,
        c.CityValue,
        c.CityName,
        c.Subject,
        c.Status,
        c.CreatedAtUtc,
        c.ReadAtUtc
    };

    private static object ToDetail(CentralComplaint c) => new
    {
        c.Id,
        c.SourceApp,
        c.SourceType,
        c.SenderUserId,
        c.SenderUserName,
        c.SenderDisplayName,
        c.SenderRole,
        c.CityValue,
        c.CityName,
        c.Subject,
        c.Body,
        c.Status,
        c.CreatedAtUtc,
        c.ReadAtUtc,
        c.MetadataJson
    };

    internal static object ToExceptionSummary(SalesExceptionRequest r) => new
    {
        r.Id,
        r.CityValue,
        r.CityName,
        r.CustomerId,
        r.CustomerName,
        r.SalesRequestId,
        r.RequestingManagerDisplayName,
        r.Status,
        r.TargetApproverType,
        r.RequestedAtUtc,
        r.DecidedAtUtc
    };

    internal static object ToExceptionDetail(SalesExceptionRequest r) => new
    {
        r.Id,
        r.CityValue,
        r.CityName,
        r.CustomerId,
        r.CustomerName,
        r.CustomerPhone,
        r.SalesRequestId,
        r.RequestingManagerUserName,
        r.RequestingManagerDisplayName,
        r.Reason,
        r.TargetApproverType,
        r.Status,
        r.RequestedAtUtc,
        r.DecidedAtUtc,
        r.DecisionMakerUserName,
        r.DecisionMakerDisplayName,
        r.DecisionNote,
        r.BranchCustomerNotePosted
    };
}
