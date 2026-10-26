using System.Security.Cryptography;
using System.Text;

namespace BE_SalesEmployee.Utilities;

/// <summary>PBKDF2 password hashing — same format as BE_Company PasswordHasher.</summary>
public static class PasswordHasher
{
    public const string Prefix = "pbkdf2$";
    private const int DefaultIterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password required", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Pbkdf2(password, salt, iterations);
        return $"{Prefix}{iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations < 10_000)
            {
                return false;
            }

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Pbkdf2(password, salt, iterations);
                var ok = CryptographicOperations.FixedTimeEquals(actual, expected);
                needsUpgrade = ok && iterations < DefaultIterations;
                return ok;
            }
            catch
            {
                return false;
            }
        }

        var left = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(stored));
        var match = CryptographicOperations.FixedTimeEquals(left, right);
        needsUpgrade = match;
        return match;
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySize);
}
