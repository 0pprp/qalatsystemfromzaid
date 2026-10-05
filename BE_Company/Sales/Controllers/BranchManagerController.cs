using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BE_Company.Sales.Controllers
{
    [Authorize]
    [Route("api/branch-manager")]
    [ApiController]
    public sealed class BranchManagerController : ControllerBase
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly SalesIdentityService _identity;
        private readonly IBranchManagerNewSalesService _newSales;
        private readonly ISalesRequestService _requests;
        private readonly IBranchManagerComplaintForwarder _complaints;
        private readonly ICustomerWaiverGatewayForwarder _waivers;
        private readonly IBranchManagerStatsService _stats;
        private readonly IConfiguration _configuration;

        public BranchManagerController(
            SalesDevelopmentGuard guard,
            SalesIdentityService identity,
            IBranchManagerNewSalesService newSales,
            ISalesRequestService requests,
            IBranchManagerComplaintForwarder complaints,
            ICustomerWaiverGatewayForwarder waivers,
            IBranchManagerStatsService stats,
            IConfiguration configuration)
        {
            _guard = guard;
            _identity = identity;
            _newSales = newSales;
            _requests = requests;
            _complaints = complaints;
            _waivers = waivers;
            _stats = stats;
            _configuration = configuration;
        }

        private async Task<IActionResult?> GateAsync(CancellationToken ct)
        {
            var (ok, reason) = await _guard.CanRunSalesModuleAsync(ct);
            if (!ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return Unauthorized();
            }

            if (!SalesRoles.IsBranchManager(identity.UserType))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه الواجهة لمدير الفرع فقط" });
            }

            return null;
        }

        [HttpGet("new-sales")]
        public async Task<IActionResult> ListNewSales([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _newSales.ListAsync(unreadOnly, ct));
        }

        [HttpGet("new-sales/unread-count")]
        public async Task<IActionResult> UnreadCount(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(new { count = await _newSales.CountUnreadAsync(ct) });
        }

        [HttpGet("new-sales/{saleId:int}")]
        public async Task<IActionResult> GetNewSale(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var row = await _newSales.GetAsync(saleId, ct);
            return row == null ? NotFound(new { message = "المبيع غير موجود" }) : Ok(row);
        }

        [HttpPost("new-sales/{saleId:int}/mark-read")]
        public async Task<IActionResult> MarkRead(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            await _newSales.MarkReadAsync(saleId, ct);
            return Ok(new { ok = true });
        }

        [HttpPost("new-sales/mark-all-read")]
        public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var marked = await _newSales.MarkAllReadAsync(ct);
            return Ok(new { marked });
        }

        [HttpGet("stats/catalog")]
        public async Task<IActionResult> StatsCatalog(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetCatalogAsync(ct));
        }

        [HttpGet("stats/trend")]
        public async Task<IActionResult> StatsTrend(
            [FromQuery] string? scope,
            [FromQuery] int id,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var today = DateTime.UtcNow.AddHours(3).Date;
            var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (toDate ?? today).Date;
            return Ok(await _stats.GetTrendAsync(scope ?? "branch", id, from, to, ct));
        }

        /// <summary>زبائن التقييم حسب النطاق والمستوى — للضغط على المعدل/الحالات في تطبيق مدير الفرع.</summary>
        [HttpGet("stats/rating-customers")]
        public async Task<IActionResult> StatsRatingCustomers(
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
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var today = DateTime.UtcNow.AddHours(3).Date;
            var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (toDate ?? today).Date;
            return Ok(await _stats.GetRatingCustomersAsync(
                scope ?? "branch", id, level, from, to, ct, take, skip, q));
        }

        [HttpPost("sales-requests")]
        public async Task<IActionResult> CreateSalesRequest([FromBody] SalesRequestCreateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            try
            {
                return Ok(await _requests.CreateAsync(identity, body ?? new SalesRequestCreateDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        public sealed class DelegatedManagerRequestBody
        {
            public string? Message { get; set; }
            public string? Subject { get; set; }
        }

        [HttpPost("delegated-manager-requests")]
        public async Task<IActionResult> CreateDelegatedManagerRequest(
            [FromBody] DelegatedManagerRequestBody? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var message = (body?.Message ?? "").Trim();
            if (message.Length < 10)
            {
                return BadRequest(new { message = "اكتب 10 أحرف على الأقل" });
            }

            if (message.Length > 2000)
            {
                return BadRequest(new { message = "الحد الأقصى 2000 حرف" });
            }

            var subject = string.IsNullOrWhiteSpace(body?.Subject)
                ? "طلب من مدير الفرع"
                : body!.Subject!.Trim();

            var forwarded = await _complaints.ForwardAsync(new BranchManagerComplaintForwardRequest
            {
                Message = message,
                Subject = subject,
                CityValue = identity.BranchId,
                CityName = identity.BranchName,
                SenderUserId = identity.EmployeeId.ToString(),
                SenderUserName = identity.EmployeeName,
                SenderDisplayName = string.IsNullOrWhiteSpace(identity.EmployeeName)
                    ? "مدير فرع"
                    : identity.EmployeeName,
                SenderRole = SalesRoles.UserTypeBranchManager,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    branchId = identity.BranchId,
                    configuredBranch = _configuration["SalesManagement:BranchId"]
                })
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر إرسال الطلب" });
            }

            return Ok(new { message = "تم إرسال الطلب إلى المدير المفوض", detail = forwarded.ResponseBody });
        }

        public sealed class WaiverCreateBody
        {
            public int CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? CustomerPhone { get; set; }
            public string? Note { get; set; }
        }

        [HttpPost("waivers")]
        public async Task<IActionResult> CreateWaiver([FromBody] WaiverCreateBody? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var note = (body?.Note ?? "").Trim();
            if (string.IsNullOrWhiteSpace(note))
            {
                return BadRequest(new { message = "ملاحظة مدير الفرع إجبارية" });
            }

            if (body is null || body.CustomerId <= 0)
            {
                return BadRequest(new { message = "معرّف الزبون مطلوب" });
            }

            var forwarded = await _waivers.CreateAsync(new CustomerWaiverCreateForwardRequest
            {
                CityValue = identity.BranchId,
                CityName = identity.BranchName,
                CustomerId = body.CustomerId,
                CustomerName = body.CustomerName,
                CustomerPhone = body.CustomerPhone,
                Note = note,
                RequesterUserId = identity.EmployeeId.ToString(),
                RequesterUserName = identity.EmployeeName,
                RequesterDisplayName = string.IsNullOrWhiteSpace(identity.EmployeeName)
                    ? "مدير فرع"
                    : identity.EmployeeName,
                Role = SalesRoles.UserTypeBranchManager
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر إرسال طلب التنازل" });
            }

            return Content(forwarded.ResponseBody ?? "{}", "application/json");
        }

        [HttpGet("waivers")]
        public async Task<IActionResult> ListWaivers(
            [FromQuery] string? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var forwarded = await _waivers.ListAsync(new CustomerWaiverListForwardRequest
            {
                Status = status,
                CityValue = identity.BranchId,
                Page = page,
                PageSize = pageSize,
                RequesterUserName = identity.EmployeeName
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر تحميل طلبات التنازل" });
            }

            return Content(forwarded.ResponseBody ?? "{}", "application/json");
        }
    }
}
