using System.Text.Json;
using BE_SalesEmployee.DelegatedManager;
using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
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
    private readonly CustomerWaiverService _waivers;
    private readonly ISalesManagerBranchAggregator _aggregator;

    public DelegatedManagerController(
        CentralComplaintsService complaints,
        SalesExceptionService exceptions,
        CustomerWaiverService waivers,
        ISalesManagerBranchAggregator aggregator)
    {
        _complaints = complaints;
        _exceptions = exceptions;
        _waivers = waivers;
        _aggregator = aggregator;
    }

    public sealed class DecisionRequest
    {
        public string? Decision { get; set; }
        public string? Note { get; set; }
        public double? ApprovedAmount { get; set; }
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
        var (outcome, detail, error) = await _exceptions.GetEnrichedDetailAsync(id, ct);
        return outcome switch
        {
            SalesExceptionOutcome.NotFound => NotFound(new { message = error ?? "الطلب غير موجود" }),
            SalesExceptionOutcome.Invalid => BadRequest(new { message = error }),
            SalesExceptionOutcome.Success when detail is not null => Ok(new
            {
                request = ToExceptionDetail(detail.Request),
                audit = detail.Audit.Select(a => new
                {
                    a.ActorDisplayName,
                    a.ActorRole,
                    a.PreviousStatus,
                    a.NewStatus,
                    a.DecisionNote,
                    a.CreatedAtUtc
                }),
                review = detail.Review,
                customerProfile = detail.CustomerProfile,
                reviewError = detail.ReviewError,
                decisionHistory = detail.Audit.Select(a => new
                {
                    a.ActorDisplayName,
                    a.ActorRole,
                    a.PreviousStatus,
                    a.NewStatus,
                    a.DecisionNote,
                    a.CreatedAtUtc
                })
            }),
            _ => BadRequest(new { message = error ?? "تعذر تحميل التفاصيل" })
        };
    }

    [HttpPost("exceptions/{id:guid}/read")]
    public async Task<IActionResult> MarkExceptionRead(Guid id, CancellationToken ct)
    {
        var row = await _exceptions.MarkReadAsync(id, ct);
        if (row is null)
        {
            return NotFound(new { message = "الطلب غير موجود" });
        }

        return Ok(ToExceptionDetail(row));
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

    [HttpGet("waivers")]
    public async Task<IActionResult> Waivers(
        [FromQuery] string? status,
        [FromQuery] string? cityValue,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _waivers.ListAsync(new CustomerWaiverQuery
        {
            Page = normalizedPage,
            PageSize = normalizedSize,
            Status = status,
            CityValue = cityValue
        }, ct);

        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            items = result.Items.Select(ToWaiverSummary)
        });
    }

    [HttpGet("waivers/{id:guid}")]
    public async Task<IActionResult> Waiver(Guid id, CancellationToken ct)
    {
        var (outcome, detail, error) = await _waivers.GetDetailAsync(id, ct);
        return outcome switch
        {
            CustomerWaiverOutcome.NotFound => NotFound(new { message = error ?? "الطلب غير موجود" }),
            CustomerWaiverOutcome.Success when detail is not null => Ok(new
            {
                request = ToWaiverDetail(detail.Request),
                audit = detail.Audit,
                review = detail.Review,
                reviewError = detail.ReviewError
            }),
            _ => BadRequest(new { message = error ?? "تعذر تحميل التفاصيل" })
        };
    }

    [HttpPut("waivers/{id:guid}/decision")]
    public async Task<IActionResult> DecideWaiver(Guid id, [FromBody] DecisionRequest request, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var result = await _waivers.DecideAsync(
            actor, id, request.Decision, request.Note, request.ApprovedAmount, ct);
        return result.Outcome switch
        {
            CustomerWaiverOutcome.Success => Ok(ToWaiverDetail(result.Request!)),
            CustomerWaiverOutcome.NotFound => NotFound(new { message = result.Error }),
            CustomerWaiverOutcome.Conflict => Conflict(new { message = result.Error }),
            CustomerWaiverOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] string? cityValue, CancellationToken ct)
    {
        // Hold exceptions only (WrongSale isolated in unreadWrongSales + own app tab).
        var counts = await _exceptions.HoldDashboardCountsAsync(cityValue, ct);
        var waiverCounts = await _waivers.DashboardCountsAsync(cityValue, null, ct);
        var unreadWrongSales = await _exceptions.UnreadWrongSalesCountAsync(cityValue, ct);
        return Ok(new
        {
            unreadComplaints = await _complaints.UnreadCountAsync(ct),
            unreadWrongSales,
            exceptions = new
            {
                pending = Count(counts, ExceptionStatuses.Pending),
                approved = Count(counts, ExceptionStatuses.Approved),
                rejected = Count(counts, ExceptionStatuses.Rejected),
                cancelled = Count(counts, ExceptionStatuses.Cancelled),
                total = counts.Values.Sum()
            },
            waivers = new
            {
                pending = Count(waiverCounts, ExceptionStatuses.Pending),
                approved = Count(waiverCounts, ExceptionStatuses.Approved),
                rejected = Count(waiverCounts, ExceptionStatuses.Rejected),
                total = waiverCounts.Values.Sum()
            }
        });
    }

    /// <summary>
    /// Ratings catalog for one province. Proxies BE_Company sales-manager/stats/catalog
    /// (same BranchManagerStatsService as مدير الفرع). No DB schema change.
    /// Impact: DM Flutter only; FE_Company / BM / other Flutter apps unchanged.
    /// </summary>
    [HttpGet("stats/catalog")]
    public async Task<IActionResult> StatsCatalog([FromQuery] string? cityValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyCatalog,
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("stats/trend")]
    public async Task<IActionResult> StatsTrend(
        [FromQuery] string? cityValue,
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyTrend(scope, id, from, to),
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>
    /// زبائن مستوى تقييم واحد — نفس منطق لوحة المدير / مدير الفرع (BranchManagerStatsService).
    /// Impact: DM Flutter ratings board only; FE_Company / other Flutter apps unchanged.
    /// </summary>
    [HttpGet("stats/rating-customers")]
    public async Task<IActionResult> StatsRatingCustomers(
        [FromQuery] string? cityValue,
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] string? level,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int take = 500,
        [FromQuery] int skip = 0,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        // level optional: omit = every rated customer (same as branch-manager board).
        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyRatingCustomers(scope, id, level, from, to, take, skip, q),
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>
    /// كروت المتابعين — نفس عمل المتابعين (director-follower-ops). قراءة فقط.
    /// Impact: FE_Company DM ratings; BE_Company unchanged; Flutter unchanged.
    /// </summary>
    [HttpGet("followers/ops/follower-cards")]
    public async Task<IActionResult> FollowerOpsFollowerCards(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "sales-manager/director-follower-ops/follower-cards", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/summaries")]
    public async Task<IActionResult> FollowerOpsSummaries(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "sales-manager/director-follower-ops/summaries", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/work-bundle")]
    public async Task<IActionResult> FollowerOpsWorkBundle(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/director-follower-ops/followers/{followerId}/work-bundle",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/remaining-customers")]
    public async Task<IActionResult> FollowerOpsRemainingCustomers(
        int followerId,
        [FromQuery] string? cityValue,
        [FromQuery] string? bucket,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var q = string.IsNullOrWhiteSpace(bucket) ? "" : $"?bucket={Uri.EscapeDataString(bucket)}";
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/director-follower-ops/followers/{followerId}/remaining-customers{q}",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/collection")]
    public async Task<IActionResult> FollowerOpsCollection(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/director-follower-ops/followers/{followerId}/collection",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/legal-followups")]
    public async Task<IActionResult> FollowerOpsLegalFollowups(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/director-follower-ops/followers/{followerId}/legal-followups",
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>
    /// تفاصيل قائمة مثل متابعة المندوبين — نفس director-delegate-followup.
    /// Impact: FE DM ratings list drill-down only.
    /// </summary>
    [HttpGet("delegate-followup/lists/{listId:int}/summary")]
    public async Task<IActionResult> DelegateFollowupListSummary(
        int listId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/director-delegate-followup/lists/{listId}/summary",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("delegate-followup/lists/{listId:int}/customers")]
    public async Task<IActionResult> DelegateFollowupListCustomers(
        int listId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/director-delegate-followup/lists/{listId}/customers",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("customers/{cityValue}/{customerId:int}/profile")]
    public async Task<IActionResult> CustomerProfileById(string cityValue, int customerId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/profile",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("customers/{cityValue}/{customerId:int}/notes")]
    public async Task<IActionResult> CustomerNotes(string cityValue, int customerId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/notes",
            ct);
        return StatusCode(status, payload);
    }

    [HttpPost("customers/{cityValue}/notes")]
    public async Task<IActionResult> AddCustomerNote(
        string cityValue,
        [FromBody] JsonElement body,
        CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.PostAsync(
            actor,
            cityValue,
            "sales-manager/customers/notes",
            body.GetRawText(),
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("sales/{cityValue}/{saleId:int}/shop-image")]
    public async Task<IActionResult> SaleShopImage(string cityValue, int saleId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetFileAsync(
            actor,
            cityValue,
            $"sales-manager/sales/{saleId}/shop-image",
            ct);
        if (payload is ValueTuple<byte[], string> file)
        {
            return File(file.Item1, string.IsNullOrWhiteSpace(file.Item2) ? "image/jpeg" : file.Item2);
        }

        return StatusCode(status, payload ?? new { message = "الصورة غير موجودة." });
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string status) =>
        counts.TryGetValue(status, out var value) ? value : 0;

    private static object ToSummary(CentralComplaint c) => new
    {
        senderDisplayName = c.SenderDisplayName,
        senderRole = c.SenderRole,
        cityValue = c.CityValue,
        cityName = c.CityName,
        subject = c.Subject,
        status = c.Status,
        createdAtUtc = c.CreatedAtUtc,
        readAtUtc = c.ReadAtUtc,
        sourceLabel = FriendlySourceLabel(c.SourceApp, c.SourceType),
        // id kept for client navigation only — Flutter must not render it
        id = c.Id
    };

    private static object ToDetail(CentralComplaint c) => new
    {
        senderDisplayName = c.SenderDisplayName,
        senderRole = c.SenderRole,
        cityValue = c.CityValue,
        cityName = c.CityName,
        subject = c.Subject,
        body = c.Body,
        status = c.Status,
        createdAtUtc = c.CreatedAtUtc,
        readAtUtc = c.ReadAtUtc,
        sourceLabel = FriendlySourceLabel(c.SourceApp, c.SourceType),
        id = c.Id
    };

    internal static string FriendlySourceLabel(string? sourceApp, string? sourceType)
    {
        var app = (sourceApp ?? "").Trim().ToLowerInvariant();
        if (app.Contains("delegate"))
            return "مندوب";
        if (app.Contains("follower") || app.Contains("متابع"))
            return "متابع";
        if (app.Contains("sales") || app.Contains("employee"))
            return "موظف مبيعات";
        if (app.Contains("filter"))
            return "موظف فلترة";

        var type = (sourceType ?? "").Trim();
        if (string.Equals(type, "Complaint", StringComparison.OrdinalIgnoreCase))
            return "شكوى";
        if (string.Equals(type, "Message", StringComparison.OrdinalIgnoreCase))
            return "رسالة";
        if (!string.IsNullOrWhiteSpace(type) && type.Any(c => c > 127))
            return type;

        return "غير متوفر";
    }

    private static bool IsWrongSaleReason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason)
        && (reason.Contains("مبيعات خاطئة", StringComparison.Ordinal)
            || reason.Contains("تقييم المتجر", StringComparison.Ordinal));

    internal static object ToExceptionSummary(SalesExceptionRequest r) => new
    {
        r.Id,
        r.CityValue,
        r.CityName,
        r.CustomerId,
        r.CustomerName,
        r.CustomerPhone,
        r.SalesRequestId,
        r.RequestingManagerDisplayName,
        r.Status,
        r.TargetApproverType,
        r.RequestedAtUtc,
        r.DecidedAtUtc,
        r.DecisionNote,
        r.AssignmentConsumed,
        r.AssignedEmployeeId,
        r.AssignedEmployeeName,
        r.AssignedAtUtc,
        r.AssignedByManagerUserName,
        isWrongSale = IsWrongSaleReason(r.Reason),
        requestKind = IsWrongSaleReason(r.Reason) ? "WrongSale" : "Hold",
        r.ReadAtUtc,
        isUnread = r.ReadAtUtc is null,
        r.BranchManagerReviewedAtUtc,
        r.BranchManagerReviewerUserName,
        r.BranchManagerReviewerDisplayName,
        r.BranchManagerReviewNote,
        isBranchManagerReviewed = r.BranchManagerReviewedAtUtc is not null
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
        r.ReadAtUtc,
        isUnread = r.ReadAtUtc is null,
        r.DecidedAtUtc,
        r.DecisionMakerUserName,
        r.DecisionMakerDisplayName,
        r.DecisionNote,
        r.BranchCustomerNotePosted,
        r.AssignmentConsumed,
        r.AssignedEmployeeId,
        r.AssignedEmployeeName,
        r.AssignedAtUtc,
        isWrongSale = IsWrongSaleReason(r.Reason),
        requestKind = IsWrongSaleReason(r.Reason) ? "WrongSale" : "Hold",
        r.AssignedByManagerUserName,
        r.BranchManagerReviewedAtUtc,
        r.BranchManagerReviewerUserName,
        r.BranchManagerReviewerDisplayName,
        r.BranchManagerReviewNote,
        isBranchManagerReviewed = r.BranchManagerReviewedAtUtc is not null
    };

    internal static object ToWaiverSummary(CustomerWaiverRequest r) => new
    {
        r.Id,
        r.CityValue,
        r.CityName,
        r.CustomerId,
        r.CustomerName,
        r.CustomerPhone,
        r.BranchManagerDisplayName,
        r.Note,
        r.Status,
        r.RequestedAtUtc,
        r.DecidedAtUtc,
        r.ApprovedAmount,
        r.PaymentPosted
    };

    internal static object ToWaiverDetail(CustomerWaiverRequest r) => new
    {
        r.Id,
        r.CityValue,
        r.CityName,
        r.CustomerId,
        r.CustomerName,
        r.CustomerPhone,
        r.BranchManagerUserName,
        r.BranchManagerDisplayName,
        r.BranchManagerUserId,
        r.Note,
        r.Status,
        r.RequestedAtUtc,
        r.DecidedAtUtc,
        r.DecisionMakerUserName,
        r.DecisionMakerDisplayName,
        r.DecisionNote,
        r.ApprovedAmount,
        r.PaymentPosted,
        r.AccountantNotified
    };
}
