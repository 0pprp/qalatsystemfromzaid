using BE_SalesEmployee.Sales.Crm;
using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CrmProvinceRouterTests
{
    private static List<AdminCity> BaseBranches() =>
    [
        new() { Value = "1", Name = "النجف", Database = "DatabaseCompanyNajaf", Link = "http://najaf/api/" },
        new() { Value = "2", Name = "كربلاء", Database = "DatabaseCompanyKarbala", Link = "http://karbala/api/" },
        new() { Value = "3", Name = "الكرخ", Database = "DatabaseCompanyBaghdadKarak", Link = "http://karkh/api/" },
        new() { Value = "4", Name = "الرصافة", Database = "DatabaseCompanyBaghdadRusafa", Link = "http://rusafa/api/" },
        new() { Value = "5", Name = "البصرة", Database = "DatabaseCompanyBasra", Link = "http://basra/api/" },
        new() { Value = "6", Name = "ديالى", Database = "DatabaseCompanyDiyala", Link = "http://diyala/api/" },
        new() { Value = "12", Name = "الديوانية", Database = "DatabaseCompanyDiwaniyah", Link = "http://diw/api/" },
        new() { Value = "13", Name = "الحلة", Database = "DatabaseCompanyHilla", Link = "http://hilla/api/" },
        new() { Value = "14", Name = "الناصرية", Database = "DatabaseCompanyNasiriyah", Link = "http://nas/api/" },
        new() { Value = "8", Name = "الموصل", Database = "DatabaseCompanyMosul", Link = "http://mosul/api/" },
        new() { Value = "10", Name = "الكوت", Database = "DatabaseCompanyKot", Link = "http://kot/api/" },
        new() { Value = "11", Name = "السماوة", Database = "DatabaseCompanyMuthanna", Link = "http://sam/api/" },
        new() { Value = "15", Name = "الكرخ - عقيل", Database = "DatabaseCompanyKarakAqeel", Link = "http://aqeel-k/api/" },
        new() { Value = "16", Name = "ميسان", Database = "DatabaseCompanyMaysan", Link = "http://maysan/api/" },
        new() { Value = "17", Name = "كوت - منهج", Database = "DatabaseCompanyKotManhaj", Link = "http://manhaj/api/" },
    ];

    [Theory]
    [InlineData("النجف", "1")]
    [InlineData("البصرة", "5")]
    [InlineData("كربلاء", "2")]
    [InlineData("ديالى", "6")]
    [InlineData("بغداد - الكرخ", "3")]
    [InlineData("بغداد - الرصافة", "4")]
    public void Mapped_province_resolves_exact_sales_branch(string crmProvince, string expectedValue)
    {
        var result = CrmProvinceRouter.Resolve(crmProvince, BaseBranches());
        Assert.True(result.Ok);
        Assert.Equal(expectedValue, result.City!.Value);
    }

    [Theory]
    [InlineData("القادسية", "12")]
    [InlineData("الديوانية", "12")]
    [InlineData("قادسيه", "12")]
    [InlineData("بابل", "13")]
    [InlineData("الحلة", "13")]
    [InlineData("ذي قار", "14")]
    [InlineData("الناصرية", "14")]
    [InlineData("نينوى", "8")]
    [InlineData("الموصل", "8")]
    [InlineData("واسط", "10")]
    [InlineData("المثنى", "11")]
    public void Alias_labels_resolve_to_alternate_branch_names(string crmProvince, string expectedValue)
    {
        var result = CrmProvinceRouter.Resolve(crmProvince, BaseBranches());
        Assert.True(result.Ok, result.ErrorMessage);
        Assert.Equal(expectedValue, result.City!.Value);
    }

    [Fact]
    public void Baghdad_karkh_does_not_resolve_to_aqeel()
    {
        var result = CrmProvinceRouter.Resolve("بغداد - الكرخ", BaseBranches());
        Assert.True(result.Ok);
        Assert.Equal("3", result.City!.Value);
        Assert.DoesNotContain("عقيل", result.City.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Wasit_does_not_resolve_to_manhaj_kot()
    {
        var result = CrmProvinceRouter.Resolve("واسط", BaseBranches());
        Assert.True(result.Ok);
        Assert.Equal("10", result.City!.Value);
        Assert.DoesNotContain("منهج", result.City.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Plain_baghdad_without_side_is_rejected()
    {
        var result = CrmProvinceRouter.Resolve("بغداد", BaseBranches());
        Assert.False(result.Ok);
        Assert.Contains("كرخ", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void Unmapped_province_is_rejected_without_fallback()
    {
        var result = CrmProvinceRouter.Resolve("أربيل", BaseBranches());
        Assert.False(result.Ok);
        Assert.Null(result.City);
        Assert.Contains("غير مربوطة", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void Known_province_without_live_branch_reports_no_active_branch()
    {
        var onlyNajaf = new List<AdminCity>
        {
            new() { Value = "1", Name = "النجف", Database = "DatabaseCompanyNajaf", Link = "http://najaf/api/" }
        };
        var result = CrmProvinceRouter.Resolve("القادسية", onlyNajaf);
        Assert.False(result.Ok);
        Assert.Contains("لا يوجد فرع", result.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void Blank_province_is_rejected()
    {
        var result = CrmProvinceRouter.Resolve("  ", BaseBranches());
        Assert.False(result.Ok);
    }

    [Theory]
    [InlineData("القادسية", "Qadisiyyah")]
    [InlineData("الديوانية", "Qadisiyyah")]
    [InlineData("بابل", "Babylon")]
    [InlineData("الحلة", "Babylon")]
    [InlineData("ذي قار", "DhiQar")]
    [InlineData("الناصرية", "DhiQar")]
    [InlineData("نينوى", "Nineveh")]
    [InlineData("الموصل", "Nineveh")]
    public void Catalog_maps_aliases_to_same_canonical(string label, string canonicalId)
    {
        Assert.Equal(canonicalId, CrmProvinceCatalog.ResolveCanonicalId(label));
    }
}
