using System.Text;

namespace BE_Company.Sales.Services;

/// <summary>
/// Phone matching for the customer app: legacy Customers.PhoneNumber values may contain spaces,
/// dashes, "+964", brackets… The SQL expression strips the same characters as <see cref="StripStored"/>
/// and compares the last 10 digits with <see cref="SalesPhoneNormalizer.ForMatch"/> of the app phone.
/// </summary>
public static class CustomerAppPhoneMatch
{
    // (C# char, T-SQL literal) — one REPLACE per entry; keep both columns in sync.
    private static readonly (char Char, string Sql)[] StrippedChars =
    [
        (' ', "N' '"),
        ('-', "N'-'"),
        ('+', "N'+'"),
        ('(', "N'('"),
        (')', "N')'"),
        ('.', "N'.'"),
        (' ', "NCHAR(160)"),
        ('\t', "NCHAR(9)"),
        ('\r', "NCHAR(13)"),
        ('\n', "NCHAR(10)"),
    ];

    public static IReadOnlyList<char> StrippedCharacters => StrippedChars.Select(c => c.Char).ToArray();

    /// <summary>App-side key: 10 digits 7XXXXXXXXX, or empty when the phone is not a valid Iraqi mobile.</summary>
    public static string ToMatchKey(string? phone) => SalesPhoneNormalizer.ForMatch(phone);

    /// <summary>C# mirror of <see cref="StrippedSql"/>: removes the stripped characters only.</summary>
    public static string StripStored(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(stored.Length);
        foreach (var ch in stored)
        {
            if (Array.FindIndex(StrippedChars, c => c.Char == ch) < 0)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    /// <summary>C# mirror of <see cref="MatchPredicateSql"/> (RIGHT(stripped, 10) = key).</summary>
    public static bool StoredMatches(string? stored, string matchKey)
    {
        if (matchKey.Length != 10)
        {
            return false;
        }

        var stripped = StripStored(stored);
        var right10 = stripped.Length <= 10 ? stripped : stripped[^10..];
        return string.Equals(right10, matchKey, StringComparison.Ordinal);
    }

    /// <summary>T-SQL expression: column with every stripped character removed.</summary>
    public static string StrippedSql(string column)
    {
        var expr = $"ISNULL({column}, N'')";
        foreach (var (_, sql) in StrippedChars)
        {
            expr = $"REPLACE({expr}, {sql}, N'')";
        }

        return expr;
    }

    /// <summary>T-SQL predicate comparing the stripped column's last 10 chars with a parameter.</summary>
    public static string MatchPredicateSql(string column, string parameter) =>
        $"RIGHT({StrippedSql(column)}, 10) = {parameter}";
}
