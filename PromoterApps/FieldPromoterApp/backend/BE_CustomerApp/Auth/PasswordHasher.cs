using System.Security.Cryptography;

namespace BE_CustomerApp.Auth;

/// <summary>PBKDF2-SHA256 password hashes stored as <c>pbkdf2$iterations$salt$hash</c> (base64).</summary>
public static class PasswordHasher
{
    private const int Iterations = 120_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Dashboard rule for new passwords: 6–64 characters.</summary>
    /// <summary>Any non-empty password is accepted (the administration chooses it). 256 characters at most.</summary>
    public static string? Validate(string? password) =>
        string.IsNullOrEmpty(password) ? "أدخل كلمة المرور"
        : password.Length > 256 ? "كلمة المرور طويلة جداً"
        : null;
}
