using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace BE_Company.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersPaymentsRequestController : ControllerBase
    {
        private readonly ICustomersPaymentsRequestRepository _customersPaymentsRequestRepository;

        public CustomersPaymentsRequestController(ICustomersPaymentsRequestRepository customersPaymentsRequestRepository)
        {
            _customersPaymentsRequestRepository = customersPaymentsRequestRepository;
        }

        [HttpGet("CustomersPaymentsRequest_GetAll/{customerName}&&{delegateID}")]
        public async Task<ActionResult<IEnumerable<CustomersPaymentsRequestGetDTO>>> CustomersPaymentsRequest_GetAll(string? customerName, int? delegateID)
        {
            try
            {
                string? customer_Name = customerName != "null" ? customerName : null;
                int? delegate_ID = delegateID != 0 ? delegateID : null;
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_GetAll(customer_Name, delegate_ID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("CustomersPaymentsRequest_Delete/{customersPaymentsRequestID}")]
        public async Task<ActionResult<bool?>> CustomersPaymentsRequest_Delete(int? customersPaymentsRequestID)
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
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_Delete(customersPaymentsRequestID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("CustomersPaymentsRequest_DeleteAll")]
        public async Task<ActionResult<bool?>> CustomersPaymentsRequest_DeleteAll()
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
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_DeleteAll(userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("CustomersPaymentsRequest_DeleteSame")]
        public async Task<ActionResult<bool?>> CustomersPaymentsRequest_DeleteSame()
        {
            try
            {
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_DeleteSame();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("CustomersPaymentsRequest_Approve/{customersPaymentsRequestID}")]
        public async Task<ActionResult<bool?>> CustomersPaymentsRequest_Approve(int? customersPaymentsRequestID)
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
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_Approve(customersPaymentsRequestID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("CustomersPaymentsRequest_ApproveAll")]
        public async Task<ActionResult<bool?>> CustomersPaymentsRequest_ApproveAll()
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
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_ApproveAll(userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("CustomersPaymentsRequest_ChangeDate/{paymentDate}")]
        public async Task<ActionResult<bool?>> CustomersPaymentsRequest_ChangeDate(DateTime? paymentDate)
        {
            try
            {
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_ChangeDate(paymentDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("CustomersPaymentsRequest_DeleteSelect")]
        public async Task<ActionResult<bool?>> CustomersPaymentsRequest_DeleteSelect([FromBody] List<int> paymentlist)
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
                var result = await _customersPaymentsRequestRepository.CustomersPaymentsRequest_DeleteSelect(idsFinal, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
