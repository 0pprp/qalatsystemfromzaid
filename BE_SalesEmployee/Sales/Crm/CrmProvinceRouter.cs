using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.Crm;

/// <summary>
/// Maps CRM governorate labels onto the live sales-branch catalog via
/// <see cref="CrmProvinceCatalog"/> aliases (not raw letter equality).
/// Baghdad must arrive as «بغداد - الكرخ» or «بغداد - الرصافة».
/// </summary>
public static class CrmProvinceRouter
{
    public static CrmProvinceRouteResult Resolve(string? crmProvince, IReadOnlyList<AdminCity> salesBranches)
    {
        var display = (crmProvince ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(display))
        {
            return CrmProvinceRouteResult.Fail("المحافظة مطلوبة لتوجيه طلب المبيعات.");
        }

        var canonicalId = CrmProvinceCatalog.ResolveCanonicalId(display);
        if (canonicalId is null)
        {
            return CrmProvinceRouteResult.Fail($"المحافظة «{display}» غير مربوطة بفرع مبيعات.");
        }

        if (string.Equals(canonicalId, CrmProvinceCatalog.BaghdadBare, StringComparison.Ordinal))
        {
            return CrmProvinceRouteResult.Fail(
                "بغداد تتطلب اختيار الكرخ أو الرصافة قبل إنشاء الطلب.");
        }

        var province = CrmProvinceCatalog.Get(canonicalId);
        if (province is null)
        {
            return CrmProvinceRouteResult.Fail($"المحافظة «{display}» غير مربوطة بفرع مبيعات.");
        }

        salesBranches ??= Array.Empty<AdminCity>();
        var matches = salesBranches
            .Where(c => !string.IsNullOrWhiteSpace(c.Link))
            .Where(c => CrmProvinceCatalog.BranchMatches(province, c.Name, c.Database, c.Value))
            .ToList();

        if (matches.Count == 0)
        {
            return CrmProvinceRouteResult.Fail($"لا يوجد فرع مبيعات نشط للمحافظة «{display}».");
        }

        if (matches.Count == 1)
        {
            return CrmProvinceRouteResult.Success(matches[0]);
        }

        // Prefer shortest Arabic display name, then stable CityValue.
        var ranked = matches
            .OrderBy(c => CrmProvinceCatalog.NormalizeArabic(c.Name).Length)
            .ThenBy(c => c.Value ?? "", StringComparer.OrdinalIgnoreCase)
            .ToList();
        return CrmProvinceRouteResult.Success(ranked[0]);
    }
}

public sealed class CrmProvinceRouteResult
{
    public bool Ok { get; private init; }
    public AdminCity? City { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static CrmProvinceRouteResult Success(AdminCity city) =>
        new() { Ok = true, City = city };

    public static CrmProvinceRouteResult Fail(string message) =>
        new() { Ok = false, ErrorMessage = message };
}
