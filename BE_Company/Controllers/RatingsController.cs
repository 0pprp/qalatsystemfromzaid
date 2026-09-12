using BE_Company.Sales.Rating;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class RatingsController : ControllerBase
{
    private readonly IRatingService _ratingService;

    public RatingsController(IRatingService ratingService)
    {
        _ratingService = ratingService;
    }

    [HttpPost("customers/summary")]
    public async Task<ActionResult<IReadOnlyList<CustomerRatingSummaryDto>>> CustomersSummary(
        [FromBody] IdsRequest request, CancellationToken ct)
    {
        var result = await _ratingService.GetCustomerSummariesAsync(request.Ids ?? [], ct);
        return Ok(result);
    }

    [HttpGet("customers/{customerId:int}")]
    public async Task<ActionResult<CustomerRatingDetailDto>> CustomerDetail(int customerId, CancellationToken ct)
    {
        var result = await _ratingService.GetCustomerDetailAsync(customerId, ct);
        if (result is null) return NotFound(new { message = "الزبون غير موجود" });
        return Ok(result);
    }

    [HttpPost("lists/summary")]
    public async Task<ActionResult<IReadOnlyList<ListRatingSummaryDto>>> ListsSummary(
        [FromBody] IdsRequest request, CancellationToken ct)
    {
        var result = await _ratingService.GetListSummariesAsync(request.Ids ?? [], ct);
        return Ok(result);
    }

    [HttpGet("lists/{listId:int}")]
    public async Task<ActionResult<ListRatingDetailDto>> ListDetail(int listId, CancellationToken ct)
    {
        var result = await _ratingService.GetListDetailAsync(listId, ct);
        if (result is null) return NotFound(new { message = "القائمة غير موجودة" });
        return Ok(result);
    }

    public sealed class IdsRequest
    {
        public List<int>? Ids { get; set; }
    }
}
