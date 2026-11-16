using System.Text.RegularExpressions;

namespace BE_PromotersApp.Domain.Users;

/// <summary>Iraqi mobile numbers only. Canonical form 07XXXXXXXXX (same as the company system's SalesPhoneNormalizer.ForStorage).</summary>
public static partial class IraqiPhone
{
    /// <summary>Returns 07XXXXXXXXX or null when the input is not an Iraqi mobile number.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(ToLatinDigits(input).Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00964", StringComparison.Ordinal)) digits = digits[5..];
        else if (digits.StartsWith("964", StringComparison.Ordinal)) digits = digits[3..];
        if (digits.StartsWith('0')) digits = digits[1..];
        if (digits.Length != 10) return null;
        var canonical = "0" + digits;
        return Canonical().IsMatch(canonical) ? canonical : null;
    }

    /// <summary>+9647XXXXXXXXX for SMS providers.</summary>
    public static string ToInternational(string canonical) => "+964" + canonical[1..];

    public static string Mask(string canonical) =>
        canonical.Length == 11 ? $"{canonical[..4]}****{canonical[^3..]}" : "****";

    private static string ToLatinDigits(string s)
    {
        var chars = s.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c is >= '٠' and <= '٩') chars[i] = (char)('0' + (c - '٠'));      // Arabic-Indic
            else if (c is >= '۰' and <= '۹') chars[i] = (char)('0' + (c - '۰')); // Persian
        }
        return new string(chars);
    }

    [GeneratedRegex(@"^07[3-9]\d{8}$")]
    private static partial Regex Canonical();
}
