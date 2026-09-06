namespace BE_Company.Sales.Services
{
    public static class SalesCityDisplay
    {
        public static bool IsInternalKey(string? value, string? cityValue = null)
        {
            var text = value?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(cityValue)
                && string.Equals(text, cityValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (text.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return text.EndsWith("_DEMO", StringComparison.OrdinalIgnoreCase)
                   && text.IndexOfAny([' ', '\u00A0']) < 0
                   && text.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        public static string HumanName(string? candidate, string? fallbackName, string? internalKey = null)
        {
            foreach (var item in new[] { candidate, fallbackName })
            {
                if (!IsInternalKey(item, internalKey))
                {
                    return item!.Trim();
                }
            }

            return fallbackName?.Trim() ?? string.Empty;
        }
    }
}
