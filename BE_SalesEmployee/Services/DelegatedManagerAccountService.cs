using System.Security.Cryptography;
using System.Text;

namespace BE_SalesEmployee.Services;

public sealed class CentralDelegatedManagerIdentity
{
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>Config-backed delegated-manager accounts (no city). Mirrors SalesManagerAccountService.</summary>
public sealed class DelegatedManagerAccountService
{
    private readonly IConfiguration _configuration;

    public DelegatedManagerAccountService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool TryAuthenticate(string? userName, string? password, out CentralDelegatedManagerIdentity? identity)
    {
        identity = null;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var normalized = userName.Trim();
        var primaryUser = _configuration["DelegatedManagerAccount:UserName"] ?? "";
        var primaryPass = _configuration["DelegatedManagerAccount:Password"];
        var primaryDisplay = _configuration["DelegatedManagerAccount:DisplayName"] ?? "المدير المفوض";

        if (!string.IsNullOrWhiteSpace(primaryUser) &&
            string.Equals(normalized, primaryUser, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(primaryPass) &&
            PasswordEquals(password, primaryPass))
        {
            identity = new CentralDelegatedManagerIdentity
            {
                UserName = primaryUser.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(primaryDisplay) ? primaryUser.Trim() : primaryDisplay.Trim(),
            };
            return true;
        }

        foreach (var child in _configuration.GetSection("DelegatedManagerAccounts").GetChildren())
        {
            var u = child["UserName"];
            var p = child["Password"];
            var d = child["DisplayName"];
            if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p))
            {
                continue;
            }

            if (!string.Equals(normalized, u.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!PasswordEquals(password, p))
            {
                continue;
            }

            identity = new CentralDelegatedManagerIdentity
            {
                UserName = u.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(d) ? u.Trim() : d.Trim(),
            };
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
