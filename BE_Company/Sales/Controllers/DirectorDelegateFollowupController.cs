using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>Branch APIs for director/monitor «متابعة المندوبين» (proxied via gateway).</summary>
[Authorize(Policy = SalesPolicies.SalesManager)]
[Route("api/sales-manager/director-delegate-followup")]
[ApiController]
public sealed class DirectorDelegateFollowupController : ControllerBase
{
    private readonly IDirectorDelegateFollowupService _svc;
    private readonly ICustomerShopLocationService _locations;
    private readonly SalesDevelopmentGuard _guard;

    public DirectorDelegateFollowupController(
        IDirectorDelegateFollowupService svc,
        ICustomerShopLocationService locations,
        SalesDevelopmentGuard guard)
    {
        _svc = svc;
        _locations = locations;
        _guard = guard;
    }

    [HttpGet("lists")]
    public async Task<IActionResult> Lists(CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _svc.GetActiveListsAsync(ct));
    }

    [HttpGet("lists/{listId:int}/summary")]
    public async Task<IActionResult> ListSummary(int listId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        var row = await _svc.GetListSummaryAsync(listId, ct);
        if (row is null)
        {
            return NotFound(new { message = "القائمة غير موجودة" });
        }

        return Ok(row);
    }

    [HttpGet("lists/{listId:int}/customers")]
    public async Task<IActionResult> ListCustomers(int listId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _svc.GetListCustomersAsync(listId, ct));
    }

    /// <summary>Map: shop location (sales GPS, else estimated from payment GPS) per non-zeroed customer.</summary>
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
        if (gate != null)
        {
            return gate;
        }

        var row = await _locations.GetListLocationsAsync(listId, ct);
        if (row is null)
        {
            return NotFound(new { message = "القائمة غير موجودة" });
        }

        return Ok(row);
    }

    /// <summary>Map: every recent payment GPS point of one customer, tagged shop / office / other.</summary>
    [HttpGet("lists/{listId:int}/customers/{customerId:int}/payment-locations")]
    public async Task<IActionResult> CustomerPaymentLocations(int listId, int customerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        var row = await _locations.GetCustomerPaymentLocationsAsync(listId, customerId, ct);
        if (row is null)
        {
            return NotFound(new { message = "الزبون غير موجود في هذه القائمة" });
        }

        return Ok(row);
    }

    /// <summary>Navbar «بحث الزبائن» — name/phone search with payoff status, rating and list.</summary>
    [HttpGet("customers/search")]
    public async Task<IActionResult> SearchCustomers(
        [FromQuery] string? q,
        [FromQuery] int take = 25,
        [FromQuery] bool includeFinished = false,
        CancellationToken ct = default)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        var query = (q ?? "").Trim();
        if (query.Length < 2)
        {
            return BadRequest(new { message = "اكتب حرفين على الأقل للبحث" });
        }

        return Ok(await _svc.SearchCustomersAsync(query, take, includeFinished, ct));
    }

    [HttpGet("lists/{listId:int}/board-operator-rating")]
    public async Task<IActionResult> GetBoardOperatorRating(
        int listId,
        [FromQuery] string? slot,
        CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        var row = await _svc.GetBoardOperatorRatingAsync(listId, null, ct, slot);
        return Ok(row);
    }

    [HttpPost("lists/{listId:int}/board-operator-rating")]
    public async Task<IActionResult> UpsertBoardOperatorRating(
        int listId,
        [FromBody] DirectorDelegateFollowupUpsertBoardOperatorRatingRequest? body,
        CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        if (body is null)
        {
            return BadRequest(new { message = "بيانات التقييم مطلوبة" });
        }

        // «تقييم المحاسب الرئيسي» is written only via api/accountant/delegate-followup.
        if (DirectorDelegateFollowupService.NormalizeBoardOperatorSlot(body.Slot)
            == DirectorDelegateFollowupService.BoardOperatorSlotAccountant)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "تقييم المحاسب الرئيسي متاح للمحاسب الرئيسي فقط" });
        }

        var userName = User.FindFirst("UserName")?.Value
            ?? User.Identity?.Name;
        var (ok, error, row) = await _svc.UpsertBoardOperatorRatingAsync(
            listId, body.Score, body.Note, userName, ct, body.Slot);
        if (!ok)
        {
            return BadRequest(new { message = error ?? "تعذر حفظ التقييم" });
        }

        return Ok(row);
    }

    private IActionResult? Gate()
    {
        if (!_guard.CanRunSalesModule(out var reason))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
        }

        return null;
    }
}
