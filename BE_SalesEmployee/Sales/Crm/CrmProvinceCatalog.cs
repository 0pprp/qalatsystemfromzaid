using System.Globalization;
using System.Text;

namespace BE_SalesEmployee.Sales.Crm;

/// <summary>
/// Canonical Iraqi governorates for CRM → sales-branch routing.
/// Matching is by alias set, never raw letter equality between CRM label and branch name.
/// </summary>
public static class CrmProvinceCatalog
{
    public const string BaghdadBare = "Baghdad";
    public const string BaghdadKarkh = "BaghdadKarkh";
    public const string BaghdadRusafa = "BaghdadRusafa";

    private static readonly ProvinceDef[] Provinces =
    [
        new("Najaf", ["النجف", "نجف"], Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Basra", ["البصرة", "بصرة"], Excludes: ["تجريبي", "DEMO", "قانونية", "انوار", "أنوار"]),
        new("Karbala", ["كربلاء"], Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Diyala", ["ديالى", "ديالي"], Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Kirkuk", ["كركوك"], Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Anbar", ["الانبار", "الأنبار", "انبار", "الرمادي", "رمادي"], Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Salahuddin", ["صلاح الدين", "صلاح", "تكريت"], Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Maysan", ["ميسان", "العمارة", "عمارة"], Excludes: ["تجريبي", "DEMO", "قانونية", "عقيل"]),
        new("Qadisiyyah", ["القادسية", "قادسية", "الديوانية", "ديوانية", "Diwaniyah", "Qadisiyyah"],
            Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Babylon", ["بابل", "الحلة", "حلة", "Hilla", "Babil"],
            Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("DhiQar", ["ذي قار", "ذيقار", "الناصرية", "ناصرية", "Nasiriyah", "DhiQar"],
            Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Nineveh", ["نينوى", "الموصل", "موصل", "Mosul", "Nineveh"],
            Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Muthanna", ["المثنى", "مثنى", "السماوة", "سماوة", "Muthanna"],
            Excludes: ["تجريبي", "DEMO", "قانونية"]),
        new("Wasit", ["واسط", "الكوت", "كوت", "Wasit"],
            Excludes: ["تجريبي", "DEMO", "قانونية", "عقيل", "منهج"]),
        new(BaghdadKarkh, ["بغداد - الكرخ", "بغداد الكرخ", "الكرخ", "كرخ", "Karkh", "BaghdadKarkh"],
            Excludes: ["عقيل", "رصافة", "تجريبي", "DEMO", "قانونية"]),
        new(BaghdadRusafa, ["بغداد - الرصافة", "بغداد الرصافة", "الرصافة", "رصافة", "Rusafa", "BaghdadRusafa"],
            Excludes: ["عقيل", "كرخ", "تجريبي", "DEMO", "قانونية"]),
        // Bare Baghdad is recognized so the router can demand Karkh/Rusafa — not a routable branch.
        new(BaghdadBare, ["بغداد"], Excludes: []),
    ];

    private static readonly Dictionary<string, ProvinceDef> AliasIndex = BuildAliasIndex();

    public static string? ResolveCanonicalId(string? label)
    {
        var keys = AliasKeys(label);
        foreach (var key in keys)
        {
            if (AliasIndex.TryGetValue(key, out var def))
            {
                return def.Id;
            }
        }

        return null;
    }

    public static ProvinceDef? Get(string? canonicalId)
    {
        if (string.IsNullOrWhiteSpace(canonicalId))
        {
            return null;
        }

        return Provinces.FirstOrDefault(p =>
            string.Equals(p.Id, canonicalId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool BranchMatches(ProvinceDef province, string? name, string? database, string? value)
    {
        var hay = NormalizeArabic($"{name} {database} {value}");
        if (string.IsNullOrEmpty(hay))
        {
            return false;
        }

        var hayCompact = hay.Replace(" ", "", StringComparison.Ordinal);
        foreach (var ex in province.Excludes)
        {
            var exNorm = NormalizeArabic(ex);
            if (string.IsNullOrEmpty(exNorm))
            {
                continue;
            }

            var exCompact = exNorm.Replace(" ", "", StringComparison.Ordinal);
            if (hay.Contains(exNorm, StringComparison.Ordinal)
                || hayCompact.Contains(exCompact, StringComparison.Ordinal))
            {
                return false;
            }
        }

        // Prefer resolving the branch display name itself to this canonical province.
        if (string.Equals(ResolveCanonicalId(name), province.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fall back: any alias token appears in name/database/value haystack.
        foreach (var alias in province.Aliases)
        {
            var a = NormalizeArabic(alias);
            if (a.Length < 3)
            {
                continue;
            }

            var aCompact = a.Replace(" ", "", StringComparison.Ordinal);
            if (hay.Contains(a, StringComparison.Ordinal)
                || hayCompact.Contains(aCompact, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (a.StartsWith("ال", StringComparison.Ordinal) && a.Length > 2)
            {
                var stripped = a[2..];
                if (stripped.Length >= 3
                    && (hay.Contains(stripped, StringComparison.Ordinal)
                        || hayCompact.Contains(stripped.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Comparison-only Arabic normalization (does not mutate stored CRM text).</summary>
    public static string NormalizeArabic(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var form = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(form.Length);
        foreach (var ch in form)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var c = ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ة' => 'ه',
                'ى' => 'ي',
                'ؤ' => 'و',
                'ئ' => 'ي',
                _ => ch
            };

            if (char.IsWhiteSpace(c) || c is '-' or '_' or '/' or '\\')
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }

                continue;
            }

            sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    private static Dictionary<string, ProvinceDef> BuildAliasIndex()
    {
        var map = new Dictionary<string, ProvinceDef>(StringComparer.Ordinal);
        foreach (var province in Provinces)
        {
            foreach (var alias in province.Aliases)
            {
                foreach (var key in AliasKeys(alias))
                {
                    map[key] = province;
                }
            }
        }

        return map;
    }

    private static List<string> AliasKeys(string? raw)
    {
        var normalized = NormalizeArabic(raw);
        if (string.IsNullOrEmpty(normalized))
        {
            return [];
        }

        var compact = normalized.Replace(" ", "", StringComparison.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal) { normalized, compact };

        foreach (var variant in new[] { normalized, compact })
        {
            if (variant.StartsWith("ال", StringComparison.Ordinal) && variant.Length > 2)
            {
                keys.Add(variant[2..]);
            }
            else
            {
                keys.Add("ال" + variant);
            }
        }

        return keys.ToList();
    }

    public sealed record ProvinceDef(string Id, string[] Aliases, string[] Excludes);
}
