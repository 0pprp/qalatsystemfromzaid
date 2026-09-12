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
        private readonly SalesManagerAccountService _managerAccount;
        private readonly SalesFilterLoginService _filterLogin;

        public AuthController(
            AdminCitiesService cities,
            BranchProxyService proxy,
            TokenService tokens,
            SalesManagerAccountService managerAccount,
            SalesFilterLoginService filterLogin)
        {
            _cities = cities;
            _proxy = proxy;
            _tokens = tokens;
            _managerAccount = managerAccount;
            _filterLogin = filterLogin;
        }

        public class LoginRequest
        {
            public string? UserName { get; set; }
            public string? Password { get; set; }
            public string? City { get; set; }
        }

        [HttpPost("LoginSalesManager")]
        public IActionResult LoginSalesManager([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            if (!_managerAccount.TryAuthenticate(request.UserName, request.Password, out var identity) ||
                identity == null)
            {
                return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
            }

            var token = _tokens.CreateCentralManagerToken(identity.DisplayName, out var expiration);
            return Ok(new
            {
                token,
                expiration,
                userId = 0,
                userName = identity.DisplayName,
                userType = "مدير مبيعات",
                cityLink = "",
                cityName = "كل المحافظات",
                cityValue = "",
                central = true
            });
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

        [HttpPost("LoginSalesFilter")]
        public async Task<IActionResult> LoginSalesFilter([FromBody] LoginRequest request, CancellationToken ct)
        {
            var result = await _filterLogin.LoginAsync(request.UserName ?? "", request.Password ?? "", ct);
            if (!result.Ok)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(new
            {
                token = result.Token,
                expiration = result.Expiration,
                userId = result.UserId,
                userName = result.UserName,
                userType = result.UserType,
                homeCityValue = result.HomeCityValue,
                cityValue = result.HomeCityValue,
                allowedFilterCities = result.AllowedFilterCities,
                cities = result.Cities,
                central = true
            });
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
                user.CityValue,
                homeCityValue = user.CityValue,
                allowedFilterCities = user.AllowedFilterCities,
                central = user.IsCentral
            });
        }
    }
}
