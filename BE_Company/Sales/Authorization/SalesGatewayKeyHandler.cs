using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BE_Company.Sales.Authorization
{
    /// <summary>
    /// Authenticates BE_SalesEmployee using X-Sales-Gateway-Key.
    /// Supports central sales manager OR sales-filter employee identity headers.
    /// </summary>
    public sealed class SalesGatewayKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "SalesGateway";
        public const string HeaderName = "X-Sales-Gateway-Key";
        public const string ManagerNameHeader = "X-Sales-Manager-Name";
        public const string ManagerNameB64Header = "X-Sales-Manager-Name-B64";
        public const string FilterUserNameB64Header = "X-Sales-Filter-User-Name-B64";
        public const string FilterExternalUserIdHeader = "X-Sales-Filter-External-User-Id";
        public const string FilterRoleHeader = "X-Sales-Filter-Role";
        public const string AuthSourceClaim = "AuthSource";
        public const string AuthSourceGateway = "Gateway";
        public const string ExternalUserIdClaim = "ExternalUserId";

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

            var isFilter = IsFilterRequest(Request.Headers);
            var name = isFilter
                ? ResolveFilterName(Request.Headers)
                : ResolveManagerName(Request.Headers);
            var externalId = isFilter ? ResolveExternalUserId(Request.Headers) : "";

            var claims = new List<Claim>
            {
                new("UserID", "0"),
                new("UserName", name),
                new("UserType", isFilter
                    ? SalesRoles.UserTypeSalesFilterEmployee
                    : SalesRoles.UserTypeSalesManager),
                new(AuthSourceClaim, AuthSourceGateway)
            };
            if (!string.IsNullOrWhiteSpace(externalId))
            {
                claims.Add(new Claim(ExternalUserIdClaim, externalId));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private static bool IsFilterRequest(IHeaderDictionary headers) =>
            headers.ContainsKey(FilterUserNameB64Header)
            || headers.ContainsKey(FilterRoleHeader)
            || headers.ContainsKey(FilterExternalUserIdHeader);

        private static string ResolveExternalUserId(IHeaderDictionary headers)
        {
            if (headers.TryGetValue(FilterExternalUserIdHeader, out var id) &&
                !string.IsNullOrWhiteSpace(id))
            {
                return id.ToString().Trim();
            }

            return "";
        }

        private static string ResolveFilterName(IHeaderDictionary headers)
        {
            if (headers.TryGetValue(FilterUserNameB64Header, out var b64) &&
                !string.IsNullOrWhiteSpace(b64))
            {
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(b64.ToString().Trim()));
                    if (!string.IsNullOrWhiteSpace(decoded))
                    {
                        return decoded;
                    }
                }
                catch (FormatException)
                {
                }
            }

            return "SalesFilterEmployee";
        }

        private static string ResolveManagerName(IHeaderDictionary headers)
        {
            if (headers.TryGetValue(ManagerNameB64Header, out var b64) &&
                !string.IsNullOrWhiteSpace(b64))
            {
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(b64.ToString().Trim()));
                    if (!string.IsNullOrWhiteSpace(decoded))
                    {
                        return decoded;
                    }
                }
                catch (FormatException)
                {
                }
            }

            if (headers.TryGetValue(ManagerNameHeader, out var managerName) &&
                !string.IsNullOrWhiteSpace(managerName))
            {
                return managerName.ToString();
            }

            return "SalesManager";
        }
    }
}
