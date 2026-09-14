using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BE_SalesEmployee.Sales.Services;

/// <summary>
/// Loads BE_Company JWT signing keys for CompanyJwt (Main Accountant). Never falls back to gateway Jwt:Key.
/// </summary>
public static class CompanyJwtSigningConfig
{
    public static IReadOnlyList<SecurityKey> LoadRequired(IConfiguration configuration)
    {
        var keys = new List<SecurityKey>();
        foreach (var child in configuration.GetSection("Authentication:Schemes:Bearer:SigningKeys").GetChildren())
        {
            var value = child["Value"];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            try
            {
                keys.Add(new SymmetricSecurityKey(Convert.FromBase64String(value)));
            }
            catch (FormatException)
            {
                // Skip malformed entries; emptiness is validated below.
            }
        }

        if (keys.Count == 0)
        {
            throw new InvalidOperationException(
                "CompanyJwt requires Authentication:Schemes:Bearer:SigningKeys (BE_Company JWT keys). " +
                "Do not reuse Jwt:Key for global-managers authorization.");
        }

        return keys;
    }

    public static string Fingerprint12(SecurityKey key)
    {
        var bytes = key switch
        {
            SymmetricSecurityKey sym => sym.Key,
            _ => Encoding.UTF8.GetBytes(key.KeyId ?? key.ToString() ?? "unknown")
        };
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant()[..12];
    }
}
