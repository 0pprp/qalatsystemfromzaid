using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Repository;
using BE_Company.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BE_Company.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUsersRepository _usersRepository;
        private readonly IConfiguration _configuration;

        public UsersController(IUsersRepository usersRepository, IConfiguration configuration)
        {
            _usersRepository = usersRepository;
            _configuration = configuration;
        }

        [HttpPost("Users_LoginAdmin")]
        public async Task<IActionResult> Users_LoginAdmin([FromBody] LoginDTO loginDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid input data" });
            }
            try
            {
                var data = await _usersRepository.Users_GetUserLoginAdmin(loginDTO.UserName, loginDTO.Password);
                if (data == null)
                {
                    return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
                }
                var authenticationResponse = BuildToken(data, _configuration, _usersRepository);
                return Ok(authenticationResponse);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost("Users_LoginEmployee")]
        public async Task<IActionResult> Users_LoginEmployee([FromBody] LoginDTO loginDTO)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "Invalid input data" });
            }
            try
            {
                var data = await _usersRepository.Users_GetUserLoginEmployee(loginDTO.UserName, loginDTO.Password);
                if (data == null)
                {
                    return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
                }
                var authenticationResponse = BuildToken(data, _configuration, _usersRepository);
                return Ok(authenticationResponse);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        private static AuthenticationResponseDTO BuildToken(UsersGetDTO user, IConfiguration configuration, IUsersRepository usersRepository)
        {
            var claims = new List<Claim>
            {
                new Claim("UserID", user.UserID.ToString() ?? string.Empty),
                new Claim("UserName", user.UserName ?? string.Empty),
                new Claim("UserImage", user.UserImage ?? string.Empty),
                new Claim("UserType", user.UserType ?? string.Empty),
            };
            var key = KeysHandler.GetKey(configuration).First();
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddHours(24);
            var securityToken = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: expiration,
                signingCredentials: credentials
            );
            var token = new JwtSecurityTokenHandler().WriteToken(securityToken);
            return new AuthenticationResponseDTO
            {
                Token = token,
                Expiration = expiration
            };
        }

        [Authorize]
        [HttpGet("Users_GetUserLogin/{userName}&&{password}")]
        public async Task<ActionResult<UsersGetDTO?>> Users_GetUserLogin(string? userName, string? password)
        {
            try
            {
                var result = await _usersRepository.Users_GetUserLogin(userName, password);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpPost("Users_Create")]
        public async Task<ActionResult<UsersGetDTO?>> Users_Create([FromForm] UsersPostDTO usersPostDTO)
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
                usersPostDTO.UserCreateID = userID;
                var result = await _usersRepository.Users_Create(usersPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpPut("Users_Update/{userID}")]
        public async Task<ActionResult<UsersGetDTO?>> Users_Update(int? userID, [FromForm] UsersPutDTO usersPutDTO)
        {
            try
            {
                int? authenticatedUserID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    authenticatedUserID = parsedUserId;
                }
                if (!authenticatedUserID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                usersPutDTO.UserUpdateID = authenticatedUserID; 
                var result = await _usersRepository.Users_Update(userID, usersPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpDelete("Users_Delete/{userID}")]
        public async Task<ActionResult<bool?>> Users_Delete(int? userID)
        {
            try
            {
                int? user_ID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }
                var result = await _usersRepository.Users_Delete(userID, user_ID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpGet("Users_GetAll/{textSearch}")]
        public async Task<ActionResult<IEnumerable<UsersGetDTO>?>> Users_GetAll(string? textSearch)
        {
            try
            {
                string? text_Search = textSearch != "null" ? textSearch : null;
                var result = await _usersRepository.Users_GetAll(text_Search);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [Authorize]
        [HttpGet("Activities_GetByDate/{fromDate}&&{toDate}")]
        public async Task<ActionResult<IEnumerable<ActiveDTO>?>> Activities_GetByDate(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var result = await _usersRepository.Activities_GetByDate(fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
