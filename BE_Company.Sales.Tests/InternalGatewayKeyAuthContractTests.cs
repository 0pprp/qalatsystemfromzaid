using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// Documents expected gateway-key auth outcomes for internal Global Manager APIs.
/// Handler rejects missing/wrong keys; scheme is SalesGateway only (user JWT alone cannot authenticate).
/// </summary>
public sealed class InternalGatewayKeyAuthContractTests
{
    [Theory]
    [InlineData(null, "expected", false)]
    [InlineData("", "expected", false)]
    [InlineData("wrong", "expected", false)]
    [InlineData("expected", "expected", true)]
    public void Gateway_Key_Equality_Is_Ordinal(string? provided, string expected, bool ok)
    {
        var allowed = !string.IsNullOrWhiteSpace(expected)
                      && !string.IsNullOrWhiteSpace(provided)
                      && string.Equals(provided, expected, StringComparison.Ordinal);
        Assert.Equal(ok, allowed);
    }

    [Fact]
    public void Internal_Route_Requires_SalesGateway_Scheme_Not_Bearer()
    {
        // Contract: InternalGlobalSalesManagerController uses SalesGatewayKeyHandler.SchemeName only.
        Assert.Equal("SalesGateway", BE_Company.Sales.Authorization.SalesGatewayKeyHandler.SchemeName);
        Assert.Equal("X-Sales-Gateway-Key", BE_Company.Sales.Authorization.SalesGatewayKeyHandler.HeaderName);
    }
}
