using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.DTO;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersPaymentsRequestsController : ControllerBase
    {
        private readonly ICustomersPaymentsRequestsRepository _customersPaymentsRequestsRepository;
        private readonly IDelegateRepository _delegateRepository;

        public CustomersPaymentsRequestsController(
            ICustomersPaymentsRequestsRepository customersPaymentsRequestsRepository,
            IDelegateRepository delegateRepository)
        {
            _customersPaymentsRequestsRepository = customersPaymentsRequestsRepository;
            _delegateRepository = delegateRepository;
        }

        [HttpPost("PostSelectPaymentCustomerTemporary")]
        public async Task<ActionResult<bool?>> PostSelectPaymentCustomerTemporary([FromBody] CustomersPaymentsRequestsPostDTO? customersPaymentsRequestsPostDTO)
        {
            try
            {
                var result = await _customersPaymentsRequestsRepository.PostSelectPaymentCustomerTemporary(customersPaymentsRequestsPostDTO);
                if (result == true)
                {
                    return Ok(result);
                }
                return BadRequest("Failed to process payment request.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("PostSelectPaymentCustomerTemporaryMulti")]
        public async Task<ActionResult<bool?>> PostSelectPaymentCustomerTemporaryMulti([FromBody] List<CustomersPaymentsRequestsPostDTO>? customersPaymentsRequestsPostDTO)
        {
            try
            {
                var result = await _customersPaymentsRequestsRepository.PostSelectPaymentCustomerTemporaryMulti(customersPaymentsRequestsPostDTO);
                if (result == true)
                {
                    return Ok(result);
                }
                return BadRequest("Failed to process payment request.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Idempotent offline-first payment intake. Requires AsyncId; DelegateId taken from server identity.
        /// </summary>
        [HttpPost("PostPaymentIdempotent")]
        public async Task<ActionResult<PaymentIdempotentResultDTO>> PostPaymentIdempotent(
            [FromBody] CustomersPaymentsRequestsPostDTO? body,
            CancellationToken ct)
        {
            if (body is null)
            {
                return BadRequest(new { message = "بيانات التسديد مطلوبة" });
            }

            if (string.IsNullOrWhiteSpace(body.AsyncId))
            {
                return Unauthorized(new { message = "جلسة المندوب غير صالحة" });
            }

            var login = await _delegateRepository.GetDelegateLogin(body.AsyncId.Trim().TrimEnd('/'));
            if (login is null || login.DelegateId <= 0)
            {
                return Unauthorized(new { message = "جلسة المندوب غير صالحة" });
            }

            var result = await _customersPaymentsRequestsRepository.PostPaymentIdempotentAsync(
                body,
                login.DelegateId,
                ct);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }

        [HttpGet("GetCustomersPaymentsRequestsByDelegateID/{delegateId}")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentsRequestsGetDTO>?>> GetCustomersPaymentsRequestsByDelegateID(int? delegateId)
        {
            try
            {
                var result = await _customersPaymentsRequestsRepository.GetCustomersPaymentsRequestsByDelegateID(delegateId);
                if (result != null)
                {
                    return Ok(result);
                }
                return NotFound("No payment requests found for this delegate.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
