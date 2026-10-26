namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Arabic name tokenization for SalesRequest automatic evaluation.
    /// Normalization is for matching only — never mutates stored DB names.
    /// </summary>
    public static class SalesRequestNameSimilarity
    {
        private static readonly HashSet<char> Diacritics =
        [
            '\u064B', '\u064C', '\u064D', '\u064E', '\u064F', '\u0650', '\u0651', '\u0652',
            '\u0670', '\u0653', '\u0654', '\u0655'
        ];

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = new List<char>(value.Length);
            foreach (var c in value.Trim())
            {
                if (Diacritics.Contains(c) || c == 'ـ')
                {
                    continue;
                }

                if (char.IsPunctuation(c) || char.IsSymbol(c))
                {
                    continue;
                }

                chars.Add(c switch
                {
                    'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                    'ى' => 'ي',
                    'ة' => 'ه',
                    _ => c
                });
            }

            var text = new string(chars.ToArray());
            var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        /// <summary>Name tokens with trailing pure-numeric tokens removed (e.g. "… سرحان 2").</summary>
        public static IReadOnlyList<string> MatchingTokens(string? value)
        {
            var normalized = Normalize(value);
            if (normalized.Length == 0)
            {
                return [];
            }

            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            while (parts.Count > 0 && parts[^1].All(char.IsDigit))
            {
                parts.RemoveAt(parts.Count - 1);
            }

            return parts;
        }

        public static (string First, string Father, string Grandfather) TripleParts(string? value)
        {
            var tokens = MatchingTokens(value);
            return (
                tokens.ElementAtOrDefault(0) ?? string.Empty,
                tokens.ElementAtOrDefault(1) ?? string.Empty,
                tokens.ElementAtOrDefault(2) ?? string.Empty);
        }

        /// <summary>Exact match of first three normalized name tokens (after dropping trailing numerics).</summary>
        public static bool IsTripleNameMatch(string? queryName, string? candidateName)
        {
            var q = MatchingTokens(queryName);
            var c = MatchingTokens(candidateName);
            if (q.Count < 3 || c.Count < 3)
            {
                return false;
            }

            return string.Equals(q[0], c[0], StringComparison.Ordinal)
                   && string.Equals(q[1], c[1], StringComparison.Ordinal)
                   && string.Equals(q[2], c[2], StringComparison.Ordinal);
        }

        /// <summary>
        /// Kinship: candidate father+grandfather tokens (positions 1 and 2) must both match
        /// the query father+grandfather pair in the same order after normalization.
        /// First name is ignored. Single-token father-only or grandfather-only is not a match.
        /// </summary>
        public static bool IsFatherOrGrandfatherMatch(string? queryName, string? candidateName)
        {
            var (_, father, grandfather) = TripleParts(queryName);
            if (father.Length == 0 || grandfather.Length == 0)
            {
                return false;
            }

            var c = MatchingTokens(candidateName);
            if (c.Count < 3)
            {
                return false;
            }

            var cFather = c[1];
            var cGrandfather = c[2];
            return string.Equals(father, cFather, StringComparison.Ordinal)
                   && string.Equals(grandfather, cGrandfather, StringComparison.Ordinal);
        }

        public static string FatherGrandfatherMatchReason(string? queryName, string? candidateName)
        {
            return IsFatherOrGrandfatherMatch(queryName, candidateName)
                ? "تطابق اسم الأب والجد"
                : string.Empty;
        }

        /// <summary>
        /// Son↔father: A's tokens[1],[2] equal B's tokens[0],[1] (or reverse).
        /// Example: request «حسين أحمد محمد» matches customer «أحمد محمد علي».
        /// The side with father+grandfather pair needs ≥3 tokens; the other ≥2.
        /// </summary>
        public static bool IsSonFatherMatch(string? queryName, string? candidateName)
        {
            var q = MatchingTokens(queryName);
            var c = MatchingTokens(candidateName);
            return IsSonFatherTokenMatch(q, c) || IsSonFatherTokenMatch(c, q);
        }

        /// <summary>True when <paramref name="longer"/>[1],[2] equals <paramref name="shorter"/>[0],[1].</summary>
        private static bool IsSonFatherTokenMatch(IReadOnlyList<string> longer, IReadOnlyList<string> shorter)
        {
            if (longer.Count < 3 || shorter.Count < 2)
                return false;
            if (longer[1].Length == 0 || longer[2].Length == 0)
                return false;
            if (shorter[0].Length == 0 || shorter[1].Length == 0)
                return false;

            return string.Equals(longer[1], shorter[0], StringComparison.Ordinal)
                   && string.Equals(longer[2], shorter[1], StringComparison.Ordinal);
        }

        public static string SonFatherMatchReason(string? queryName, string? candidateName)
        {
            return IsSonFatherMatch(queryName, candidateName)
                ? "تطابق ابن مع أب"
                : string.Empty;
        }
    }
}
