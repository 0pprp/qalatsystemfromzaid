using System.Text.RegularExpressions;
using BE_Company.DTO;
using BE_Company.Sales.Services;

namespace BE_Company.Services.LegalSearch;

/// <summary>
/// Arabic fold for accountant legal search: alef variants, ة→ه, ya, tatweel, device suffixes.
/// </summary>
public static class LegalArabicText
{
    // Trailing sale/device tags common in customers.xlsx export: -A51, -Y9a, A11/2, -Y9s+y7p
    private static readonly Regex DeviceSuffix = new(
        @"(?:[\s\-_/]*(?:[A-Za-z]\d[\w+./]*|\d+[A-Za-z][\w+./]*))$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        text = DeviceSuffix.Replace(text, string.Empty);

        text = text
            .Replace("أ", "ا", StringComparison.Ordinal)
            .Replace("إ", "ا", StringComparison.Ordinal)
            .Replace("آ", "ا", StringComparison.Ordinal)
            .Replace("ٱ", "ا", StringComparison.Ordinal)
            .Replace("ة", "ه", StringComparison.Ordinal)
            .Replace("ى", "ي", StringComparison.Ordinal)
            .Replace("ـ", "", StringComparison.Ordinal);

        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts);
    }
}

public static class LegalCustomerNameMatch
{
    public const int MinimumLength = 2;
    public const int MaxQueries = 1500;
    public const int MaxMatchesPerQuery = 80;

    /// <summary>Full triple-name logical match only — never family/brother search.</summary>
    public static bool IsNameMatch(string? storedName, string? queryName)
    {
        var query = LegalArabicText.Normalize(queryName);
        if (query.Length < MinimumLength)
        {
            return false;
        }

        // Reuse logical rules but with legal normalize on both sides.
        var stored = LegalArabicText.Normalize(storedName);
        if (stored.Length == 0)
        {
            return false;
        }

        if (string.Equals(stored, query, StringComparison.Ordinal))
        {
            return true;
        }

        if (query.IndexOf(' ') < 0)
        {
            return false;
        }

        return stored.StartsWith(query + " ", StringComparison.Ordinal);
    }
}

public sealed class LegalSearchQueryInput
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
}

public sealed class LegalSearchQueryResult
{
    public int Index { get; set; }
    public string? RequestedName { get; set; }
    public string? RequestedPhone { get; set; }
    public List<CustomersGetDTO> NameMatches { get; set; } = new();
    public List<CustomersGetDTO> PhoneMatches { get; set; } = new();
}

public sealed class LegalSearchExportRow
{
    public CustomersGetDTO Customer { get; set; } = new();
    public List<string> MatchKinds { get; set; } = new();
    public int SourceQueryIndex { get; set; }
    public string? RequestedName { get; set; }
    public string? RequestedPhone { get; set; }
}

public sealed class LegalSearchEngineResult
{
    public List<LegalSearchQueryResult> Queries { get; set; } = new();
    public List<LegalSearchExportRow> ExportRows { get; set; } = new();
    public int NameHitCount { get; set; }
    public int PhoneHitCount { get; set; }
    public int UniqueCustomerCount { get; set; }
}

public static class LegalCustomerSearchEngine
{
    public static LegalSearchEngineResult Run(
        IReadOnlyList<CustomersGetDTO> catalog,
        IEnumerable<LegalSearchQueryInput> queries)
    {
        var result = new LegalSearchEngineResult();
        var exportById = new Dictionary<int, LegalSearchExportRow>();
        var index = 0;

        foreach (var q in queries.Take(LegalCustomerNameMatch.MaxQueries))
        {
            var name = (q.Name ?? string.Empty).Trim();
            var phone = (q.Phone ?? string.Empty).Trim();
            if (name.Length == 0 && phone.Length == 0)
            {
                continue;
            }

            var qr = new LegalSearchQueryResult
            {
                Index = index,
                RequestedName = string.IsNullOrWhiteSpace(name) ? null : name,
                RequestedPhone = string.IsNullOrWhiteSpace(phone) ? null : phone
            };

            if (!string.IsNullOrWhiteSpace(name))
            {
                foreach (var c in catalog)
                {
                    if (c.CustomerID is null or <= 0)
                    {
                        continue;
                    }

                    if (!LegalCustomerNameMatch.IsNameMatch(c.CustomerName, name))
                    {
                        continue;
                    }

                    qr.NameMatches.Add(c);
                    if (qr.NameMatches.Count >= LegalCustomerNameMatch.MaxMatchesPerQuery)
                    {
                        break;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                foreach (var c in catalog)
                {
                    if (c.CustomerID is null or <= 0)
                    {
                        continue;
                    }

                    if (!SalesPhoneNormalizer.Matches(c.PhoneNumber, phone))
                    {
                        continue;
                    }

                    qr.PhoneMatches.Add(c);
                    if (qr.PhoneMatches.Count >= LegalCustomerNameMatch.MaxMatchesPerQuery)
                    {
                        break;
                    }
                }
            }

            result.NameHitCount += qr.NameMatches.Count;
            result.PhoneHitCount += qr.PhoneMatches.Count;

            void AddExport(CustomersGetDTO c, string kind)
            {
                var id = c.CustomerID!.Value;
                if (!exportById.TryGetValue(id, out var row))
                {
                    row = new LegalSearchExportRow
                    {
                        Customer = c,
                        SourceQueryIndex = index,
                        RequestedName = qr.RequestedName,
                        RequestedPhone = qr.RequestedPhone
                    };
                    exportById[id] = row;
                    result.ExportRows.Add(row);
                }

                if (!row.MatchKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
                {
                    row.MatchKinds.Add(kind);
                }
            }

            foreach (var c in qr.NameMatches)
            {
                AddExport(c, "name");
            }

            foreach (var c in qr.PhoneMatches)
            {
                AddExport(c, "phone");
            }

            result.Queries.Add(qr);
            index++;
        }

        result.UniqueCustomerCount = exportById.Count;
        return result;
    }
}
