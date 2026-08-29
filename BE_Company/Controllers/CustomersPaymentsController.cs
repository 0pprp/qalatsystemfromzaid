using BE_Company.IRepository;
using BE_Company.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BE_Company.Repository;
using Newtonsoft.Json;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersPaymentsController : ControllerBase
    {
        private readonly ICustomersPaymentsRepository _customersPaymentsRepository;

        public CustomersPaymentsController(ICustomersPaymentsRepository customersPaymentsRepository)
        {
            _customersPaymentsRepository = customersPaymentsRepository;
        }

        [HttpPost("CustomersPayments_Create")]
        public async Task<ActionResult<CustomersPaymentsGetDTO?>> CustomersPayments_Create([FromBody] CustomersPaymentsPostDTO customersPaymentsPostDTO)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                
                customersPaymentsPostDTO.UserCreateID = userID;
                var result = await _customersPaymentsRepository.CustomersPayments_Create(customersPaymentsPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("CustomersPayments_GetAll/{fromDate}&&{toDate}&&{delegateID}&&{textSearch}")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentsGetDTO>>> CustomersPayments_GetAll(DateTime? fromDate, DateTime? toDate, int? delegateID, string? textSearch)
        {
            try
            {
                string? text_Search = textSearch != "null" ? textSearch : null;
                int? delegate_ID = delegateID != 0 ? delegateID : null;
                var result = await _customersPaymentsRepository.CustomersPayments_GetAll(fromDate, toDate, delegate_ID, text_Search);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("CustomersPayments_GetByCustomerID/{customerID}")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentsGetDTO>>> CustomersPayments_GetByCustomerID(int? customerID)
        {
            try
            {
                var result = await _customersPaymentsRepository.CustomersPayments_GetByCustomerID(customerID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("CustomersPayments_Delete/{customerPaymentID}")]
        public async Task<ActionResult<bool?>> CustomersPayments_Delete(int? customerPaymentID)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                var result = await _customersPaymentsRepository.CustomersPayments_Delete(customerPaymentID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        [HttpPut("CustomersPayments_ChangePaymentDate")]
        public async Task<ActionResult<bool?>> CustomersPayments_ChangePaymentDate([FromBody] ChangePaymentDateDTO request)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }

                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
 
                string idsFinal = string.Join(",", request.Ids);
                var result = await _customersPaymentsRepository.CustomersPayments_ChangePaymentDate(idsFinal, request.NewDate, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        [HttpDelete("CustomersPayments_DeleteSelect")]
        public async Task<ActionResult<bool?>> CustomersPayments_DeleteSelect([FromBody] List<int> paymentlist)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }

                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }

                string idsFinal = string.Join(",", paymentlist);
                var result = await _customersPaymentsRepository.CustomersPayments_DeleteSelect(idsFinal, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
