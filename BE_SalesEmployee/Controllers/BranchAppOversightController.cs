using System.Globalization;
using System.Text;
using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// «ادارة الفرع» — تطبيق إدارة الفرع tabs on the dashboard for المدير / مسؤول المراقبين /
/// مسؤول لجنة المراقبة / مساعد مسؤول لجنة المراقبة. One province per call; committee
/// members are limited to their assigned provinces.
/// </summary>
[Route("api/director/branch-app")]
[ApiController]
[Authorize(Policy = SalesPolicies.DirectorOrMonitor)]
public sealed class BranchAppOversightController : ControllerBase
{
    public const string WaiverDecision = "تنازل";

    private readonly ISalesManagerBranchAggregator _aggregator;
    private readonly CustomerWaiverService _waivers;
    private readonly SalesExceptionService _exceptions;

    public BranchAppOversightController(
        ISalesManagerBranchAggregator aggregator,
        CustomerWaiverService waivers,
        SalesExceptionService exceptions)
    {
        _aggregator = aggregator;
        _waivers = waivers;
        _exceptions = exceptions;
    }

    /// <summary>Committee accounts only see their own provinces; director/supervisor see all.</summary>
    public static bool CanAccessCity(GatewayUser actor, string? cityValue) =>
        !string.IsNullOrWhiteSpace(cityValue)
        && (!SalesRoles.IsCommittee(actor.UserType) || actor.AllowsFilterCity(cityValue));

    public static string ActorDisplay(GatewayUser actor)
    {
        var name = string.IsNullOrWhiteSpace(actor.UserName) ? "الإدارة" : actor.UserName.Trim();
        var role = (actor.UserType ?? "").Trim();
        return role.Length == 0 ? name : $"{name} ({role})";
    }

