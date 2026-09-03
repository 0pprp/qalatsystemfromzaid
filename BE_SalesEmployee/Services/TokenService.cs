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
            return new GatewayUser
            {
                UserID = user.FindFirst("UserID")?.Value ?? "",
                UserName = user.FindFirst("UserName")?.Value ?? "",
                UserType = user.FindFirst("UserType")?.Value ?? "",
                CityLink = user.FindFirst("CityLink")?.Value ?? "",
                CityName = user.FindFirst("CityName")?.Value ?? "",
                CityValue = user.FindFirst("CityValue")?.Value ?? "",
                BranchToken = user.FindFirst("BranchToken")?.Value ?? "",
                IsCentral = string.Equals(user.FindFirst("Central")?.Value, "true", StringComparison.OrdinalIgnoreCase)
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
    }
}
