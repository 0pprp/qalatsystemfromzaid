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
    public class ExchangesItemsController : ControllerBase
    {
        private readonly IExchangesItemsRepository _exchangesItemsRepository;

        public ExchangesItemsController(IExchangesItemsRepository exchangesItemsRepository)
        {
            _exchangesItemsRepository = exchangesItemsRepository;
        }

        [HttpPost("ExchangesItems_Create")]
        public async Task<ActionResult<ExchangesItemsGetDTO?>> ExchangesItems_Create([FromBody] ExchangesItemsPostDTO exchangesItemsPostDTO)
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
                exchangesItemsPostDTO.UserCreateID = userID;
                var result = await _exchangesItemsRepository.ExchangesItems_Create(exchangesItemsPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("ExchangesItems_Update/{exchangeItemID}")]
        public async Task<ActionResult<ExchangesItemsGetDTO?>> ExchangesItems_Update(int exchangeItemID, [FromBody] ExchangesItemsPutDTO exchangesItemsPutDTO)
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
                exchangesItemsPutDTO.UserUpdateID = userID;
                var result = await _exchangesItemsRepository.ExchangesItems_Update(exchangeItemID, exchangesItemsPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("ExchangesItems_Delete/{exchangeItemID}")]
        public async Task<ActionResult<bool?>> ExchangesItems_Delete(int exchangeItemID)
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
                var result = await _exchangesItemsRepository.ExchangesItems_Delete(exchangeItemID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("ExchangesItems_GetAll")]
        public async Task<ActionResult<IEnumerable<ExchangesItemsGetDTO>>> ExchangesItems_GetAll()
        {
            try
            {
                var result = await _exchangesItemsRepository.ExchangesItems_GetAll();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
