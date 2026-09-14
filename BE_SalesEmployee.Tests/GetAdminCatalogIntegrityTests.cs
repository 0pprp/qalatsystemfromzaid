using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Live GetAdmin snapshot (2026-09-14) — every sales cityValue must resolve uniquely.
/// Excludes قانونية/تجريبي/شهري the same way GetSalesBranchesAsync does.
/// </summary>
public sealed class GetAdminCatalogIntegrityTests
{
    private static List<AdminCity> LiveSalesCatalog() =>
    [
        Entry("1", "النجف", "DatabaseCompanyNajaf", "http://sharenewnajaf.alsaaeidy.com/api/"),
        Entry("2", "الرصافة", "DatabaseCompanyBaghdadRosafa", "http://sharenewrosafa.alsaaeidy.com/api/"),
        Entry("3", "الكرخ", "DatabaseCompanyBaghdadKarak", "http://sharenewrkarak.alsaaeidy.com/api/"),
        Entry("4", "كربلاء", "DatabaseCompanyKarbala", "http://sharenewkarbala.alsaaeidy.com/api/"),
        Entry("5", "الحلة", "DatabaseCompanyBabil", "http://sharenewrbabil.alsaaeidy.com/api/"),
        Entry("6", "الديوانية", "DatabaseCompanyDewania", "http://sharenewdewania.alsaaeidy.com/api/"),
        Entry("7", "الكوت", "DatabaseCompanyKot", "http://sharenewkot.alsaaeidy.com/api/"),
        Entry("8", "الناصرية", "DatabaseCompanyNasria", "http://sharenewnasria.alsaaeidy.com/api/"),
        Entry("9", "البصرة", "DatabaseCompanyBasra", "http://sharenewrbasra.alsaaeidy.com/api/"),
        Entry("10", "المثنى", "DatabaseCompanyMothana", "http://sharenewmothana.alsaaeidy.com/api/"),
        Entry("11", "ديالى", "DatabaseCompanyDeiala", "http://sharenewrdeiala.alsaaeidy.com/api/"),
        Entry("12", "الانوار", "DatabaseCompanyBasraAlanwar", "http://sharenewrbasranwar.alsaaeidy.com/api/"),
        Entry("13", "الموصل", "DatabaseCompanyMusol", "http://sharenewmusol.alsaaeidy.com/api/"),
        Entry("14", "كركوك", "DatabaseCompanyKarkok", "http://sharenewrkarkok.alsaaeidy.com/api/"),
        Entry("15", "الرصافة عقيل", "DatabaseCompanyRusafaAqeel", "http://shortnewrosafaaqeel.alsaaeidy.com/api/"),
        Entry("16", "ميسان عقيل", "DatabaseCompanyMaysan", "http://sharenewmaysanl.alsaaeidy.com/api/"),
        Entry("18", "الكرخ عقيل", "DatabaseCompanyKarakAqeel", "http://shortnewkarakaqeel.alsaaeidy.com/api/"),
    ];

    private static AdminCity Entry(string value, string name, string database, string link) =>
        new() { Value = value, Name = name, Database = database, Link = link };

    public static IEnumerable<object[]> EveryCityValue() =>
        LiveSalesCatalog().Select(c => new object[] { c.Value, c.Database, c.Link });

    [Theory]
    [MemberData(nameof(EveryCityValue))]
    public void Every_Configured_CityValue_Resolves_Exactly_One_Branch(string value, string database, string link)
    {
        var city = SalesBranchResolver.ResolveExact(LiveSalesCatalog(), value);
        Assert.NotNull(city);
        Assert.Equal(database, city!.Database);
        Assert.Equal(link, city.Link);
    }

    [Fact]
    public void Catalog_Values_Are_Unique()
    {
        var values = LiveSalesCatalog().Select(c => c.Value).ToList();
        Assert.Equal(values.Count, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Catalog_Links_Are_Unique()
    {
        var links = LiveSalesCatalog().Select(c => c.Link.TrimEnd('/')).ToList();
        Assert.Equal(links.Count, links.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void No_Sales_Branch_Reuses_Najaf_Link()
    {
        var najaf = LiveSalesCatalog().Single(c => c.Value == "1").Link;
        var others = LiveSalesCatalog().Where(c => c.Value != "1");
        Assert.All(others, c =>
            Assert.False(string.Equals(c.Link, najaf, StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("3", "DatabaseCompanyBaghdadKarak")]
    [InlineData("9", "DatabaseCompanyBasra")]
    [InlineData("1", "DatabaseCompanyNajaf")]
    public void Critical_Provinces_Do_Not_Resolve_To_Wrong_Database(string value, string expectedDb)
    {
        var city = SalesBranchResolver.ResolveExact(LiveSalesCatalog(), value);
        Assert.NotNull(city);
        Assert.Equal(expectedDb, city!.Database);
        if (value != "1")
        {
            Assert.DoesNotContain("najaf", city.Link, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual("DatabaseCompanyNajaf", city.Database);
        }
    }
}
