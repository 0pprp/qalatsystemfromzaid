using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Repository;
using BE_Company.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ItemsController : ControllerBase
    {
        private readonly IItemsRepository _itemsRepository;

        public ItemsController(IItemsRepository itemsRepository)
        {
            _itemsRepository = itemsRepository;
        }

        [HttpPost("Items_Create")]
        public async Task<ActionResult<ItemsGetDTO?>> Items_Create([FromBody] ItemsPostDTO itemsPostDTO)
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
                itemsPostDTO.UserCreateID = userID;
                var result = await _itemsRepository.Items_Create(itemsPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Items_Update/{itemID}")]
        public async Task<ActionResult<ItemsGetDTO?>> Items_Update(int? itemID, [FromBody] ItemsPutDTO itemsPutDTO)
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
                itemsPutDTO.UserUpdateID = userID;
                var result = await _itemsRepository.Items_Update(itemID, itemsPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("Items_Delete/{itemID}")]
        public async Task<ActionResult<bool?>> Items_Delete(int? itemID)
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
                var result = await _itemsRepository.Items_Delete(itemID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Items_GetAll/{storeID}&&{itemName}&&{showType}")]
        public async Task<ActionResult<IEnumerable<ItemsGetDTO>?>> Items_GetAll(int? storeID, string? itemName, string? showType)
        {
            try
            {
                string? item_Name = itemName != "null" ? itemName : null;
                var result = await _itemsRepository.Items_GetAll(storeID, item_Name, showType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Items_GetByItemBuy/{storeID}")]
        public async Task<ActionResult<IEnumerable<ItemsGetDTO>?>> Items_GetByItemBuy(int? storeID)
        {
            try
            {
                var result = await _itemsRepository.Items_GetByItemBuy(storeID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Items_GetByItemSale/{storeID}")]
        public async Task<ActionResult<IEnumerable<ItemsGetDTO>?>> Items_GetByItemSale(int? storeID)
        {
            try
            {
                var result = await _itemsRepository.Items_GetByItemSale(storeID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
