using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>Company-relative paths the director/monitor legal-names read proxies must hit.</summary>
public sealed class DirectorLegalNamesProxyPathsTests
{
    [Fact]
    public void Weak_week_payers_path()
    {
        Assert.Equal(
            "CustomerDecisions/WeakWeekPayers",
            DirectorLegalNamesPaths.WeakWeekPayers());
    }

    [Fact]
    public void Legal_conversion_requests_path_includes_filters()
    {
        var path = DirectorLegalNamesPaths.LegalConversionRequests("Pending", 200, 0);
        Assert.StartsWith("CustomerDecisions/LegalConversionRequests?", path);
        Assert.Contains("status=Pending", path);
        Assert.Contains("take=200", path);
        Assert.Contains("skip=0", path);
    }

    [Fact]
    public void Waiver_accountant_requests_path()
    {
        var path = DirectorLegalNamesPaths.WaiverAccountantRequests("", 100, 0);
        Assert.StartsWith("CustomerDecisions/WaiverAccountantRequests?", path);
        Assert.DoesNotContain("status=", path);
    }

    [Fact]
    public void Decisions_path_with_filters()
    {
        var path = DirectorLegalNamesPaths.Decisions("قانونية", null, null, 12);
        Assert.StartsWith("CustomerDecisions/Decisions?", path);
        Assert.Contains("decisionType=", path);
        Assert.Contains("customerID=12", path);
    }
}
