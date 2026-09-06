using Microsoft.AspNetCore.Http;

namespace BE_Company.Sales.Services
{
    public static class SalesIraqPhone
    {
        public const string Message = "يجب أن يكون رقم الهاتف 11 رقم ويبدأ بـ 07";

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("964", StringComparison.Ordinal) && digits.Length >= 13)
            {
                digits = "0" + digits[3..];
            }

            return digits.Length > 11 ? digits[..11] : digits;
        }

        public static bool IsValid(string? value)
        {
            var digits = Normalize(value);
            return digits.Length == 11 && digits.StartsWith("07", StringComparison.Ordinal);
        }

        public static void RequireIfPresent(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!IsValid(value))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, Message);
            }
        }
    }
}
