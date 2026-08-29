using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersPaymentsRequestsController : ControllerBase
    {
        private readonly ICustomersPaymentsRequestsRepository _customersPaymentsRequestsRepository;

        public CustomersPaymentsRequestsController(ICustomersPaymentsRequestsRepository customersPaymentsRequestsRepository)
        {
            _customersPaymentsRequestsRepository = customersPaymentsRequestsRepository;
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
