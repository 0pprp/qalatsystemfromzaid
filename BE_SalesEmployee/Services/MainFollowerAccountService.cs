using System.Security.Cryptography;
using System.Text;

namespace BE_SalesEmployee.Services;

public sealed class CentralMainFollowerIdentity
{
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
}

/// <summary>Config-backed main-follower accounts (central, multi-province via missions).</summary>
public sealed class MainFollowerAccountService
{
    private readonly IConfiguration _configuration;

    public MainFollowerAccountService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool TryAuthenticate(string? userName, string? password, out CentralMainFollowerIdentity? identity)
    {
        identity = null;
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var normalized = DelegatedManagerAccountService.NormalizeUserName(userName);
        foreach (var child in _configuration.GetSection("MainFollowerAccounts").GetChildren())
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

            identity = new CentralMainFollowerIdentity
            {
                UserName = u.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(d) ? u.Trim() : d.Trim(),
            };
            return true;
        }

        return false;
    }

    public IReadOnlyList<CentralMainFollowerIdentity> ListAccounts()
    {
        var list = new List<CentralMainFollowerIdentity>();
        foreach (var child in _configuration.GetSection("MainFollowerAccounts").GetChildren())
        {
            var u = child["UserName"];
            var d = child["DisplayName"];
            if (string.IsNullOrWhiteSpace(u))
            {
                continue;
            }

            list.Add(new CentralMainFollowerIdentity
            {
                UserName = u.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(d) ? u.Trim() : d.Trim(),
            });
        }

        return list;
    }

    private static bool UserNamesEqual(string a, string b) =>
        string.Equals(
            DelegatedManagerAccountService.NormalizeUserName(a),
            DelegatedManagerAccountService.NormalizeUserName(b),
            StringComparison.OrdinalIgnoreCase);

    private static bool PasswordEquals(string provided, string expected)
    {
        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
