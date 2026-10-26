using System.Security.Cryptography;
using System.Text;

namespace BE_DelegateWebApplication.Utilities;

/// <summary>
/// PBKDF2 password hashing aligned with BE_Company.Utilities.PasswordHasher.
/// Format: pbkdf2$iterations$saltBase64$hashBase64
/// </summary>
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

    public static bool IsHashed(string? stored) =>
        !string.IsNullOrEmpty(stored) &&
        stored.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool Verify(string password, string? stored, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(stored))
        {
            return false;
        }

        if (IsHashed(stored))
        {
            return VerifyHashed(password, stored, out needsUpgrade);
        }

        var left = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(stored));
        var ok = CryptographicOperations.FixedTimeEquals(left, right);
        needsUpgrade = ok;
        return ok;
    }

    private static bool VerifyHashed(string password, string stored, out bool needsUpgrade)
    {
        needsUpgrade = false;
        var parts = stored.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations < 10_000)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actual = Pbkdf2(password, salt, iterations);
        var ok = CryptographicOperations.FixedTimeEquals(actual, expected);
        needsUpgrade = ok && iterations < DefaultIterations;
        return ok;
    }

    private static byte[] Pbkdf2(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySize);
}