    private IActionResult? Guard(GatewayUser actor, string? cityValue)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        if (!CanAccessCity(actor, cityValue))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه المحافظة غير مسندة لحسابك." });
        return null;
    }

    private async Task<IActionResult> ForwardGet(string? cityValue, string companyPath, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var deny = Guard(actor, cityValue);
        if (deny != null) return deny;
        var (status, payload) = await _aggregator.GetOneAsync(actor, cityValue!, companyPath, ct);
        return StatusCode(status, payload);
    }

    private static string D(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime Today => DateTime.UtcNow.AddHours(3).Date;

    // ── 1. أسماء قانونية ─────────────────────────────────────────────

    [HttpGet("legal/weak-week-payers")]
    public Task<IActionResult> WeakWeekPayers([FromQuery] string? cityValue, CancellationToken ct) =>
        ForwardGet(cityValue, DirectorLegalNamesPaths.WeakWeekPayers(), ct);

    public sealed class DecideBody
    {
        public int CustomerId { get; set; }
        public string? DecisionType { get; set; }
        public string? Note { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
    }

    /// <summary>متواصل / قانونية / وهمي → branch decision; تنازل → waiver request (same as the app).</summary>
    [HttpPost("legal/decide")]
    public async Task<IActionResult> Decide([FromQuery] string? cityValue, [FromBody] DecideBody? body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var deny = Guard(actor, cityValue);
        if (deny != null) return deny;
        if (body is null || body.CustomerId <= 0 || string.IsNullOrWhiteSpace(body.DecisionType))
            return BadRequest(new { message = "يجب تحديد الزبون ونوع القرار" });

        var type = body.DecisionType.Trim();
        if (string.Equals(type, WaiverDecision, StringComparison.Ordinal))
        {
            var branches = await _aggregator.BranchesAsync(ct);
            var city = branches.FirstOrDefault(b => string.Equals(b.Value, cityValue!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (city is null)
                return NotFound(new { message = "المحافظة غير موجودة." });

            var requester = new GatewayUser
            {
                UserID = actor.UserID,
                UserName = ActorDisplay(actor),
                UserType = actor.UserType,
                CityValue = city.Value,
                CityName = city.Name,
            };
            var result = await _waivers.CreateAsync(requester, new CreateCustomerWaiverInput
            {
                CityValue = city.Value,
                CityName = city.Name,
                CustomerId = body.CustomerId,
                CustomerName = body.CustomerName,
                CustomerPhone = body.CustomerPhone,
                Note = body.Note,
            }, ct, trustedOversight: true);

            return result.Outcome switch
            {
                CustomerWaiverOutcome.Success => Ok(DelegatedManagerController.ToWaiverDetail(result.Request!)),
                CustomerWaiverOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
                CustomerWaiverOutcome.Conflict => Conflict(new { message = result.Error }),
                CustomerWaiverOutcome.NotFound => NotFound(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error }),
            };
        }

        var json = JsonSerializer.Serialize(new
        {
            customerId = body.CustomerId,
            decisionType = type,
            note = body.Note,
            actorUserId = actor.UserID,
            actorName = string.IsNullOrWhiteSpace(actor.UserName) ? "الإدارة" : actor.UserName.Trim(),
            actorRole = actor.UserType,
        });
        var (status, payload) = await _aggregator.SendContentAsync(
            actor, cityValue!, "branch-oversight/decide", HttpMethod.Post,
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        return StatusCode(status, payload);
    }

    // ── 2. الزبائن المتدنية ─────────────────────────────────────────

    [HttpGet("declining")]
    public Task<IActionResult> Declining([FromQuery] string? cityValue, CancellationToken ct) =>
        ForwardGet(cityValue, "branch-oversight/customers/declining", ct);

    // ── 3. المبيعات الخاطئة ─────────────────────────────────────────

    [HttpGet("wrong-sales")]
    public async Task<IActionResult> WrongSales(
        [FromQuery] string? cityValue,
        [FromQuery] bool reviewed = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        var actor = TokenService.FromPrincipal(User);
        var deny = Guard(actor, cityValue);
        if (deny != null) return deny;
        var (p, size) = PagingDefaults.Normalize(page, pageSize);
        var result = await _exceptions.ListWrongSalesForBranchManagerAsync(cityValue!.Trim(), reviewed, p, size, ct);
        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            reviewed,
            items = result.Items.Select(DelegatedManagerController.ToExceptionSummary),
        });
    }

    [HttpGet("wrong-sales/{id:guid}")]
    public async Task<IActionResult> WrongSale(Guid id, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (outcome, detail, error) = await _exceptions.GetEnrichedDetailAsync(id, ct);
        if (outcome == SalesExceptionOutcome.NotFound || detail is null)
            return NotFound(new { message = error ?? "المبيع الخاطئ غير موجود" });
        if (!CanAccessCity(actor, detail.Request.CityValue))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه المحافظة غير مسندة لحسابك." });
        if (outcome != SalesExceptionOutcome.Success)
            return BadRequest(new { message = error ?? "تعذر تحميل التفاصيل" });

        return Ok(new
        {
            request = DelegatedManagerController.ToExceptionDetail(detail.Request),
            customerProfile = detail.CustomerProfile,
            review = detail.Review,
            reviewError = detail.ReviewError,
            decisionHistory = detail.Audit.Select(a => new
            {
                a.ActorDisplayName,
                a.ActorRole,
                a.PreviousStatus,
                a.NewStatus,
                a.DecisionNote,
                a.CreatedAtUtc,
            }),
        });
    }

    public sealed class ReviewBody
    {
        public string? Note { get; set; }
    }

    [HttpPost("wrong-sales/{id:guid}/review")]
    public async Task<IActionResult> ReviewWrongSale(Guid id, [FromBody] ReviewBody? body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (outcome, detail, error) = await _exceptions.GetEnrichedDetailAsync(id, ct);
        if (outcome == SalesExceptionOutcome.NotFound || detail is null)
            return NotFound(new { message = error ?? "المبيع الخاطئ غير موجود" });
        if (!CanAccessCity(actor, detail.Request.CityValue))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه المحافظة غير مسندة لحسابك." });

        var result = await _exceptions.MarkBranchManagerReviewedAsync(
            id, actor.UserName, ActorDisplay(actor), body?.Note, ct);
        return result.Outcome switch
        {
            SalesExceptionOutcome.Success => Ok(DelegatedManagerController.ToExceptionDetail(result.Request!)),
            SalesExceptionOutcome.NotFound => NotFound(new { message = result.Error }),
            SalesExceptionOutcome.Conflict => Conflict(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error }),
        };
    }

    // ── 4. التقييم ───────────────────────────────────────────────────

    [HttpGet("catalog")]
    public Task<IActionResult> Catalog([FromQuery] string? cityValue, CancellationToken ct) =>
        ForwardGet(cityValue, "sales-manager/stats/catalog", ct);

    [HttpGet("ratings/trend")]
    public Task<IActionResult> RatingTrend(
        [FromQuery] string? cityValue,
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var from = (fromDate ?? Today.AddDays(-30)).Date;
        var to = (toDate ?? Today).Date;
        return ForwardGet(cityValue, DelegatedManager.DelegatedManagerStatsPaths.CompanyTrend(scope, id, from, to), ct);
    }

    [HttpGet("ratings/customers")]
    public Task<IActionResult> RatingCustomers(
        [FromQuery] string? cityValue,
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] string? level,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(level))
            return Task.FromResult<IActionResult>(BadRequest(new { message = "يجب تحديد مستوى التقييم." }));
        var from = (fromDate ?? Today.AddDays(-30)).Date;
        var to = (toDate ?? Today).Date;
        return ForwardGet(
            cityValue,
            DelegatedManager.DelegatedManagerStatsPaths.CompanyRatingCustomers(scope, id, level, from, to, 500, 0, null),
            ct);
    }

    // ── 5. طلبات التنازل ─────────────────────────────────────────────

    [HttpGet("waivers")]
    public async Task<IActionResult> Waivers(
        [FromQuery] string? cityValue,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var actor = TokenService.FromPrincipal(User);
        var deny = Guard(actor, cityValue);
        if (deny != null) return deny;
        var result = await _waivers.ListAsync(new CustomerWaiverQuery
        {
            Page = page <= 0 ? 1 : page,
            PageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200),
            Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
            CityValue = cityValue!.Trim(),
        }, ct);
        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            total = result.TotalCount,
            items = result.Items.Select(DelegatedManagerController.ToWaiverSummary),
        });
    }

    // ── 6. سجل القرارات ──────────────────────────────────────────────

    [HttpGet("decisions")]
    public Task<IActionResult> Decisions(
        [FromQuery] string? cityValue,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var from = (fromDate ?? Today.AddDays(-30)).Date;
        var to = (toDate ?? Today).Date;
        return ForwardGet(cityValue, DirectorLegalNamesPaths.Decisions(null, from, to, null), ct);
    }

    // ── 7. المبيعات ──────────────────────────────────────────────────

    [HttpGet("sales")]
    public Task<IActionResult> Sales([FromQuery] string? cityValue, CancellationToken ct) =>
        ForwardGet(cityValue, "branch-oversight/new-sales", ct);

    [HttpGet("sales/{saleId:int}")]
    public Task<IActionResult> Sale(int saleId, [FromQuery] string? cityValue, CancellationToken ct) =>
        ForwardGet(cityValue, $"branch-oversight/new-sales/{saleId}", ct);

    // ── 8. التسديدات ─────────────────────────────────────────────────

    public static string PaymentsPath(DateTime from, DateTime to, int listId, string? q)
    {
        var text = string.IsNullOrWhiteSpace(q) ? "null" : Uri.EscapeDataString(q.Trim());
        return $"CustomersPayments/CustomersPayments_GetAll/{D(from)}&&{D(to)}&&{Math.Max(0, listId)}&&{text}";
    }

    [HttpGet("payments")]
    public Task<IActionResult> Payments(
        [FromQuery] string? cityValue,
        [FromQuery] int listId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        if (listId <= 0)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "يجب اختيار القائمة." }));
        var from = (fromDate ?? Today).Date;
        var to = (toDate ?? Today).Date;
        if (to < from)
            (from, to) = (to, from);
        if ((to - from).TotalDays > 62)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "أقصى فترة شهران." }));
        return ForwardGet(cityValue, PaymentsPath(from, to, listId, q), ct);
    }
}
