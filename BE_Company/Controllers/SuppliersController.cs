using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly ISuppliersRepository _suppliersRepository;

        public SuppliersController(ISuppliersRepository suppliersRepository)
        {
            _suppliersRepository = suppliersRepository;
        }

        [HttpPost("Suppliers_Create")]
        public async Task<ActionResult<SuppliersGetDTO?>> Suppliers_Create([FromBody] SuppliersPostDTO suppliersPostDTO)
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
                suppliersPostDTO.UserCreateID = userID;
                var result = await _suppliersRepository.Suppliers_Create(suppliersPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Suppliers_Update/{supplierID}")]
        public async Task<ActionResult<SuppliersGetDTO?>> Suppliers_Update(int? supplierID, [FromBody] SuppliersPutDTO suppliersPutDTO)
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
                suppliersPutDTO.UserUpdateID = userID;
                var result = await _suppliersRepository.Suppliers_Update(supplierID, suppliersPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("Suppliers_Delete/{supplierID}")]
        public async Task<ActionResult<bool?>> Suppliers_Delete(int? supplierID)
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
                var result = await _suppliersRepository.Suppliers_Delete(supplierID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Suppliers_GetAll/{textSearch}")]
        public async Task<ActionResult<IEnumerable<SuppliersGetDTO>?>> Suppliers_GetAll(string? textSearch)
        {
            try
            {
                string? text_Search = textSearch != "null" ? textSearch : null;
                var result = await _suppliersRepository.Suppliers_GetAll(text_Search);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
