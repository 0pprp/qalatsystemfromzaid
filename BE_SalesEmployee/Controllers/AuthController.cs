using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AdminCitiesService _cities;
        private readonly BranchProxyService _proxy;
        private readonly TokenService _tokens;

        public AuthController(AdminCitiesService cities, BranchProxyService proxy, TokenService tokens)
        {
            _cities = cities;
            _proxy = proxy;
            _tokens = tokens;
        }

        public class LoginRequest
        {
            public string? UserName { get; set; }
            public string? Password { get; set; }
            public string? City { get; set; }
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            var cities = await _cities.GetCitiesAsync(ct);
            if (!string.IsNullOrWhiteSpace(request.City) && request.City != "all")
            {
                cities = cities.Where(c =>
                    string.Equals(c.Value, request.City, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Name, request.City, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            foreach (var city in cities)
            {
                try
                {
                    var login = await _proxy.TryEmployeeLoginAsync(city.Link, request.UserName.Trim(), request.Password, ct);
                    if (!login.Ok || !city.Accepts(login.UserType))
                    {
                        continue;
                    }

                    var token = _tokens.CreateToken(
                        login.UserId,
                        login.UserName,
                        login.UserType,
                        city.Link,
                        city.Name,
                        city.Value,
                        login.Token!,
                        out var expiration);

                    return Ok(new
                    {
                        token,
                        expiration,
                        userId = login.UserId,
                        userName = login.UserName,
                        userType = login.UserType,
                        cityLink = city.Link,
                        cityName = city.Name,
                        cityValue = city.Value
                    });
                }
                catch
                {
                    // try next city
                }
            }

            return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة، أو الحساب ليس موظف مبيعات" });
        }

        [Authorize]
        [HttpGet("Me")]
        public IActionResult Me()
        {
            var user = TokenService.FromPrincipal(User);
            return Ok(new
            {
                user.UserID,
                user.UserName,
                user.UserType,
                user.CityName,
                user.CityValue
            });
        }
    }
}
