using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BE_Company.Sales.Authorization
{
    /// <summary>
    /// Authenticates BE_SalesEmployee as a central sales manager using the
    /// existing X-Sales-Gateway-Key. This is not a city employee JWT.
    /// </summary>
    public sealed class SalesGatewayKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "SalesGateway";
        public const string HeaderName = "X-Sales-Gateway-Key";
        public const string ManagerNameHeader = "X-Sales-Manager-Name";
        public const string AuthSourceClaim = "AuthSource";
        public const string AuthSourceGateway = "Gateway";

        private readonly IConfiguration _configuration;

        public SalesGatewayKeyHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfiguration configuration)
            : base(options, logger, encoder)
        {
            _configuration = configuration;
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(HeaderName, out var provided) ||
                string.IsNullOrWhiteSpace(provided))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var expected = _configuration["SalesEmployee:GatewayKey"]
                           ?? _configuration["InternalApiKey"];
            if (string.IsNullOrWhiteSpace(expected) ||
                !string.Equals(provided.ToString(), expected, StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid sales gateway key."));
            }

            var name = Request.Headers.TryGetValue(ManagerNameHeader, out var managerName)
                       && !string.IsNullOrWhiteSpace(managerName)
                ? managerName.ToString()
                : "مدير المبيعات";

            var claims = new[]
            {
                new Claim("UserID", "0"),
                new Claim("UserName", name),
                new Claim("UserType", SalesRoles.UserTypeSalesManager),
                new Claim(AuthSourceClaim, AuthSourceGateway)
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
