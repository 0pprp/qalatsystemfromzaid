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
    public class CustomersPaymentsController : ControllerBase
    {
        private readonly ICustomersPaymentsRepository _customersPaymentsRepository;

        public CustomersPaymentsController(ICustomersPaymentsRepository customersPaymentsRepository)
        {
            _customersPaymentsRepository = customersPaymentsRepository;
        }

        [HttpGet("GetCustomersPaymentsCustomerDate/id={customerId}&&date={dateCreate}")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentGetDTO>>> GetCustomersPaymentsCustomerDate(int? customerId, string? dateCreate)
        {
            try
            {
                var result = await _customersPaymentsRepository.GetCustomersPaymentsCustomerDate(customerId, DateTime.Parse(dateCreate!));
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetCustomersPaymentsCustomer/{customerId}")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentGetDTO>>> GetCustomersPaymentsCustomer(int? customerId)
        {
            try
            {
                var result = await _customersPaymentsRepository.GetCustomersPaymentsCustomer(customerId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetCustomersPaymentsCustomerName/id={id}&&customerName={customerName}")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentGetDTO>>> GetCustomersPaymentsCustomerName(int? id,string? customerName)
        {
            try
            {
                var result = await _customersPaymentsRepository.GetCustomersPaymentsCustomerName(id, customerName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
