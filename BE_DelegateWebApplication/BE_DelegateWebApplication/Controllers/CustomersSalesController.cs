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
    public class CustomersSalesController : ControllerBase
    {
        private readonly ICustomersSalesRepository _customersSalesRepository;

        public CustomersSalesController(ICustomersSalesRepository customersSalesRepository)
        {
            _customersSalesRepository = customersSalesRepository;
        }

        [HttpGet("GetCustomersSalesCustomerDate/id={customerId}&&date={dateCreate}")]
        public async Task<ActionResult<IEnumerable<CustomersSalesGetDTO>>> GetCustomersSalesCustomerDate(int? customerId, string? dateCreate)
        {
            try
            {
                var result = await _customersSalesRepository.GetCustomersSalesCustomerDate(customerId, DateTime.Parse(dateCreate!));
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetCustomersSalesCustomer/{customerId}")]
        public async Task<ActionResult<IEnumerable<CustomersSalesGetDTO>>> GetCustomersSalesCustomer(int? customerId)
        {
            try
            {
                var result = await _customersSalesRepository.GetCustomersSalesCustomer(customerId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetCustomersSalesCustomerLast/{delegateId}")]
        public async Task<ActionResult<IEnumerable<CustomersSalesGetDTO>>> GetCustomersSalesCustomerLast(int? delegateId)
        {
            try
            {
                var result = await _customersSalesRepository.GetCustomersSalesCustomerLast(delegateId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetCustomersSalesCustomerName/id={id}&&customerName={customerName}")]
        public async Task<ActionResult<IEnumerable<CustomersSalesGetDTO>>> GetCustomersSalesCustomerName(int? id,string? customerName)
        {
            try
            {
                var result = await _customersSalesRepository.GetCustomersSalesCustomerName(id, customerName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
