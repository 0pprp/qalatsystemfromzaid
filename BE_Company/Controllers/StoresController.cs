using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Repository;
using BE_Company.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Claims;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StoresController : ControllerBase
    {
        private readonly IStoresRepository _storesRepository;

        public StoresController(IStoresRepository storesRepository)
        {
            _storesRepository = storesRepository;
        }

        [HttpPost("Stores_Create")]
        public async Task<ActionResult<StoresGetDTO?>> Stores_Create([FromBody] StoresPostDTO storesPostDTO)
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
                storesPostDTO.UserCreateID = userID;
                var result = await _storesRepository.Stores_Create(storesPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Stores_Update/{storeID}")]
        public async Task<ActionResult<StoresGetDTO?>> Stores_Update(int? storeID, [FromBody] StoresPutDTO storesPutDTO)
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
                storesPutDTO.UserUpdateID = userID;
                var result = await _storesRepository.Stores_Update(storeID, storesPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("Stores_Delete/{storeID}")]
        public async Task<ActionResult<bool?>> Stores_Delete(int? storeID)
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
                var result = await _storesRepository.Stores_Delete(storeID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Stores_GetAll/{textSearch}")]
        public async Task<ActionResult<IEnumerable<StoresGetDTO>?>> Stores_GetAll(string? textSearch)
        {
            try
            {
                string? text_Search = textSearch != "null" ? textSearch : null;
                var result = await _storesRepository.Stores_GetAll(text_Search);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("StoresData_GetAll")]
        public async Task<ActionResult<IEnumerable<StoresDataGetDTO>?>> StoresData_GetAll()
        {
            try
            {
                var result = await _storesRepository.StoresData_GetAll();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
