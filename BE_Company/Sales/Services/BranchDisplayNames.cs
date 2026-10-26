namespace BE_Company.Sales.Services;

/// <summary>Maps gateway/branch catalog keys to Arabic province labels for UI.</summary>
public static class BranchDisplayNames
{
    private static readonly Dictionary<string, string> ByCatalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DatabaseCompanyBasra"] = "البصرة",
        ["DatabaseCompanyBasraAlanwar"] = "البصرة - الأنوار",
        ["DatabaseCompanyNajaf"] = "النجف",
        ["DatabaseCompanyNajaf_DEMO"] = "النجف - DEMO",
        ["DatabaseCompanyKarbala"] = "كربلاء",
        ["DatabaseCompanyKarkh"] = "بغداد - الكرخ",
        ["DatabaseCompanyBaghdadKarkh"] = "بغداد - الكرخ",
        ["DatabaseCompanyRusafa"] = "بغداد - الرصافة",
        ["DatabaseCompanyBaghdadRusafa"] = "بغداد - الرصافة",
        ["DatabaseCompanyDiyala"] = "ديالى",
        ["DatabaseCompanyKirkuk"] = "كركوك",
        ["DatabaseCompanyMosul"] = "الموصل",
        ["DatabaseCompanyNineveh"] = "نينوى",
        ["DatabaseCompanyKot"] = "واسط",
        ["DatabaseCompanyWasit"] = "واسط",
        ["DatabaseCompanyMaysan"] = "ميسان",
        ["DatabaseCompanyNasiriyah"] = "ذي قار",
        ["DatabaseCompanyDhiQar"] = "ذي قار",
        ["DatabaseCompanyMuthanna"] = "المثنى",
        ["DatabaseCompanyDiwaniyah"] = "الديوانية",
        ["DatabaseCompanyQadisiyyah"] = "الديوانية",
        ["DatabaseCompanyHilla"] = "بابل",
        ["DatabaseCompanyBabil"] = "بابل",
        ["DatabaseCompanyKarak"] = "الكرخ - عقيل",
        ["DatabaseCompanyKarakAqeel"] = "الكرخ - عقيل",
        ["DatabaseCompanyRusafaAqeel"] = "الرصافة - عقيل",
        ["najaf-demo"] = "النجف - DEMO",
    };

    public static bool LooksLikeCatalogKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        var v = value.Trim();
        return v.StartsWith("DatabaseCompany", StringComparison.OrdinalIgnoreCase)
               || v.Contains("_DEMO", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "najaf-demo", StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryMap(string? catalogOrKey)
    {
        if (string.IsNullOrWhiteSpace(catalogOrKey))
            return null;
        return ByCatalog.TryGetValue(catalogOrKey.Trim(), out var name) ? name : null;
    }

    /// <summary>Prefer a human branch name; otherwise map catalog/id to Arabic.</summary>
    public static string Resolve(string? branchName, string? branchId)
    {
        if (!string.IsNullOrWhiteSpace(branchName) && !LooksLikeCatalogKey(branchName))
            return branchName.Trim();

        return TryMap(branchName)
               ?? TryMap(branchId)
               ?? (string.IsNullOrWhiteSpace(branchName) ? (branchId ?? "").Trim() : branchName.Trim());
    }
}
