using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesEmployeeController : ControllerBase
    {
        private readonly ISalesEmployeeRepository _repository;
        private readonly IConfiguration _configuration;

        public SalesEmployeeController(ISalesEmployeeRepository repository, IConfiguration configuration)
        {
            _repository = repository;
            _configuration = configuration;
        }

        private int? GetAuthenticatedUserId()
        {
            if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
            {
                return parsedUserId;
            }
            return null;
        }

        private bool IsGatewayRequest()
        {
            var expected = _configuration["SalesEmployee:GatewayKey"];
            if (string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }
            return Request.Headers.TryGetValue("X-Sales-Gateway-Key", out var key)
                   && string.Equals(key.ToString(), expected, StringComparison.Ordinal);
        }

        [Authorize]
        [HttpGet("ShiftStatus")]
        public async Task<ActionResult<SalesEmployeeShiftDTO?>> ShiftStatus()
        {
            try
            {
                var userID = GetAuthenticatedUserId();
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                var result = await _repository.GetActiveShift(userID.Value, SalesEmployeeRepository.IraqNow());
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpPost("ShiftStart")]
        public async Task<ActionResult<SalesEmployeeShiftDTO>> ShiftStart()
        {
            try
            {
                var userID = GetAuthenticatedUserId();
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                var result = await _repository.StartShift(userID.Value, SalesEmployeeRepository.IraqNow());
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpPost("TrackSync")]
        public async Task<ActionResult<SalesEmployeeTrackSyncResultDTO>> TrackSync([FromBody] SalesEmployeeTrackSyncDTO dto)
        {
            try
            {
                var userID = GetAuthenticatedUserId();
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                var result = await _repository.SyncTrackPoints(userID.Value, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpPost("Ratings")]
        public async Task<ActionResult<SalesCustomerRatingGetDTO?>> Ratings([FromBody] SalesCustomerRatingPostDTO dto)
        {
            try
            {
                var userID = GetAuthenticatedUserId();
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                if (dto.RatingLevel < 1 || dto.RatingLevel > 5)
                {
                    return BadRequest(new { message = "مستوى التقييم يجب أن يكون بين 1 و 5" });
                }
                if (dto.RatingLevel == 1 && string.IsNullOrWhiteSpace(dto.RejectionReason))
                {
                    return BadRequest(new { message = "سبب الرفض إلزامي للتقييم المرفوض" });
                }
                var result = await _repository.SaveRating(userID.Value, dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpGet("SearchCustomers/{textSearch}")]
        public async Task<ActionResult<IEnumerable<SalesEmployeeSearchCustomerDTO>>> SearchCustomers(string? textSearch)
        {
            try
            {
                if (!IsGatewayRequest() && GetAuthenticatedUserId() == null)
                {
                    return Unauthorized();
                }
                var q = textSearch == "null" ? null : textSearch;
                var result = await _repository.SearchCustomers(q);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
