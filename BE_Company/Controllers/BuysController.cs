using BE_Company.IRepository;
using BE_Company.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BE_Company.Repository;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BuysController : ControllerBase
    {
        private readonly IBuysRepository _buysRepository;

        public BuysController(IBuysRepository buysRepository)
        {
            _buysRepository = buysRepository;
        }

        [HttpPost("Buys_Create")]
        public async Task<ActionResult<BuysGetDTO?>> Buys_Create([FromBody] BuysPostDTO buysPostDTO)
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
                buysPostDTO.UserCreateID = userID;
                var result = await _buysRepository.Buys_Create(buysPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("Buys_Delete/{buyID}")]
        public async Task<ActionResult<bool?>> Buys_Delete(int? buyID)
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
                var result = await _buysRepository.Buys_Delete(buyID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Buys_GetByDateByTextSearch/{fromDate}&&{toDate}&&{textSearch}")]
        public async Task<ActionResult<IEnumerable<BuysGetDTO>>> Buys_GetByDateByTextSearch(DateTime? fromDate, DateTime toDate, string? textSearch)
        {
            try
            {
                string? text_Search = textSearch != "null" ? textSearch : null;
                var result = await _buysRepository.Buys_GetByDateByTextSearch(fromDate, toDate, text_Search);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Buys_UpdateDate/{buyID}")]
        public async Task<ActionResult<bool?>> Buys_UpdateDate(int? buyID, [FromBody] BuysPutDateCreateDTO buysPutDateCreate)
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

                buysPutDateCreate.UpdateCreateID = userID;
                var result = await _buysRepository.Buys_UpdateDate(buyID, buysPutDateCreate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
