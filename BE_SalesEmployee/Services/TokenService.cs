using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BE_SalesEmployee.Services
{
    public class TokenService
    {
        private readonly string _key;

        public TokenService(IConfiguration configuration)
        {
            _key = configuration["Jwt:Key"] ?? "SalesEmployeeGwSigningKey-2026-ChangeMe!!";
        }

        public const string CentralClaim = "Central";
        public const string AllowedFilterCitiesClaim = "AllowedFilterCities";
        public const string HomeCityValueClaim = "HomeCityValue";

        public string CreateCentralManagerToken(string userName, out DateTime expiration)
        {
            var claims = new List<Claim>
            {
                new("UserID", "0"),
                new("UserName", userName),
                new("UserType", "مدير مبيعات"),
                new("CityLink", ""),
                new("CityName", "كل المحافظات"),
                new("CityValue", ""),
                new(CentralClaim, "true")
            };
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                SecurityAlgorithms.HmacSha256);
            expiration = DateTime.UtcNow.AddHours(24);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiration,
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Central filter-employee JWT. Never embeds branch tokens or passwords.
        /// AllowedFilterCities is a comma-separated list of cityValue codes.
        /// </summary>
        public string CreateSalesFilterToken(
            string userId,
            string userName,
            string userType,
            string homeCityValue,
            IEnumerable<string> allowedFilterCities,
            out DateTime expiration)
        {
            var cities = allowedFilterCities
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var claims = new List<Claim>
            {
                new("UserID", userId ?? ""),
                new("UserName", userName ?? ""),
                new("UserType", userType ?? ""),
                new("CityLink", ""),
                new("CityName", ""),
                new("CityValue", homeCityValue ?? ""),
                new(HomeCityValueClaim, homeCityValue ?? ""),
                new(AllowedFilterCitiesClaim, string.Join(',', cities)),
                new(CentralClaim, "true")
            };
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                SecurityAlgorithms.HmacSha256);
            expiration = DateTime.UtcNow.AddHours(24);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiration,
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string CreateToken(
            string userId,
            string userName,
            string userType,
            string cityLink,
            string cityName,
            string cityValue,
            string branchToken,
            out DateTime expiration)
        {
            var claims = new List<Claim>
            {
                new("UserID", userId),
                new("UserName", userName),
                new("UserType", userType),
                new("CityLink", cityLink),
                new("CityName", cityName),
                new("CityValue", cityValue),
                new("BranchToken", branchToken)
            };
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                SecurityAlgorithms.HmacSha256);
            expiration = DateTime.UtcNow.AddHours(24);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiration,
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static GatewayUser FromPrincipal(ClaimsPrincipal user)
        {
            var allowedRaw = user.FindFirst(AllowedFilterCitiesClaim)?.Value ?? "";
            var allowed = string.IsNullOrWhiteSpace(allowedRaw)
                ? []
                : allowedRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            var homeCity = user.FindFirst(HomeCityValueClaim)?.Value
                           ?? user.FindFirst("CityValue")?.Value
                           ?? "";
            var userType = user.FindFirst("UserType")?.Value ?? "";
            return new GatewayUser
            {
                UserID = user.FindFirst("UserID")?.Value ?? "",
                UserName = user.FindFirst("UserName")?.Value ?? "",
                UserType = userType,
                CityLink = user.FindFirst("CityLink")?.Value ?? "",
                CityName = user.FindFirst("CityName")?.Value ?? "",
                CityValue = homeCity,
                BranchToken = user.FindFirst("BranchToken")?.Value ?? "",
                IsCentral = string.Equals(user.FindFirst("Central")?.Value, "true", StringComparison.OrdinalIgnoreCase),
                AllowedFilterCities = allowed,
                IsSalesFilterEmployee = string.Equals(userType, "موظف فلترة المبيعات", StringComparison.Ordinal)
            };
        }
    }

    public class GatewayUser
    {
        public string UserID { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserType { get; set; } = "";
        public string CityLink { get; set; } = "";
        public string CityName { get; set; } = "";
        public string CityValue { get; set; } = "";
        public string BranchToken { get; set; } = "";
        public bool IsCentral { get; set; }
        public IReadOnlyList<string> AllowedFilterCities { get; set; } = Array.Empty<string>();
        public bool IsSalesFilterEmployee { get; set; }

        public bool AllowsFilterCity(string? cityValue) =>
            !string.IsNullOrWhiteSpace(cityValue)
            && AllowedFilterCities.Any(c => string.Equals(c, cityValue.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
