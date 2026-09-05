using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers
{
    [Authorize(Policy = SalesPolicies.SalesManager)]
    [Route("api/sales-manager")]
    [ApiController]
    public class SalesManagerController : ControllerBase
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly SalesIdentityService _identity;
        private readonly SalesManagerQueryService _query;
        private readonly ISalesRequestService _requests;
        private readonly IGlobalCustomerSearchService _search;
        private readonly IIraqClock _clock;

        private readonly ISalesShopProfileService _shops;

        public SalesManagerController(
            SalesDevelopmentGuard guard,
            SalesIdentityService identity,
            SalesManagerQueryService query,
            ISalesRequestService requests,
            IGlobalCustomerSearchService search,
            IIraqClock clock,
            ISalesShopProfileService shops)
        {
            _guard = guard;
            _identity = identity;
            _query = query;
            _requests = requests;
            _search = search;
            _clock = clock;
            _shops = shops;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _query.DashboardAsync(ct));
        }

        [HttpGet("employees")]
        public async Task<IActionResult> Employees([FromQuery] string? cityValue, [FromQuery] string? shiftStatus, [FromQuery] string? locationStatus, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var rows = await _query.ListEmployeesAsync(shiftStatus, locationStatus, ct);
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                rows = rows.Where(e => string.Equals(e.CityValue, cityValue, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(rows);
        }

        [HttpGet("employees/{employeeId:int}")]
        public async Task<IActionResult> Employee(int employeeId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var row = await _query.GetEmployeeAsync(employeeId, ct);
            return row == null ? NotFound() : Ok(row);
        }

        [HttpGet("employees/{employeeId:int}/route")]
        public async Task<IActionResult> Route(int employeeId, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                var day = date?.Date ?? IraqTimeService.IraqNow(_clock).Date;
                return Ok(await _query.GetRouteAsync(employeeId, day, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("employees/{employeeId:int}/tracking-events")]
        public async Task<IActionResult> Events(int employeeId, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var day = date?.Date ?? DateTime.UtcNow.Date;
            return Ok(await _query.GetEventsAsync(employeeId, day, ct));
        }

        [HttpGet("sales")]
        public async Task<IActionResult> Sales([FromQuery] int? employeeId, [FromQuery] string? status, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            DateTime? from = date?.Date;
            DateTime? to = from?.AddDays(1);
            var rows = await _query.ListSalesAsync(employeeId, status, from, to, ct);
            return Ok(rows.Select(s => new SalesManagerSaleListItemDTO
            {
                SaleId = s.SaleId,
                CustomerName = s.FullName,
                CustomerPhone = s.Phone,
                EmployeeId = s.EmployeeId,
                EmployeeName = s.UserName,
                CityName = s.CityName,
                BaseSalePrice = s.BaseSalePrice,
                FinalSalePrice = s.FinalSalePrice,
                EvaluationLevel = s.EvaluationLevel,
                EvaluationName = SalesEvaluationLevels.DisplayName(s.EvaluationLevel),
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                CompletedAt = s.CompletedAt
            }));
        }

        [HttpGet("sales/{saleId:int}")]
        public async Task<IActionResult> Sale(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var sale = await _query.GetSaleAsync(saleId, ct);
            return sale == null ? NotFound() : Ok(sale);
        }

        [HttpGet("customers/profile")]
        public async Task<IActionResult> CustomerProfile(
            [FromQuery] int? customerId,
            [FromQuery] string? name,
            [FromQuery] string? phone,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            if (customerId is null or <= 0 && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { message = "حدد الزبون." });
            }

            var profile = await _shops.GetCustomerProfileAsync(customerId, name, phone, ct);
            var identity = _identity.FromAuthenticatedUser();
            if (identity != null)
            {
                profile.CityValue ??= identity.BranchId;
                profile.CityName ??= identity.BranchName;
            }

            return Ok(profile);
        }

        [HttpPost("customers/notes")]
        public async Task<IActionResult> AddCustomerNote([FromBody] SalesCustomerNoteCreateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _shops.AddNoteAsync(body, "SalesManager", identity?.EmployeeName, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales/{saleId:int}/shop-image")]
        public async Task<IActionResult> ShopImage(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var image = await _shops.ReadImageAsync(saleId, ct);
            if (image == null)
            {
                return NotFound();
            }

            var ext = Path.GetExtension(image.Value.FileName).ToLowerInvariant();
            var type = ext == ".png" ? "image/png" : "image/jpeg";
            return File(image.Value.Bytes, type, image.Value.FileName);
        }

        [HttpGet("customers/search")]
        public async Task<IActionResult> SearchCustomers([FromQuery] string? q, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var query = (q ?? string.Empty).Trim();
            if (query.Length < 2)
            {
                return BadRequest(new { message = "اكتب حرفين على الأقل للبحث" });
            }

            return Ok(await _search.SearchAsync(query, ct));
        }

        [HttpGet("sales-requests")]
        public async Task<IActionResult> Requests([FromQuery] string? cityValue, [FromQuery] int? employeeId, [FromQuery] string? status, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            DateTime? from = date?.Date;
            DateTime? to = from?.AddDays(1);
            var rows = await _requests.ListForManagerAsync(status, employeeId, from, to, ct);
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                rows = rows.Where(r => string.Equals(r.CityValue, cityValue, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(rows);
        }

        [HttpGet("sales-requests/{id:int}")]
        public async Task<IActionResult> RequestDetails(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var row = await _requests.GetForManagerAsync(id, ct);
            return row == null ? NotFound() : Ok(row);
        }

        [HttpPost("sales-requests")]
        public async Task<IActionResult> CreateRequest([FromBody] SalesRequestCreateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.CreateAsync(identity!, body, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/assign")]
        public async Task<IActionResult> AssignRequest(int id, [FromBody] SalesRequestAssignDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.AssignAsync(identity!, id, body, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/return")]
        public async Task<IActionResult> ReturnRequest(int id, [FromBody] SalesRequestNoteDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ReturnAsync(identity!, id, body.Note ?? body.Reason ?? string.Empty, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        private async Task<IActionResult?> GateAsync(CancellationToken ct)
        {
            var check = await _guard.CanRunSalesModuleAsync(ct);
            if (!check.Ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = check.Reason });
            }

            if (_identity.FromAuthenticatedUser() == null)
            {
                return Unauthorized();
            }

            return null;
        }
    }
}
