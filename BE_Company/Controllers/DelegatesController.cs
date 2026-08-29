using BE_Company.IRepository;
using BE_Company.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class DelegatesController : ControllerBase
    {
        private readonly IDelegatesRepository _delegatesRepository;

        public DelegatesController(IDelegatesRepository delegatesRepository)
        {
            _delegatesRepository = delegatesRepository;
        }

        [HttpPost("Delegates_Create")]
        public async Task<ActionResult<DelegatesGetDTO?>> Delegates_Create([FromBody] DelegatesPostDTO delegatesPostDTO)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(delegatesPostDTO.AsyncID))
                {
                    return BadRequest("كلمة المرور (AsyncID) مطلوبة.");
                }
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                delegatesPostDTO.UserCreateID = userID;
                var result = await _delegatesRepository.Delegates_Create(delegatesPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Delegates_Update")]
        public async Task<ActionResult<DelegatesGetDTO?>> Delegates_Update([FromBody] DelegatesPutDTO delegatesPutDTO)
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
                delegatesPutDTO.UserUpdateID = userID;
                var result = await _delegatesRepository.Delegates_Update(delegatesPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("Delegates_Delete/{delegateID}")]
        public async Task<ActionResult<bool?>> Delegates_Delete(int? delegateID)
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
                var result = await _delegatesRepository.Delegates_Delete(delegateID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Delegates_GetAll/{delegateName}")]
        public async Task<ActionResult<IEnumerable<DelegatesGetDTO>>> Delegates_GetAll(string? delegateName)
        {
            try
            {
                string? delegate_Name = delegateName != "null" ? delegateName : null;
                var result = await _delegatesRepository.Delegates_GetAll(delegate_Name);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Delegates_GetDataAll")]
        public async Task<ActionResult<IEnumerable<DelegatesDataGetDTO>>> Delegates_GetDataAll()
        {
            try
            {
                var result = await _delegatesRepository.Delegates_GetDataAll();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
 

        [HttpGet("Delegates_Statistics/{fromDate}&&{toDate}")]
        public async Task<ActionResult<IEnumerable<StatisticsGetDTO>>> Delegates_Statistics(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var result = await _delegatesRepository.Delegates_Statistics(fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Delegates_GetDashboardData/{fromDate}&&{toDate}")]
        public async Task<ActionResult<IEnumerable<DelegatesDashboardDataGetDTO>>> Delegates_GetDashboardData(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var result = await _delegatesRepository.Delegates_GetDashboardData(fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Delegates_NoStatistics/{fromDate}&&{toDate}")]
        public async Task<ActionResult<IEnumerable<NoStatisticsGetDTO>>> Delegates_NoStatistics(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var result = await _delegatesRepository.Delegates_NoStatistics(fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("SelectDelegate_Create")]
        public async Task<ActionResult<SelectDelegateGetDTO?>> SelectDelegate_Create([FromBody] SelectDelegatePostDTO selectDelegatePostDTO)
        {
            try
            {
                var result = await _delegatesRepository.SelectDelegate_Create(selectDelegatePostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("SelectDelegate_Delete/{selectDelegateID}")]
        public async Task<ActionResult<bool?>> SelectDelegate_Delete(int? selectDelegateID)
        {
            try
            {
                var result = await _delegatesRepository.SelectDelegate_Delete(selectDelegateID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("SelectDelegate_GetByDelegateID/{delegateID}")]
        public async Task<ActionResult<IEnumerable<SelectDelegateGetDTO>>> SelectDelegate_GetByDelegateID(int? delegateID)
        {
            try
            {
                var result = await _delegatesRepository.SelectDelegate_GetByDelegateID(delegateID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
