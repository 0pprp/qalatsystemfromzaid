using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Repository;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Filtering;
using BE_Company.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BE_Company.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUsersRepository _usersRepository;
        private readonly IFollowerUserListsRepository _followerLists;
        private readonly ISalesFilterService _salesFilter;
        private readonly IConfiguration _configuration;

        public UsersController(
            IUsersRepository usersRepository,
            IFollowerUserListsRepository followerLists,
            ISalesFilterService salesFilter,
            IConfiguration configuration)
        {
            _usersRepository = usersRepository;
            _followerLists = followerLists;
            _salesFilter = salesFilter;
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
                var authenticationResponse = BuildToken(data, _configuration);
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
                int? sessionVersion = null;
                if (BE_Company.Sales.Authorization.SalesRoles.IsSalesEmployee(data.UserType) && data.UserID is int employeeUserId)
                {
                    sessionVersion = await _usersRepository.BumpSalesEmployeeSessionVersionAsync(employeeUserId);
                }
                var authenticationResponse = BuildToken(data, _configuration, sessionVersion);
                return Ok(authenticationResponse);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        private static AuthenticationResponseDTO BuildToken(UsersGetDTO user, IConfiguration configuration, int? sessionVersion = null)
        {
            var claims = new List<Claim>
            {
                new Claim("UserID", user.UserID.ToString() ?? string.Empty),
                new Claim("UserName", user.UserName ?? string.Empty),
                new Claim("UserImage", user.UserImage ?? string.Empty),
                new Claim("UserType", user.UserType ?? string.Empty),
            };
            if (sessionVersion is int version)
            {
                claims.Add(new Claim(BE_Company.Services.SalesEmployeeSession.ClaimName, version.ToString()));
            }
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

                var userName = usersPostDTO.UserName?.Trim();
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return BadRequest(new { message = "اسم المستخدم مطلوب" });
                }

                usersPostDTO.UserName = userName;
                if (await _usersRepository.UserNameExistsAsync(userName))
                {
                    return Conflict(new { message = "اسم المستخدم مستخدم مسبقاً" });
                }

                usersPostDTO.UserCreateID = userID;
                var result = await _usersRepository.Users_Create(usersPostDTO);
                if (result?.UserID is int newUserId)
                {
                    await SyncFollowerListsAsync(newUserId, usersPostDTO.UserType, usersPostDTO.ListIdsJson);
                    await SyncFilterCitiesAsync(newUserId, usersPostDTO.UserType, usersPostDTO.FilterCitiesJson);
                }
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

                var userName = usersPutDTO.UserName?.Trim();
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return BadRequest(new { message = "اسم المستخدم مطلوب" });
                }

                usersPutDTO.UserName = userName;
                if (await _usersRepository.UserNameExistsAsync(userName, excludeUserId: userID))
                {
                    return Conflict(new { message = "اسم المستخدم مستخدم مسبقاً" });
                }

                usersPutDTO.UserUpdateID = authenticatedUserID; 
                var result = await _usersRepository.Users_Update(userID, usersPutDTO);
                if (userID is int uid)
                {
                    await SyncFollowerListsAsync(uid, usersPutDTO.UserType, usersPutDTO.ListIdsJson);
                    await SyncFilterCitiesAsync(uid, usersPutDTO.UserType, usersPutDTO.FilterCitiesJson);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private async Task SyncFilterCitiesAsync(int userId, string? userType, string? filterCitiesJson)
        {
            if (SalesRoles.IsSalesFilterEmployee(userType))
            {
                var cities = ParseFilterCities(filterCitiesJson);
                await _salesFilter.ReplaceUserCitiesAsync(userId, cities);
            }
            else
            {
                await _salesFilter.ClearUserCitiesAsync(userId);
            }
        }

        private static List<(string CityValue, string? CityName)> ParseFilterCities(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
                var list = new List<(string, string?)>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var v = el.GetString();
                        if (!string.IsNullOrWhiteSpace(v)) list.Add((v.Trim(), null));
                        continue;
                    }

                    if (el.ValueKind != JsonValueKind.Object) continue;
                    var value = el.TryGetProperty("cityValue", out var cv) ? cv.GetString()
                        : el.TryGetProperty("CityValue", out var cv2) ? cv2.GetString()
                        : el.TryGetProperty("value", out var cv3) ? cv3.GetString() : null;
                    var name = el.TryGetProperty("cityName", out var cn) ? cn.GetString()
                        : el.TryGetProperty("CityName", out var cn2) ? cn2.GetString()
                        : el.TryGetProperty("name", out var cn3) ? cn3.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        list.Add((value.Trim(), name));
                    }
                }

                return list;
            }
            catch
            {
                return [];
            }
        }

        [Authorize]
        [HttpGet("Users_FilterCities/{userId:int}")]
        public async Task<IActionResult> Users_FilterCities(int userId, CancellationToken ct)
        {
            try
            {
                var cities = await _salesFilter.ListUserCitiesAsync(userId, ct);
                return Ok(new { cities });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        private async Task SyncFollowerListsAsync(int userId, string? userType, string? listIdsJson)
        {
            if (SalesRoles.IsFollower(userType))
            {
                var ids = ParseListIds(listIdsJson);
                await _followerLists.ReplaceAssignmentsAsync(userId, ids);
                // AsyncID stays a separate session token (Users_Create NEWID / existing value).
                // Do not overwrite AsyncID with Password.
            }
            else
            {
                // Leaving متابع: clear ACL so access cannot linger after type change.
                await _followerLists.ClearAssignmentsAsync(userId);
            }
        }

        private static List<int> ParseListIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                var ids = JsonSerializer.Deserialize<List<int>>(json);
                return ids?.Where(x => x > 0).Distinct().ToList() ?? [];
            }
            catch
            {
                return [];
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
