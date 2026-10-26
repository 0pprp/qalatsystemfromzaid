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
    private readonly SalesDevelopmentGuard _guard;

    public DirectorDelegateFollowupController(
        IDirectorDelegateFollowupService svc,
        SalesDevelopmentGuard guard)
    {
        _svc = svc;
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

    [HttpGet("lists/{listId:int}/board-operator-rating")]
    public async Task<IActionResult> GetBoardOperatorRating(int listId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        var row = await _svc.GetBoardOperatorRatingAsync(listId, null, ct);
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

        var userName = User.FindFirst("UserName")?.Value
            ?? User.Identity?.Name;
        var (ok, error, row) = await _svc.UpsertBoardOperatorRatingAsync(
            listId, body.Score, body.Note, userName, ct);
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
