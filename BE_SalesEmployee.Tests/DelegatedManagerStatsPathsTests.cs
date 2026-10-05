using BE_SalesEmployee.DelegatedManager;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DelegatedManagerStatsPathsTests
{
    [Fact]
    public void Catalog_TargetsSalesManagerStatsNotBranchManager()
    {
        Assert.Equal("sales-manager/stats/catalog", DelegatedManagerStatsPaths.CompanyCatalog);
        Assert.DoesNotContain("branch-manager", DelegatedManagerStatsPaths.CompanyCatalog);
    }

    [Fact]
    public void BranchMonth_TargetsSalesManagerPath()
    {
        Assert.Equal("sales-manager/stats/branch-month", DelegatedManagerStatsPaths.CompanyBranchMonth);
    }

    [Fact]
    public void Trend_DefaultsScopeToBranchAndFormatsDates()
    {
        var path = DelegatedManagerStatsPaths.CompanyTrend(
            null,
            0,
            new DateTime(2026, 9, 1),
            new DateTime(2026, 9, 23));

        Assert.StartsWith("sales-manager/stats/trend?", path);
        Assert.Contains("scope=branch", path);
        Assert.Contains("id=0", path);
        Assert.Contains("fromDate=2026-09-01", path);
        Assert.Contains("toDate=2026-09-23", path);
    }

    [Fact]
    public void Trend_EscapesScopeAndKeepsEntityId()
    {
        var path = DelegatedManagerStatsPaths.CompanyTrend("list", 44, new DateTime(2026, 1, 5), new DateTime(2026, 1, 6));

        Assert.Contains("scope=list", path);
        Assert.Contains("id=44", path);
    }
}
