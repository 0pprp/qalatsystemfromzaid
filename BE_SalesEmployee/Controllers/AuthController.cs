using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Stores;
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
        private readonly DelegatedManagerAccountService _delegatedManagerAccount;
        private readonly MainFollowerAccountService _mainFollowerAccount;
        private readonly DirectorAccountService _directorAccount;
        private readonly MonitorAccountService _monitorAccount;
        private readonly OversightAccountService _oversightAccounts;
        private readonly SalesFilterLoginService _filterLogin;
        private readonly ISalesEmployeeAppBlockStore _appBlocks;

        public AuthController(
            AdminCitiesService cities,
            BranchProxyService proxy,
            TokenService tokens,
            SalesManagerAccountService managerAccount,
            DelegatedManagerAccountService delegatedManagerAccount,
            MainFollowerAccountService mainFollowerAccount,
            DirectorAccountService directorAccount,
            MonitorAccountService monitorAccount,
            OversightAccountService oversightAccounts,
            SalesFilterLoginService filterLogin,
            ISalesEmployeeAppBlockStore appBlocks)
        {
            _cities = cities;
            _proxy = proxy;
            _tokens = tokens;
            _managerAccount = managerAccount;
            _delegatedManagerAccount = delegatedManagerAccount;
            _mainFollowerAccount = mainFollowerAccount;
            _directorAccount = directorAccount;
            _monitorAccount = monitorAccount;
            _oversightAccounts = oversightAccounts;
            _filterLogin = filterLogin;
            _appBlocks = appBlocks;
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

        /// <summary>Central director login. No province — ratings across all branches.</summary>
        /// <remarks>Also accepts monitor credentials so older clients that only call LoginDirector still work.</remarks>
        [HttpPost("LoginDirector")]
        public async Task<IActionResult> LoginDirector([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            if (_directorAccount.TryAuthenticate(request.UserName, request.Password, out var directorIdentity) &&
                directorIdentity != null)
            {
                return DirectorOk(directorIdentity.DisplayName);
            }

            var oversight = await _oversightAccounts.AuthenticateAsync(request.UserName, request.Password);
            if (oversight != null)
            {
                return OversightOk(oversight);
            }

            // Config monitors are مسؤول المراقبين (legacy section name MonitorAccount).
            if (_monitorAccount.TryAuthenticate(request.UserName, request.Password, out var monitorIdentity) &&
                monitorIdentity != null)
            {
                return SupervisorOk(monitorIdentity.DisplayName);
            }

            return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
        }

        /// <summary>Central monitor login — «عمل المتابعين» only (all provinces).</summary>
        [HttpPost("LoginMonitor")]
        public async Task<IActionResult> LoginMonitor([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            if (_monitorAccount.TryAuthenticate(request.UserName, request.Password, out var identity) &&
                identity != null)
            {
                return SupervisorOk(identity.DisplayName);
            }

            var oversight = await _oversightAccounts.AuthenticateAsync(request.UserName, request.Password);
            if (oversight != null &&
                (oversight.Role == SalesRoles.UserTypeMonitorSupervisor || SalesRoles.IsCommittee(oversight.Role)))
            {
                return OversightOk(oversight);
            }

            return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
        }

        private IActionResult DirectorOk(string displayName)
        {
            var token = _tokens.CreateDirectorToken(displayName, out var expiration);
            return Ok(new
            {
                token,
                expiration,
                userId = 0,
                userName = displayName,
                userType = SalesRoles.UserTypeDirector,
                cityLink = "",
                cityName = "كل المحافظات",
                cityValue = "",
                allowedCities = Array.Empty<string>(),
                central = true
            });
        }

        private IActionResult SupervisorOk(string displayName)
        {
            var token = _tokens.CreateMonitorToken(displayName, out var expiration);
            return Ok(new
            {
                token,
                expiration,
                userId = 0,
                userName = displayName,
                userType = SalesRoles.UserTypeMonitorSupervisor,
                cityLink = "",
                cityName = "كل المحافظات",
                cityValue = "",
                allowedCities = Array.Empty<string>(),
                central = true
            });
        }

        private IActionResult OversightOk(OversightLoginIdentity identity)
        {
            if (identity.Role == SalesRoles.UserTypeDirector)
                return DirectorOk(identity.DisplayName);
            if (identity.Role == SalesRoles.UserTypeMonitorSupervisor)
                return SupervisorOk(identity.DisplayName);

            var cities = identity.CityValues ?? Array.Empty<string>();
            var token = _tokens.CreateOversightToken(identity.DisplayName, identity.Role, cities, out var expiration);
            return Ok(new
            {
                token,
                expiration,
                userId = 0,
                userName = identity.DisplayName,
                userType = identity.Role,
                cityLink = "",
                cityName = cities.Count > 0 ? "فروع اللجنة" : "كل المحافظات",
                cityValue = "",
                allowedCities = cities,
                central = true
            });
        }

        /// <summary>Central delegated-manager login. No province: the account spans all branches.</summary>
        [HttpPost("LoginDelegatedManager")]
        public IActionResult LoginDelegatedManager([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            if (!_delegatedManagerAccount.TryAuthenticate(request.UserName, request.Password, out var identity) ||
                identity == null)
            {
                return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
            }

            var token = _tokens.CreateDelegatedManagerToken(identity.DisplayName, out var expiration);
            return Ok(new
            {
                token,
                expiration,
                userId = 0,
                userName = identity.DisplayName,
                userType = SalesRoles.UserTypeDelegatedManager,
                cityLink = "",
                cityName = "كل المحافظات",
                cityValue = "",
                central = true
            });
        }

        /// <summary>Central main-follower login. Provinces come from assigned missions.</summary>
        [HttpPost("LoginMainFollower")]
        public IActionResult LoginMainFollower([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "اسم المستخدم وكلمة المرور مطلوبان" });
            }

            if (!_mainFollowerAccount.TryAuthenticate(request.UserName, request.Password, out var identity) ||
                identity == null)
            {
                return BadRequest(new { message = "اسم المستخدم أو كلمة المرور غير صحيحة" });
            }

            // JWT UserName must be the login id so mission membership matches.
            var token = _tokens.CreateMainFollowerToken(identity.UserName, out var expiration);
            return Ok(new
            {
                token,
                expiration,
                userId = 0,
                userName = identity.UserName,
                displayName = identity.DisplayName,
                userType = SalesRoles.UserTypeMainFollower,
                cityLink = "",
                cityName = "مهام متعددة المحافظات",
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

                    if (SalesRoles.IsSalesEmployee(login.UserType)
                        && int.TryParse(login.UserId, out var empId)
                        && empId > 0
                        && await _appBlocks.IsBlockedAsync(city.Value, empId, ct))
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new
                        {
                            message = "تم إيقاف تطبيقك من الإدارة — لا يمكنك الدخول. تواصل مع المدير المفوض أو المدير."
                        });
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
