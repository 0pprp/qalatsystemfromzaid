using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>
/// المحاسب الرئيسي «متابعة المندوبين» — read-only, on the accountant's own branch
/// (called directly via LinkCity with the branch JWT, so only this branch's lists are visible).
/// Same services as <see cref="DirectorDelegateFollowupController"/>; no rating writes.
/// </summary>
[ApiController]
[Route("api/accountant/delegate-followup")]
[Authorize(Policy = SalesPolicies.MainAccountant)]
public sealed class AccountantDelegateFollowupController : ControllerBase
{
    private readonly IDirectorDelegateFollowupService _svc;
    private readonly ICustomerShopLocationService _locations;
    private readonly IBranchManagerStatsService _stats;
    private readonly SalesDevelopmentGuard _guard;

    public AccountantDelegateFollowupController(
        IDirectorDelegateFollowupService svc,
        ICustomerShopLocationService locations,
        IBranchManagerStatsService stats,
        SalesDevelopmentGuard guard)
    {
        _svc = svc;
        _locations = locations;
        _stats = stats;
        _guard = guard;
    }

    [HttpGet("lists")]
    public async Task<IActionResult> Lists(CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;

        return Ok(await _svc.GetActiveListsAsync(ct));
    }

    [HttpGet("lists/{listId:int}/summary")]
    public async Task<IActionResult> ListSummary(int listId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;

        var row = await _svc.GetListSummaryAsync(listId, ct);
        return row is null ? NotFound(new { message = "القائمة غير موجودة" }) : Ok(row);
    }

    [HttpGet("lists/{listId:int}/customers")]
    public async Task<IActionResult> ListCustomers(int listId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;

        return Ok(await _svc.GetListCustomersAsync(listId, ct));
    }

    /// <summary>«طلبات بيع المندوب»: every sale request this list's delegate sent + outcome/evaluation/new-customer stats.</summary>
    [HttpGet("lists/{listId:int}/sales-orders")]
    public async Task<IActionResult> ListSalesOrders(
        int listId, [FromServices] IDelegateSalesOrdersService orders, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;

        return Ok(await orders.GetAsync(listId, ct));
    }

    [HttpGet("lists/{listId:int}/locations")]
    public async Task<IActionResult> ListLocations(int listId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;

        var row = await _locations.GetListLocationsAsync(listId, ct);
        return row is null ? NotFound(new { message = "القائمة غير موجودة" }) : Ok(row);
    }

    [HttpGet("lists/{listId:int}/customers/{customerId:int}/payment-locations")]
    public async Task<IActionResult> CustomerPaymentLocations(int listId, int customerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;

        var row = await _locations.GetCustomerPaymentLocationsAsync(listId, customerId, ct);
        return row is null ? NotFound(new { message = "الزبون غير موجود في هذه القائمة" }) : Ok(row);
    }

    [HttpGet("lists/{listId:int}/board-operator-rating")]
    public async Task<IActionResult> GetBoardOperatorRating(int listId, [FromQuery] string? slot, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;

        return Ok(await _svc.GetBoardOperatorRatingAsync(listId, null, ct, slot));
    }

    /// <summary>«تقييم المحاسب الرئيسي» for today — always the accountant slot, rater = this accountant.</summary>
    [HttpPost("lists/{listId:int}/board-operator-rating")]
    public async Task<IActionResult> UpsertBoardOperatorRating(
        int listId,
        [FromBody] DirectorDelegateFollowupUpsertBoardOperatorRatingRequest? body,
        CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null) return gate;
        if (body is null) return BadRequest(new { message = "بيانات التقييم مطلوبة" });

        var userName = User.FindFirst("UserName")?.Value ?? User.Identity?.Name;
        var (ok, error, row) = await _svc.UpsertBoardOperatorRatingAsync(
            listId, body.Score, body.Note, userName, ct, DirectorDelegateFollowupService.BoardOperatorSlotAccountant);
        return ok ? Ok(row) : BadRequest(new { message = error ?? "تعذر حفظ التقييم" });
    }

    [HttpGet("list-payments-sum")]
    public async Task<IActionResult> ListPaymentsSum(
        [FromQuery] int listId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct = default)
    {
        var gate = Gate();
        if (gate != null) return gate;
        if (listId <= 0) return BadRequest(new { message = "معرف القائمة مطلوب." });

        var yesterday = DateTime.UtcNow.AddHours(3).Date.AddDays(-1);
        return Ok(await _stats.GetListPaymentsSumAsync(
            listId, (fromDate ?? yesterday).Date, (toDate ?? yesterday).Date, ct));
    }

    private IActionResult? Gate() =>
        _guard.CanRunSalesModule(out var reason)
            ? null
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
}
