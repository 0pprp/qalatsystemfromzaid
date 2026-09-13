using Microsoft.AspNetCore.Http;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Shared phone normalization for storage and matching (Excel import, evaluation, CRUD).
    /// Phones are always treated as digit strings — never numeric types.
    /// </summary>
    public static class SalesPhoneNormalizer
    {
        /// <summary>
        /// Canonical 10-digit local mobile without leading zero (e.g. 7804924373).
        /// Used for equality matching only.
        /// </summary>
        public static string ForMatch(string? value)
        {
            var digits = DigitsOnly(value);
            if (digits.Length == 0)
            {
                return string.Empty;
            }

            if (digits.StartsWith("00964", StringComparison.Ordinal) && digits.Length >= 15)
            {
                digits = digits[5..];
            }
            else if (digits.StartsWith("964", StringComparison.Ordinal) && digits.Length >= 13)
            {
                digits = digits[3..];
            }

            if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11)
            {
                digits = digits[1..];
            }

            // Valid Iraqi mobile local part: 10 digits starting with 7
            if (digits.Length == 10 && digits.StartsWith("7", StringComparison.Ordinal))
            {
                return digits;
            }

            return string.Empty;
        }

        /// <summary>
        /// Storage form: 11 digits starting with 07 when input is a valid 10-digit 7XXXXXXXXX
        /// or already a valid 07XXXXXXXXX / +9647… form. Invalid numbers are returned trimmed as-is
        /// (no silent invention) — callers decide validation.
        /// </summary>
        public static string ForStorage(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Preserve non-digit-only garbage as empty after digit extract for Excel numeric cells.
            var match = ForMatch(value);
            if (match.Length == 10)
            {
                return "0" + match;
            }

            // Already normalized via SalesIraqPhone path (11 digits 07…)
            var digits = DigitsOnly(value);
            if (digits.StartsWith("964", StringComparison.Ordinal) && digits.Length >= 13)
            {
                digits = "0" + digits[3..];
            }

            if (digits.Length == 11 && digits.StartsWith("07", StringComparison.Ordinal))
            {
                return digits;
            }

            return value.Trim();
        }

        public static bool IsValidStored(string? value)
        {
            var stored = ForStorage(value);
            return SalesIraqPhone.IsValid(stored);
        }

        public static bool Matches(string? a, string? b)
        {
            var left = ForMatch(a);
            var right = ForMatch(b);
            return left.Length == 10 && right.Length == 10
                   && string.Equals(left, right, StringComparison.Ordinal);
        }

        public static string DigitsOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            // Excel may serialize phone as 7.804924373E9 — reject scientific notation silently as invalid.
            var trimmed = value.Trim();
            if (trimmed.Contains('E', StringComparison.OrdinalIgnoreCase) && trimmed.Contains('.'))
            {
                if (double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var number)
                    && number is >= 7000000000d and <= 7999999999d)
                {
                    return ((long)number).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return new string(trimmed.Where(char.IsDigit).ToArray());
        }

        public static void RequireValidIfPresent(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var stored = ForStorage(value);
            if (!SalesIraqPhone.IsValid(stored))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, SalesIraqPhone.Message);
            }
        }
    }
}
