using System.Security.Cryptography;
using System.Text;

namespace BE_SalesEmployee.Services
{
    /// <summary>Matched central sales-manager identity (never includes password).</summary>
    public sealed class CentralSalesManagerIdentity
    {
        public required string UserName { get; init; }
        public required string DisplayName { get; init; }
    }

    public sealed class SalesManagerAccountService
    {
        private readonly IConfiguration _configuration;

        public SalesManagerAccountService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>Primary (legacy) account user name from SalesManagerAccount:UserName.</summary>
        public string UserName =>
            _configuration["SalesManagerAccount:UserName"] ?? "";

        /// <summary>Primary (legacy) account display name from SalesManagerAccount:DisplayName.</summary>
        public string DisplayName =>
            _configuration["SalesManagerAccount:DisplayName"] ?? "مدير المبيعات";

        /// <summary>
        /// Authenticates against primary SalesManagerAccount, then SalesManagerAccounts:N, then amar1 (temporary).
        /// Returns matched identity (UserName/DisplayName) without password.
        /// </summary>
        public bool TryAuthenticate(string? userName, string? password, out CentralSalesManagerIdentity? identity)
        {
            identity = null;
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            var normalizedUserName = userName.Trim();

            // 1) Primary account from SalesManagerAccount:*
            if (!string.IsNullOrWhiteSpace(UserName) &&
                string.Equals(normalizedUserName, UserName, StringComparison.OrdinalIgnoreCase))
            {
                var expected = _configuration["SalesManagerAccount:Password"];
                if (!string.IsNullOrWhiteSpace(expected) && PasswordEquals(password, expected))
                {
                    identity = new CentralSalesManagerIdentity
                    {
                        UserName = UserName.Trim(),
                        DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? UserName.Trim() : DisplayName.Trim(),
                    };
                    return true;
                }
            }

            // 2) Additional accounts from SalesManagerAccounts:0,1,2...
            foreach (var account in ReadAdditionalAccounts())
            {
                if (string.IsNullOrWhiteSpace(account.UserName) || string.IsNullOrWhiteSpace(account.Password))
                {
                    continue;
                }

                if (!string.Equals(normalizedUserName, account.UserName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!PasswordEquals(password, account.Password))
                {
                    continue;
                }

                var display = string.IsNullOrWhiteSpace(account.DisplayName)
                    ? account.UserName.Trim()
                    : account.DisplayName.Trim();
                identity = new CentralSalesManagerIdentity
                {
                    UserName = account.UserName.Trim(),
                    DisplayName = display,
                };
                return true;
            }

            // 3) Temporary hard-coded account (keep working for now)
            if (string.Equals(normalizedUserName, "amar1", StringComparison.OrdinalIgnoreCase) &&
                PasswordEquals(password, "amar1"))
            {
                // Backward compatible: same DisplayName as primary config (previous behavior).
                identity = new CentralSalesManagerIdentity
                {
                    UserName = "amar1",
                    DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? "amar1" : DisplayName.Trim(),
                };
                return true;
            }

            return false;
        }

        /// <summary>Backward-compatible overload — prefer the out-identity overload.</summary>
        public bool TryAuthenticate(string? userName, string? password) =>
            TryAuthenticate(userName, password, out _);

        private IEnumerable<(string? UserName, string? Password, string? DisplayName)> ReadAdditionalAccounts()
        {
            var section = _configuration.GetSection("SalesManagerAccounts");
            foreach (var child in section.GetChildren())
            {
                yield return (child["UserName"], child["Password"], child["DisplayName"]);
            }
        }

        private static bool PasswordEquals(string supplied, string expected)
        {
            var left = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
            var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
