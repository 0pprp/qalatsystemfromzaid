using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers
{
    [Authorize]
    [Route("api/sales")]
    [ApiController]
    public class SalesController : ControllerBase
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly SalesIdentityService _identity;
        private readonly IGlobalCustomerSearchService _search;
        private readonly ISalesInventoryService _inventory;
        private readonly ISalesDraftRepository _drafts;
        private readonly SalesDraftService _draftService;
        private readonly ISalesCompleteService _complete;
        private readonly ISalesShiftService _shifts;
        private readonly ISalesLocationIngestService _locations;
        private readonly ISalesRequestService _requests;

        public SalesController(
            SalesDevelopmentGuard guard,
            SalesIdentityService identity,
            IGlobalCustomerSearchService search,
            ISalesInventoryService inventory,
            ISalesDraftRepository drafts,
            SalesDraftService draftService,
            ISalesCompleteService complete,
            ISalesShiftService shifts,
            ISalesLocationIngestService locations,
            ISalesRequestService requests)
        {
            _guard = guard;
            _identity = identity;
            _search = search;
            _inventory = inventory;
            _drafts = drafts;
            _draftService = draftService;
            _complete = complete;
            _shifts = shifts;
            _locations = locations;
            _requests = requests;
        }

        [Authorize(Policy = SalesPolicies.AnySales)]
        [HttpGet("me")]
        public async Task<ActionResult<SalesMeResponseDTO>> Me(CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            var started = await _shifts.IsShiftStartedAsync(identity.EmployeeId, ct);
            return Ok(new SalesMeResponseDTO
            {
                EmployeeId = identity.EmployeeId,
                EmployeeName = identity.EmployeeName,
                BranchId = identity.BranchId,
                BranchName = identity.BranchName,
                Role = identity.Role,
                IsSalesShiftStarted = started
            });
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("customers/search")]
        public async Task<ActionResult<IEnumerable<SalesCustomerSearchDTO>>> SearchCustomers(
            [FromQuery] string? q,
            CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var query = (q ?? string.Empty).Trim();
            if (query.Length < DemoGlobalCustomerSearchService.MinimumQueryLength)
            {
                return BadRequest(new { message = "اكتب حرفين على الأقل للبحث" });
            }

            var rows = await _search.SearchAsync(query, ct);
            return Ok(rows.Select(r => new
            {
                r.CustomerId,
                r.FullName,
                r.Phone,
                r.Province,
                r.SalePrice,
                r.SourceCityValue,
                r.SourceCityName
            }));
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("inventory")]
        public async Task<ActionResult<IEnumerable<SalesInventoryItemDTO>>> Inventory(CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var rows = await _inventory.GetBranchInventoryAsync(ct);
            return Ok(rows);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost]
        public async Task<ActionResult<SalesDraftDTO>> Create([FromBody] SalesDraftCreateRequestDTO request, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            try
            {
                var created = await _draftService.CreateAsync(
                    request,
                    identity.EmployeeId,
                    identity.EmployeeName,
                    identity.UserType,
                    identity.BranchId,
                    identity.BranchName,
                    ct);
                return Ok(created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<SalesDraftDTO>>> Pending(CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            await _drafts.EnsureSchemaAsync(ct);
            var rows = await _drafts.GetByEmployeeAsync(identity.EmployeeId, ct);
            return Ok(rows);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult<SalesDraftDTO>> GetById(int id, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            await _drafts.EnsureSchemaAsync(ct);
            var row = await _drafts.GetByIdAsync(id, identity.EmployeeId, ct);
            if (row == null)
            {
                return NotFound();
            }

            await _complete.AttachDocumentsAsync(row, identity.EmployeeId, ct);
            return Ok(row);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("{id:int}/complete")]
        public async Task<ActionResult<SalesCompleteResponseDTO>> Complete(int id, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            try
            {
                var result = await _complete.CompleteAsync(id, identity, ct);
                return Ok(result);
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("{id:int}/documents")]
        public async Task<IActionResult> Documents(int id, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            try
            {
                var docs = await _complete.GetDocumentsAsync(id, identity.EmployeeId, ct);
                return Ok(docs);
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("{id:int}/documents/{documentId:int}/download")]
        public async Task<IActionResult> Download(int id, int documentId, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            try
            {
                var file = await _complete.DownloadAsync(id, documentId, identity.EmployeeId, ct);
                return File(file.Bytes, "application/pdf", file.FileName);
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("shifts/start")]
        public async Task<IActionResult> StartShift(CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            var shift = await _shifts.StartAsync(identity, ct);
            return Ok(shift);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("shifts/current")]
        public async Task<IActionResult> CurrentShift(CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            var shift = await _shifts.GetCurrentAsync(identity.EmployeeId, ct);
            if (shift == null)
            {
                return Ok(new { hasActiveShift = false });
            }

            return Ok(shift);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("location/batch")]
        public async Task<IActionResult> LocationBatch([FromBody] SalesLocationBatchRequestDTO request, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            try
            {
                return Ok(await _locations.IngestBatchAsync(identity, request, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("tracking/events")]
        public async Task<IActionResult> TrackingEvent([FromBody] SalesTrackingEventRequestDTO request, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            try
            {
                await _locations.RecordEventAsync(identity, request, ct);
                return Ok(new { saved = true });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("requests")]
        public async Task<IActionResult> MyRequests(CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null) return blocked;
            var identity = _identity.FromAuthenticatedUser();
            if (identity == null) return Unauthorized();
            return Ok(await _requests.ListForEmployeeAsync(identity.EmployeeId, ct));
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("requests/{id:int}")]
        public async Task<IActionResult> MyRequest(int id, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null) return blocked;
            var identity = _identity.FromAuthenticatedUser();
            if (identity == null) return Unauthorized();
            try
            {
                return Ok(await _requests.GetForEmployeeAsync(id, identity.EmployeeId, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("requests/{id:int}/view")]
        public async Task<IActionResult> ViewRequest(int id, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null) return blocked;
            var identity = _identity.FromAuthenticatedUser();
            if (identity == null) return Unauthorized();
            try
            {
                return Ok(await _requests.ViewAsync(id, identity.EmployeeId, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("requests/{id:int}/start-processing")]
        public async Task<IActionResult> StartProcessing(int id, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null) return blocked;
            var identity = _identity.FromAuthenticatedUser();
            if (identity == null) return Unauthorized();
            try
            {
                return Ok(await _requests.StartProcessingAsync(id, identity.EmployeeId, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("requests/{id:int}/reject")]
        public async Task<IActionResult> RejectRequest(int id, [FromBody] SalesRequestRejectDTO body, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null) return blocked;
            var identity = _identity.FromAuthenticatedUser();
            if (identity == null) return Unauthorized();
            try
            {
                return Ok(await _requests.RejectAsync(id, identity.EmployeeId, body.Reason ?? string.Empty, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        private async Task<ActionResult?> BlockIfNotDemo(CancellationToken ct)
        {
            var check = await _guard.CanRunSalesModuleAsync(ct);
            if (!check.Ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = check.Reason });
            }

            return null;
        }
    }
}
