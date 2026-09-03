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
            if (string.IsNullOrWhiteSpace(UserName) ||
                !string.Equals(userName?.Trim(), UserName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var expected = _configuration["SalesManagerAccount:Password"];
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var left = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
