using BE_Company.Sales.Authorization;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Filtering
{
    [Authorize(Policy = SalesPolicies.SalesFilterEmployee)]
    [Route("api/sales-filter")]
    [ApiController]
    public class SalesFilterController : ControllerBase
    {
        private readonly ISalesFilterService _service;
        private readonly SalesIdentityService _identity;

        public SalesFilterController(ISalesFilterService service, SalesIdentityService identity)
        {
            _service = service;
            _identity = identity;
        }

        private SalesIdentity RequireActor() =>
            _identity.FromAuthenticatedUser()
            ?? throw new SalesCompleteException(StatusCodes.Status401Unauthorized, "غير مصرح.");

        [HttpGet("me/cities")]
        public async Task<IActionResult> MyCities(CancellationToken ct)
        {
            try
            {
                return Ok(await _service.MyCitiesAsync(RequireActor(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("requests")]
        public async Task<IActionResult> List(
            [FromQuery] string? city,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            CancellationToken ct = default)
        {
            try
            {
                return Ok(await _service.ListAsync(RequireActor(), city, status, search, page, pageSize, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("requests/{id:int}")]
        public async Task<IActionResult> Get(int id, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.GetAsync(RequireActor(), id, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("requests/{id:int}/hold")]
        public async Task<IActionResult> Hold(int id, [FromBody] SalesFilterNoteDTO? body, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.HoldAsync(RequireActor(), id, body?.Note, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("requests/{id:int}/ready")]
        public async Task<IActionResult> Ready(int id, [FromBody] SalesFilterNoteDTO? body, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.ReadyAsync(RequireActor(), id, body?.Note, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("requests/{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, [FromBody] SalesFilterRejectDTO? body, CancellationToken ct)
        {
            try
            {
                return Ok(await _service.RejectAsync(RequireActor(), id, body?.Reason, body?.Note, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }
    }
}
