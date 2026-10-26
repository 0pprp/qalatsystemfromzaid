using System.Security.Cryptography;
using System.Text;

namespace BE_SalesEmployee.Services;

public sealed class CentralMonitorIdentity
{
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>Config-backed central monitor accounts (عمل المتابعين). Mirrors DirectorAccountService.</summary>
public sealed class MonitorAccountService
{
    private readonly IConfiguration _configuration;

    public MonitorAccountService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool HasConfiguredAccounts()
    {
        var primaryUser = _configuration["MonitorAccount:UserName"];
        var primaryPass = _configuration["MonitorAccount:Password"];
        if (!string.IsNullOrWhiteSpace(primaryUser) && !string.IsNullOrWhiteSpace(primaryPass))
        {
            return true;
        }

        foreach (var child in _configuration.GetSection("MonitorAccounts").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(child["UserName"]) &&
                !string.IsNullOrWhiteSpace(child["Password"]))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAuthenticate(string? userName, string? password, out CentralMonitorIdentity? identity)
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

        var normalized = DirectorAccountService.NormalizeUserName(userName);
        var primaryUser = _configuration["MonitorAccount:UserName"] ?? "";
        var primaryPass = _configuration["MonitorAccount:Password"];
        var primaryDisplay = _configuration["MonitorAccount:DisplayName"] ?? "المراقب";

        if (!string.IsNullOrWhiteSpace(primaryUser) &&
            UserNamesEqual(normalized, primaryUser) &&
            !string.IsNullOrWhiteSpace(primaryPass) &&
            PasswordEquals(password, primaryPass))
        {
            identity = new CentralMonitorIdentity
            {
                UserName = primaryUser.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(primaryDisplay) ? primaryUser.Trim() : primaryDisplay.Trim(),
            };
            return true;
        }

        foreach (var child in _configuration.GetSection("MonitorAccounts").GetChildren())
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

            identity = new CentralMonitorIdentity
            {
                UserName = u.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(d) ? u.Trim() : d.Trim(),
            };
            return true;
        }

        return false;
    }

    private static bool UserNamesEqual(string suppliedNormalized, string configured) =>
        string.Equals(
            DirectorAccountService.NormalizeUserName(suppliedNormalized),
            DirectorAccountService.NormalizeUserName(configured),
            StringComparison.OrdinalIgnoreCase);

    private static bool PasswordEquals(string supplied, string expected)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
