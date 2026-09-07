using System.Security.Cryptography;
using System.Text;

namespace BE_SalesEmployee.Services
{
    public sealed class SalesManagerAccountService
    {
        private readonly IConfiguration _configuration;

        public SalesManagerAccountService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string UserName =>
            _configuration["SalesManagerAccount:UserName"] ?? "";

        public string DisplayName =>
            _configuration["SalesManagerAccount:DisplayName"] ?? "مدير المبيعات";

        public bool TryAuthenticate(string? userName, string? password)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var normalizedUserName = userName.Trim();

            // Primary account from configuration
            if (!string.IsNullOrWhiteSpace(UserName) &&
                string.Equals(normalizedUserName, UserName, StringComparison.OrdinalIgnoreCase))
            {
                var expected = _configuration["SalesManagerAccount:Password"];

                if (!string.IsNullOrWhiteSpace(expected) &&
                    PasswordEquals(password, expected))
                {
                    return true;
                }
            }

            // Additional sales manager account
            if (string.Equals(normalizedUserName, "amar1", StringComparison.OrdinalIgnoreCase) &&
                PasswordEquals(password, "amar1"))
            {
                return true;
            }

            return false;
        }

        private static bool PasswordEquals(string supplied, string expected)
        {
            var left = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
            var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
