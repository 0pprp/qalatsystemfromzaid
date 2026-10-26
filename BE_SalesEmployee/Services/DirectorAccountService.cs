using System.Security.Cryptography;
using System.Text;

namespace BE_SalesEmployee.Services;

public sealed class CentralDirectorIdentity
{
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>Config-backed central director accounts (all provinces). Mirrors SalesManagerAccountService.</summary>
public sealed class DirectorAccountService
{
    private readonly IConfiguration _configuration;

    public DirectorAccountService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// True when at least one director password is configured.
    /// Missing config must fail closed — no bootstrap credentials.
    /// </summary>
    public bool HasConfiguredAccounts()
    {
        var primaryUser = _configuration["DirectorAccount:UserName"];
        var primaryPass = _configuration["DirectorAccount:Password"];
        if (!string.IsNullOrWhiteSpace(primaryUser) && !string.IsNullOrWhiteSpace(primaryPass))
        {
            return true;
        }

        foreach (var child in _configuration.GetSection("DirectorAccounts").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child["UserName"]) &&
                !string.IsNullOrWhiteSpace(child["Password"]))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAuthenticate(string? userName, string? password, out CentralDirectorIdentity? identity)
    {
        identity = null;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        if (!HasConfiguredAccounts())
        {
            return false;
        }

        var normalized = NormalizeUserName(userName);
        var primaryUser = _configuration["DirectorAccount:UserName"] ?? "";
        var primaryPass = _configuration["DirectorAccount:Password"];
        var primaryDisplay = _configuration["DirectorAccount:DisplayName"] ?? "المدير";

        if (!string.IsNullOrWhiteSpace(primaryUser) &&
            UserNamesEqual(normalized, primaryUser) &&
            !string.IsNullOrWhiteSpace(primaryPass) &&
            PasswordEquals(password, primaryPass))
        {
            identity = new CentralDirectorIdentity
            {
                UserName = primaryUser.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(primaryDisplay) ? primaryUser.Trim() : primaryDisplay.Trim(),
            };
            return true;
        }

        foreach (var child in _configuration.GetSection("DirectorAccounts").GetChildren())
        {
            var u = child["UserName"];
            var p = child["Password"];
            var d = child["DisplayName"];
            if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p))
            {
                continue;
            }

            if (!UserNamesEqual(normalized, u))
            {
                continue;
            }

            if (!PasswordEquals(password, p))
            {
                continue;
            }

            identity = new CentralDirectorIdentity
            {
                UserName = u.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(d) ? u.Trim() : d.Trim(),
            };
            return true;
        }

        return false;
    }

    internal static string NormalizeUserName(string userName)
    {
        var trimmed = userName.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            sb.Append(ch switch
            {
                '\u0623' or '\u0625' or '\u0622' or '\u0671' => '\u0627',
                _ => ch
            });
        }

        return sb.ToString();
    }

    private static bool UserNamesEqual(string suppliedNormalized, string configured) =>
        string.Equals(
            NormalizeUserName(suppliedNormalized),
            NormalizeUserName(configured),
            StringComparison.OrdinalIgnoreCase);

    private static bool PasswordEquals(string supplied, string expected)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
