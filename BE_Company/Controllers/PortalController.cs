using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    /// <summary>
    /// Public customer portal API. Access requires an authenticated AES-GCM portal token
    /// (header X-Portal-Token or Authorization Bearer preferred; query token accepted for legacy links).
    /// Legacy CBC tokens are rejected with no fallback.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PortalController : ControllerBase
    {
        private readonly ICustomersPaymentsRepository _customersPaymentsRepository;
        private readonly ICustomersSalesRepository _customersSalesRepository;
        private readonly PortalAccessService _portalAccess;

        public PortalController(
            ICustomersRepository customersRepository,
            ICustomersPaymentsRepository customersPaymentsRepository,
            ICustomersSalesRepository customersSalesRepository,
            PortalAccessService portalAccess)
        {
            _ = customersRepository;
            _customersPaymentsRepository = customersPaymentsRepository;
            _customersSalesRepository = customersSalesRepository;
            _portalAccess = portalAccess;
        }

        private ObjectResult? ValidateAccess(string? queryToken, out int customerId)
        {
            customerId = 0;
            var token = ReadToken(queryToken);
            if (!_portalAccess.TryAuthorize(token, out customerId, out var error))
            {
                if (string.Equals(error, "المعرف مفقود", StringComparison.Ordinal))
                {
                    return BadRequest(new { error });
                }

                if (string.Equals(error, "خطأ في إعدادات الخادم", StringComparison.Ordinal))
                {
                    return StatusCode(500, new { error });
                }

                return Unauthorized(new { error = error ?? "المعرف غير صالح أو منتهي الصلاحية" });
            }

            return null;
        }

        private string? ReadToken(string? queryToken)
        {
            if (Request.Headers.TryGetValue("X-Portal-Token", out var headerToken) &&
                !string.IsNullOrWhiteSpace(headerToken))
            {
                return headerToken.ToString().Trim();
            }

            var auth = Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(auth) &&
                auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return auth["Bearer ".Length..].Trim();
            }

            return queryToken;
        }

        [HttpGet("CustomerInfo")]
        public async Task<ActionResult<CustomersGetDTO>> CustomerInfo([FromQuery] string? token)
        {
            var error = ValidateAccess(token, out int customerId);
            if (error != null) return error;

            try
            {
                var customer = await _customersSalesRepository.Customers_GetByCustomerID(customerId);
                if (customer == null)
                    return NotFound(new { error = "الزبون غير موجود" });

                return Ok(customer);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpGet("Payments")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentsGetDTO>>> Payments(
            [FromQuery] string? token,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var error = ValidateAccess(token, out int customerId);
            if (error != null) return error;

            try
            {
                DateTime? parsedFrom = null;
                DateTime? parsedTo = null;

                if (!string.IsNullOrWhiteSpace(fromDate) && fromDate != "null" &&
                    DateTime.TryParse(fromDate, out DateTime dtFrom))
                {
                    parsedFrom = dtFrom;
                }

                if (!string.IsNullOrWhiteSpace(toDate) && toDate != "null" &&
                    DateTime.TryParse(toDate, out DateTime dtTo))
                {
                    parsedTo = dtTo;
                }

                var filtered = await _customersPaymentsRepository.CustomersPayments_GetByCustomerID(
                    customerId, parsedFrom, parsedTo);
                if (filtered == null)
                    return Ok(new List<CustomersPaymentsGetDTO>());

                return Ok(filtered.OrderByDescending(p => p.PaymentDate));
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpGet("Sales")]
        public async Task<ActionResult<IEnumerable<CustomersSalesGetDTO>>> Sales(
            [FromQuery] string? token,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var error = ValidateAccess(token, out int customerId);
            if (error != null) return error;

            try
            {
                DateTime? parsedFrom = null;
                DateTime? parsedTo = null;

                if (!string.IsNullOrWhiteSpace(fromDate) && fromDate != "null" &&
                    DateTime.TryParse(fromDate, out DateTime dtFrom))
                {
                    parsedFrom = dtFrom;
                }

                if (!string.IsNullOrWhiteSpace(toDate) && toDate != "null" &&
                    DateTime.TryParse(toDate, out DateTime dtTo))
                {
                    parsedTo = dtTo;
                }

                var allSales = await _customersSalesRepository.CustomersSales_GetByCustomerIDNew(customerId, parsedFrom, parsedTo);
                if (allSales == null)
                    return Ok(new List<CustomersSalesGetDTO>());

                return Ok(allSales);
            }
            catch (Exception)
            {
                return StatusCode(500, new { error = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        /// <summary>Revoke a still-valid portal token (staff-facing; requires portal token itself).</summary>
        [HttpPost("Revoke")]
        public IActionResult Revoke([FromQuery] string? token)
        {
            var raw = ReadToken(token);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return BadRequest(new { error = "المعرف مفقود" });
            }

            if (!_portalAccess.TryAuthorize(raw, out _, out _))
            {
                return Unauthorized(new { error = "المعرف غير صالح أو منتهي الصلاحية" });
            }

            _portalAccess.RevokeToken(raw);
            return Ok(new { revoked = true });
        }
    }
}
