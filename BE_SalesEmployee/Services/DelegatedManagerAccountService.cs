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

        var normalized = NormalizeUserName(userName);
        var primaryUser = _configuration["DelegatedManagerAccount:UserName"] ?? "";
        var primaryPass = _configuration["DelegatedManagerAccount:Password"];
        var primaryDisplay = _configuration["DelegatedManagerAccount:DisplayName"] ?? "المدير المفوض";

        if (!string.IsNullOrWhiteSpace(primaryUser) &&
            UserNamesEqual(normalized, primaryUser) &&
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

            if (!UserNamesEqual(normalized, u))
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

    /// <summary>Configured DM accounts (login id + display). Used for assignee pickers.</summary>
    public IReadOnlyList<CentralDelegatedManagerIdentity> ListAccounts()
    {
        var list = new List<CentralDelegatedManagerIdentity>();
        var primaryUser = _configuration["DelegatedManagerAccount:UserName"] ?? "";
        var primaryDisplay = _configuration["DelegatedManagerAccount:DisplayName"] ?? "المدير المفوض";
        if (!string.IsNullOrWhiteSpace(primaryUser))
        {
            list.Add(new CentralDelegatedManagerIdentity
            {
                UserName = primaryUser.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(primaryDisplay) ? primaryUser.Trim() : primaryDisplay.Trim(),
            });
        }

        foreach (var child in _configuration.GetSection("DelegatedManagerAccounts").GetChildren())
        {
            var u = child["UserName"];
            var d = child["DisplayName"];
            if (string.IsNullOrWhiteSpace(u))
                continue;

            list.Add(new CentralDelegatedManagerIdentity
            {
                UserName = u.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(d) ? u.Trim() : d.Trim(),
            });
        }

        return list;
    }

    /// <summary>
    /// Maps JWT UserName (login id or display name) to the stable login UserName used as AssigneeKey.
    /// </summary>
    public string ResolveStableKey(string? jwtUserName)
    {
        var claim = (jwtUserName ?? "").Trim();
        if (claim.Length == 0)
            return claim;

        foreach (var account in ListAccounts())
        {
            if (UserNamesEqual(NormalizeUserName(claim), account.UserName)
                || string.Equals(claim, account.DisplayName, StringComparison.OrdinalIgnoreCase)
                || UserNamesEqual(NormalizeUserName(claim), account.DisplayName))
            {
                return account.UserName;
            }
        }

        return claim;
    }

    /// <summary>
    /// Arabic keyboards often insert alef-with-hamza (أ/إ/آ) while config stores plain alef (ا).
    /// Normalize both sides so login matches what ops typed on the server.
    /// </summary>
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
                '\u0623' or // أ ALEF WITH HAMZA ABOVE
                '\u0625' or // إ ALEF WITH HAMZA BELOW
                '\u0622' or // آ ALEF WITH MADDA ABOVE
                '\u0671' // ٱ ALEF WASLA
                    => '\u0627', // ا ALEF
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
