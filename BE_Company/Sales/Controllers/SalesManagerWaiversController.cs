using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers
{
    /// <summary>
    /// Gateway-facing waiver apply + customer review context (DM enrichment).
    /// </summary>
    [Authorize(Policy = SalesPolicies.SalesManager)]
    [Route("api/sales-manager")]
    [ApiController]
    public sealed class SalesManagerWaiversController : ControllerBase
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly SalesIdentityService _identity;
        private readonly IExceptionReviewService _exceptionReview;
        private readonly IWaiverApplyService _waiverApply;

        public SalesManagerWaiversController(
            SalesDevelopmentGuard guard,
            SalesIdentityService identity,
            IExceptionReviewService exceptionReview,
            IWaiverApplyService waiverApply)
        {
            _guard = guard;
            _identity = identity;
            _exceptionReview = exceptionReview;
            _waiverApply = waiverApply;
        }

        private async Task<IActionResult?> GateAsync(CancellationToken ct)
        {
            var (ok, reason) = await _guard.CanRunSalesModuleAsync(ct);
            if (!ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
            }

            if (_identity.FromAuthenticatedUser() is null)
            {
                return Unauthorized();
            }

            return null;
        }

        [HttpGet("customers/{customerId:int}/waiver-review-context")]
        public async Task<IActionResult> WaiverReviewContext(int customerId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            try
            {
                return Ok(await _exceptionReview.BuildByCustomerIdAsync(identity, customerId, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        public sealed class WaiverApplyBody
        {
            public Guid? WaiverId { get; set; }
            public int CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public double Amount { get; set; }
            public string? Note { get; set; }
            public string? DecidedByUserName { get; set; }
            public string? DecidedByDisplayName { get; set; }
        }

        [HttpPost("waivers/apply")]
        public async Task<IActionResult> Apply([FromBody] WaiverApplyBody? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            if (body is null)
            {
                return BadRequest(new { message = "بيانات التطبيق مطلوبة" });
            }

            try
            {
                await _waiverApply.ApplyAsync(identity, new WaiverApplyRequest
                {
                    WaiverId = body.WaiverId,
                    CustomerId = body.CustomerId,
                    CustomerName = body.CustomerName,
                    Amount = body.Amount,
                    Note = body.Note,
                    DecidedByUserName = body.DecidedByUserName,
                    DecidedByDisplayName = body.DecidedByDisplayName
                }, ct);
                return Ok(new { ok = true });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }
    }
}
