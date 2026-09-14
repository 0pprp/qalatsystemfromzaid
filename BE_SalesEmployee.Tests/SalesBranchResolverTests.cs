using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SalesBranchResolverTests
{
    private static List<AdminCity> Catalog() =>
    [
        new()
        {
            Value = "1",
            Name = "النجف",
            Database = "DatabaseCompanyNajaf",
            Link = "http://sharenewnajaf.alsaaeidy.com/api/"
        },
        new()
        {
            Value = "3",
            Name = "الكرخ",
            Database = "DatabaseCompanyBaghdadKarak",
            Link = "http://sharenewrkarak.alsaaeidy.com/api/"
        },
        new()
        {
            Value = "9",
            Name = "البصرة",
            Database = "DatabaseCompanyBasra",
            Link = "http://sharenewrbasra.alsaaeidy.com/api/"
        },
    ];

    [Fact]
    public void Selected_Najaf_Value_Resolves_Only_Najaf_Link()
    {
        var targets = SalesBranchResolver.ResolveTargets(Catalog(), "1");
        Assert.Single(targets);
        Assert.Equal("DatabaseCompanyNajaf", targets[0].Database);
        Assert.Contains("najaf", targets[0].Link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Selected_Karkh_Value_Resolves_Only_Karkh_Not_Najaf()
    {
        var targets = SalesBranchResolver.ResolveTargets(Catalog(), "3");
        Assert.Single(targets);
        Assert.Equal("DatabaseCompanyBaghdadKarak", targets[0].Database);
        Assert.DoesNotContain("najaf", targets[0].Link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Selected_Basra_Database_Name_Resolves_Basra()
    {
        var targets = SalesBranchResolver.ResolveTargets(Catalog(), "DatabaseCompanyBasra");
        Assert.Single(targets);
        Assert.Equal("9", targets[0].Value);
        Assert.DoesNotContain("najaf", targets[0].Link, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unknown_City_Does_Not_Fallback_To_Najaf()
    {
        var targets = SalesBranchResolver.ResolveTargets(Catalog(), "999");
        Assert.Empty(targets);
        Assert.Null(SalesBranchResolver.ResolveExact(Catalog(), "unknown-city"));
    }

    [Fact]
    public void Empty_City_Returns_All_Branches_For_Fanout()
    {
        var targets = SalesBranchResolver.ResolveTargets(Catalog(), null);
        Assert.Equal(3, targets.Count);
    }

    [Fact]
    public void Arabic_Name_Matches_Branch()
    {
        var city = SalesBranchResolver.ResolveExact(Catalog(), "الكرخ");
        Assert.NotNull(city);
        Assert.Equal("3", city!.Value);
    }

    [Fact]
    public void Ambiguous_Catalog_Match_Is_Rejected()
    {
        var dupes = Catalog();
        dupes.Add(new AdminCity
        {
            Value = "3-dup",
            Name = "الكرخ",
            Database = "DatabaseCompanyBaghdadKarakDup",
            Link = "http://evil.example/api/"
        });
        Assert.Null(SalesBranchResolver.ResolveExact(dupes, "الكرخ"));
    }
}
