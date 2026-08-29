using BE_Company.IRepository;
using BE_Company.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BE_Company.Repository;

namespace BE_Company.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountsRepository _accountsRepository;

        public AccountsController(IAccountsRepository accountsRepository)
        {
            _accountsRepository = accountsRepository;
        }

        [HttpGet("Boxs_GetAll")]
        public async Task<ActionResult<IEnumerable<BoxsGetDTO>>> Boxs_GetAll()
        {
            try
            {
                var result = await _accountsRepository.Boxs_GetAll();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Boxs_GetAllData")]
        public async Task<ActionResult<IEnumerable<BoxsDataGetDTO>>> Boxs_GetAllData()
        {
            try
            {
                var result = await _accountsRepository.Boxs_GetAllData();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("AddToBoxs_GetByDateByBoxID/{fromDate}&&{toDate}&&{boxID}")]
        public async Task<ActionResult<IEnumerable<AddToBoxsGetDTO>>> AddToBoxs_GetByDateByBoxID(DateTime? fromDate, DateTime? toDate, int? boxID)
        {
            try
            {
                int? box_ID = boxID != 0 ? boxID : null;
                var result = await _accountsRepository.AddToBoxs_GetByDateByBoxID(fromDate, toDate, box_ID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("WithdrawalFromBoxs_GetByDateByBoxID/{fromDate}&&{toDate}&&{boxID}")]
        public async Task<ActionResult<IEnumerable<WithdrawalFromBoxsGetDTO>>> WithdrawalFromBoxs_GetByDateByBoxID(DateTime? fromDate, DateTime? toDate, int? boxID)
        {
            try
            {
                int? box_ID = boxID != 0 ? boxID : null;
                var result = await _accountsRepository.WithdrawalFromBoxs_GetByDateByBoxID(fromDate, toDate, box_ID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("TransferBoxs_GetByDate/{fromDate}&&{toDate}")]
        public async Task<ActionResult<IEnumerable<TransferBoxsGetDTO>>> TransferBoxs_GetByDate(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var result = await _accountsRepository.TransferBoxs_GetByDate(fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("Boxs_Delete/{boxID}")]
        public async Task<ActionResult<bool?>> Boxs_Delete(int? boxID)
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
                var result = await _accountsRepository.Boxs_Delete(boxID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("AddToBox_Delete/{addToBoxID}")]
        public async Task<ActionResult<bool?>> AddToBox_Delete(int? addToBoxID)
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
                var result = await _accountsRepository.AddToBox_Delete(addToBoxID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("WithdrawalFromBox_Delete/{withdrawalFromBoxID}")]
        public async Task<ActionResult<bool?>> WithdrawalFromBox_Delete(int? withdrawalFromBoxID)
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
                var result = await _accountsRepository.WithdrawalFromBox_Delete(withdrawalFromBoxID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("TransferBoxs_Delete/{transferBoxID}")]
        public async Task<ActionResult<bool?>> TransferBoxs_Delete(int? transferBoxID)
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
                var result = await _accountsRepository.TransferBoxs_Delete(transferBoxID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("AddToBox_Create")]
        public async Task<ActionResult<bool?>> AddToBox_Create([FromBody] AccountsPostDTO accountsPostDTO)
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
                accountsPostDTO.UserCreateID = userID;
                var result = await _accountsRepository.AddToBox_Create(accountsPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("WithdrawalFromBox_Create")]
        public async Task<ActionResult<bool?>> WithdrawalFromBox_Create([FromBody] AccountsPostDTO accountsPostDTO)
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
                accountsPostDTO.UserCreateID = userID;
                var result = await _accountsRepository.WithdrawalFromBox_Create(accountsPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("TransferBoxs_Create")]
        public async Task<ActionResult<bool?>> TransferBoxs_Create([FromBody] AccountsPostDTO accountsPostDTO)
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
                accountsPostDTO.UserCreateID = userID;
                var result = await _accountsRepository.TransferBoxs_Create(accountsPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }



        [HttpPost("Boxs_Create")]
        public async Task<ActionResult<bool?>> Boxs_Create([FromBody] BoxsPostDTO boxsPostDTO)
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
                boxsPostDTO.CreateUserID = userID;
                var result = await _accountsRepository.Boxs_Create(boxsPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }



        [HttpPut("Boxs_Update/{boxID}")]
        public async Task<ActionResult<bool?>> Boxs_Update(int? boxID,[FromBody] BoxsPutDTO boxsPutDTO)
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
                boxsPutDTO.UpdateUserID = userID;
                var result = await _accountsRepository.Boxs_Update(boxID,boxsPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
