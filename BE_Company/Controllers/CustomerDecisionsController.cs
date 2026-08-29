using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerDecisionsController : ControllerBase
    {
        private readonly ICustomerDecisionsRepository _repository;

        public CustomerDecisionsController(ICustomerDecisionsRepository repository)
        {
            _repository = repository;
        }

        private int? GetAuthenticatedUserId()
        {
            if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
            {
                return parsedUserId;
            }
            return null;
        }

        private bool CanDecide()
        {
            return GetUserType() == "مدير فرع";
        }

        private bool CanWriteNotes()
        {
            var userType = GetUserType();
            return userType == "محاسب رئيسي" || userType == "مدير فرع";
        }

        private string GetUserType()
        {
            return HttpContext.Items["UserType"] as string ?? string.Empty;
        }

        [HttpGet("WeakWeekPayers")]
        public async Task<ActionResult<IEnumerable<CustomerWeekPaymentsGetDTO>>> WeakWeekPayers()
        {
            try
            {
                var result = await _repository.Customers_GetWeakWeekPayers();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("Decide")]
        public async Task<ActionResult<CustomerWeekDecisionGetDTO?>> Decide([FromBody] CustomerWeekDecisionPostDTO dto)
        {
            try
            {
                var userID = GetAuthenticatedUserId();
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                if (!CanDecide())
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "اتخاذ القرار من صلاحية مدير الفرع فقط" });
                }
                if (dto.CustomerID == null || string.IsNullOrWhiteSpace(dto.DecisionType))
                {
                    return BadRequest(new { message = "يجب تحديد الزبون ونوع القرار" });
                }

                var result = await _repository.Customers_PostWeekDecision(dto.CustomerID, userID, dto.DecisionType, dto.Note);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Decisions")]
        public async Task<ActionResult<IEnumerable<CustomerWeekDecisionGetDTO>>> Decisions(
            [FromQuery] string? decisionType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int? customerID)
        {
            try
            {
                string? type = string.IsNullOrWhiteSpace(decisionType) || decisionType == "null" ? null : decisionType;
                var result = await _repository.Customers_GetWeekDecisions(type, fromDate, toDate, customerID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Notifications")]
        public async Task<ActionResult<IEnumerable<DecisionNotificationGetDTO>>> Notifications()
        {
            try
            {
                var result = await _repository.Customers_GetDecisionNotifications();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Notifications/{notificationID}/Read")]
        public async Task<ActionResult<bool?>> ReadNotification(int notificationID)
        {
            try
            {
                var result = await _repository.Customers_ReadDecisionNotification(notificationID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Notifications/ReadAll")]
        public async Task<ActionResult<bool?>> ReadAllNotifications()
        {
            try
            {
                var result = await _repository.Customers_ReadAllDecisionNotifications();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Notes/{customerID}")]
        public async Task<ActionResult<IEnumerable<CustomerNoteGetDTO>>> Notes(int customerID)
        {
            try
            {
                var result = await _repository.Customers_GetCustomerNotes(customerID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("Notes")]
        public async Task<ActionResult<CustomerNoteGetDTO?>> PostNote([FromBody] CustomerNotePostDTO dto)
        {
            try
            {
                var userID = GetAuthenticatedUserId();
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                if (!CanWriteNotes())
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مسموح بكتابة ملاحظة" });
                }
                if (dto.CustomerID == null || string.IsNullOrWhiteSpace(dto.NoteText))
                {
                    return BadRequest(new { message = "يجب تحديد الزبون ونص الملاحظة" });
                }

                var result = await _repository.Customers_PostCustomerNote(dto.CustomerID, userID, dto.NoteText);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
