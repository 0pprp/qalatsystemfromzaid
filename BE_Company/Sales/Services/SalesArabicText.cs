namespace BE_Company.Sales.Services
{
    public static class SalesArabicText
    {
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value
                .Replace("أ", "ا", StringComparison.Ordinal)
                .Replace("إ", "ا", StringComparison.Ordinal)
                .Replace("آ", "ا", StringComparison.Ordinal)
                .Replace("ٱ", "ا", StringComparison.Ordinal)
                .Replace("ى", "ي", StringComparison.Ordinal)
                .Replace("ـ", "", StringComparison.Ordinal);

            var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }
    }

    public static class SalesCustomerNameMatch
    {
        public const int MinimumLength = 2;
        public const int MaxNames = 1500;
        public const int MaxMatchesPerName = 80;

        /// <summary>
        /// Exact normalized name, or a longer stored name that starts with the query plus a space.
        /// "حسين محمد" matches "حسين محمد علي" but not "علي حسين محمد" and not a lone "حسين".
        /// </summary>
        public static bool IsLogicalMatch(string? storedName, string normalizedQuery)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return false;
            }

            var stored = SalesArabicText.Normalize(storedName);
            if (stored.Length == 0)
            {
                return false;
            }

            if (string.Equals(stored, normalizedQuery, StringComparison.Ordinal))
            {
                return true;
            }

            if (normalizedQuery.IndexOf(' ') < 0)
            {
                return false;
            }

            return stored.StartsWith(normalizedQuery + " ", StringComparison.Ordinal);
        }
    }
}
