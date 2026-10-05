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
        private readonly IConfiguration _configuration;

        public SalesFilterController(
            ISalesFilterService service,
            SalesIdentityService identity,
            IConfiguration configuration)
        {
            _service = service;
            _identity = identity;
            _configuration = configuration;
        }

        private SalesIdentity RequireActor() =>
            _identity.FromAuthenticatedUser()
            ?? throw new SalesCompleteException(StatusCodes.Status401Unauthorized, "غير مصرح.");

        private IActionResult? IfDisabled()
        {
            if (_configuration.GetValue("SalesManagement:FilterAppEnabled", false))
            {
                return null;
            }

            return StatusCode(StatusCodes.Status410Gone, new
            {
                message = "تم إيقاف تطبيق الفلترة. الطلبات تظهر مباشرة في تطبيق المبيعات — طلبات البيع."
            });
        }

        [HttpGet("me/cities")]
        public async Task<IActionResult> MyCities(CancellationToken ct)
        {
            if (IfDisabled() is { } disabled) return disabled;
            try
            {
                return Ok(await _service.MyCitiesAsync(RequireActor(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales-employees")]
        public async Task<IActionResult> SalesEmployees([FromQuery] string? city, CancellationToken ct = default)
        {
            if (IfDisabled() is { } disabled) return disabled;
            try
            {
                return Ok(await _service.ListSalesEmployeesAsync(RequireActor(), city, ct));
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
            [FromQuery] int? targetEmployeeId = null,
            CancellationToken ct = default)
        {
            if (IfDisabled() is { } disabled) return disabled;
            try
            {
                return Ok(await _service.ListAsync(
                    RequireActor(), city, status, search, page, pageSize, targetEmployeeId, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("counts")]
        public async Task<IActionResult> Counts(
            [FromQuery] string? city,
            [FromQuery] int? targetEmployeeId = null,
            CancellationToken ct = default)
        {
            if (IfDisabled() is { } disabled) return disabled;
            try
            {
                return Ok(await _service.CountsAsync(RequireActor(), city, targetEmployeeId, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("requests/{id:int}")]
        public async Task<IActionResult> Get(int id, CancellationToken ct)
        {
            if (IfDisabled() is { } disabled) return disabled;
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
            if (IfDisabled() is { } disabled) return disabled;
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
            if (IfDisabled() is { } disabled) return disabled;
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
            if (IfDisabled() is { } disabled) return disabled;
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
