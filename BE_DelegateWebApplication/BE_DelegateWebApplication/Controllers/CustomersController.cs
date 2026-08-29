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
    public class CustomersController : ControllerBase
    {
        private readonly ICustomersRepository _customersRepository;

        public CustomersController(ICustomersRepository customersRepository)
        {
            _customersRepository = customersRepository;
        }

        [HttpGet("GetCustomersDataInfo/{customerId}")]
        public async Task<ActionResult<CustomerGetDTO?>> GetCustomersDataInfo(int? customerId)
        {
            try
            {
                var result = await _customersRepository.GetCustomersDataInfo(customerId);
                if (result != null)
                {
                    return Ok(result);
                }
                return NotFound("Customer not found.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }



        [HttpGet("GetCustomersDelegateAll/{delegateId}")]
        public async Task<ActionResult<IEnumerable<CustomerGetDTO>>> GetCustomersDelegateAll(int? delegateId)
        {
            try
            {
                var result = await _customersRepository.GetCustomersDelegateAll(delegateId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetCustomersByDelegatePermissions/{delegateId}")]
        public async Task<ActionResult<IEnumerable<CustomerGetDTO>>> GetCustomersByDelegatePermissions(int? delegateId)
        {
            try
            {
                var result = await _customersRepository.GetCustomersByDelegatePermissions(delegateId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetDateWeek")]
        public async Task<ActionResult<DateReceiptsWeekGetDTO>> GetDateWeek()
        {   
            try
            {
                var result = await _customersRepository.GetDateWeek();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
