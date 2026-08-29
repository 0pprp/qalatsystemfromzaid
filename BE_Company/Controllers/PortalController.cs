using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    /// <summary>
    /// Public-facing customer portal API.
    /// No authentication required — access is granted via encrypted token in the query string.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class PortalController : ControllerBase
    {
        private readonly ICustomersRepository _customersRepository;
        private readonly ICustomersPaymentsRepository _customersPaymentsRepository;
        private readonly ICustomersSalesRepository _customersSalesRepository;
        private readonly IConfiguration _configuration;

        public PortalController(
            ICustomersRepository customersRepository,
            ICustomersPaymentsRepository customersPaymentsRepository,
            ICustomersSalesRepository customersSalesRepository,
            IConfiguration configuration)
        {
            _customersRepository = customersRepository;
            _customersPaymentsRepository = customersPaymentsRepository;
            _customersSalesRepository = customersSalesRepository;
            _configuration = configuration;
        }

        /// <summary>
        /// Validates the token and extracts customer ID.
        /// Returns null with HTTP result if invalid.
        /// </summary>
        private ObjectResult? ValidateToken(string? token, out int customerId)
        {
            customerId = 0;

            if (string.IsNullOrEmpty(token))
                return BadRequest(new { error = "المعرف مفقود" });

            string encryptionKey = _configuration["PortalSettings:EncryptionKey"]!;
            if (string.IsNullOrEmpty(encryptionKey))
                return StatusCode(500, new { error = "خطأ في إعدادات الخادم" });

            int? id = EncryptionHelper.ValidateAndGetCustomerId(token, encryptionKey);
            if (!id.HasValue)
                return Unauthorized(new { error = "المعرف غير صالح أو منتهي الصلاحية" });

            customerId = id.Value;
            return null; // Token valid
        }

        /// <summary>
        /// GET /api/Portal/CustomerInfo?token={encrypted}
        /// Returns full customer information including financial summary.
        /// </summary>
        [HttpGet("CustomerInfo")]
        public async Task<ActionResult<CustomersGetDTO>> CustomerInfo([FromQuery] string? token)
        {
            var error = ValidateToken(token, out int customerId);
            if (error != null) return error;

            try
            {
                var customer = await _customersSalesRepository.Customers_GetByCustomerID(customerId);
                if (customer == null)
                    return NotFound(new { error = "الزبون غير موجود" });

                return Ok(customer);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"حدث خطأ: {ex.Message}" });
            }
        }

        /// <summary>
        /// GET /api/Portal/Payments?token={encrypted}&fromDate=yyyy-MM-dd&toDate=yyyy-MM-dd
        /// Returns customer payments with optional date filtering.
        /// </summary>
        [HttpGet("Payments")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentsGetDTO>>> Payments(
            [FromQuery] string? token,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var error = ValidateToken(token, out int customerId);
            if (error != null) return error;

            try
            {
                // Parse date filters
                DateTime? parsedFrom = null;
                DateTime? parsedTo = null;

                if (!string.IsNullOrWhiteSpace(fromDate) && fromDate != "null")
                {
                    if (DateTime.TryParse(fromDate, out DateTime dt))
                        parsedFrom = dt;
                }
                if (!string.IsNullOrWhiteSpace(toDate) && toDate != "null")
                {
                    if (DateTime.TryParse(toDate, out DateTime dt))
                        parsedTo = dt;
                }

                // Get all payments for this customer, then filter by date in-memory
                // (The existing SP doesn't support date filtering by customer, so we filter here)
                var allPayments = await _customersPaymentsRepository.CustomersPayments_GetByCustomerID(customerId);
                if (allPayments == null)
                    return Ok(new List<CustomersPaymentsGetDTO>());

                var filtered = allPayments.AsEnumerable();

                if (parsedFrom.HasValue)
                    filtered = filtered.Where(p => p.PaymentDate >= parsedFrom.Value);

                if (parsedTo.HasValue)
                    filtered = filtered.Where(p => p.PaymentDate <= parsedTo.Value);

                return Ok(filtered.OrderByDescending(p => p.PaymentDate));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"حدث خطأ: {ex.Message}" });
            }
        }

        /// <summary>
        /// GET /api/Portal/Sales?token={encrypted}&fromDate=yyyy-MM-dd&toDate=yyyy-MM-dd
        /// Returns customer sales with optional date filtering.
        /// </summary>
        [HttpGet("Sales")]
        public async Task<ActionResult<IEnumerable<CustomersSalesGetDTO>>> Sales(
            [FromQuery] string? token,
            [FromQuery] string? fromDate,
            [FromQuery] string? toDate)
        {
            var error = ValidateToken(token, out int customerId);
            if (error != null) return error;

            try
            {
                // Parse date filters
                DateTime? parsedFrom = null;
                DateTime? parsedTo = null;

                if (!string.IsNullOrWhiteSpace(fromDate) && fromDate != "null")
                {
                    if (DateTime.TryParse(fromDate, out DateTime dt))
                        parsedFrom = dt;
                }
                if (!string.IsNullOrWhiteSpace(toDate) && toDate != "null")
                {
                    if (DateTime.TryParse(toDate, out DateTime dt))
                        parsedTo = dt;
                }

                // Get sales for this customer
                var allSales = await _customersSalesRepository.CustomersSales_GetByCustomerIDNew(customerId, parsedFrom, parsedTo);
                if (allSales == null)
                    return Ok(new List<CustomersSalesGetDTO>());

                return Ok(allSales);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"حدث خطأ: {ex.Message}" });
            }
        }
    }
}
