using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesRequestPriceCeilingTests
{
    [Fact]
    public void Validate_NullOrZeroCeiling_AllowsAnyPrice()
    {
        Assert.Null(SalesRequestPriceCeiling.Validate(1_000_000, null));
        Assert.Null(SalesRequestPriceCeiling.Validate(1_000_000, 0));
        Assert.Null(SalesRequestPriceCeiling.Validate(1_000_000, -5));
    }

    [Fact]
    public void Validate_AtOrBelowCeiling_Ok()
    {
        Assert.Null(SalesRequestPriceCeiling.Validate(500_000, 500_000));
        Assert.Null(SalesRequestPriceCeiling.Validate(499_999, 500_000));
    }

    [Fact]
    public void Validate_AboveCeiling_ReturnsArabicMessage()
    {
        var err = SalesRequestPriceCeiling.Validate(500_001, 500_000);
        Assert.NotNull(err);
        Assert.Contains("500", err!);
        Assert.Contains("الحد الأقصى", err);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_NonPositive_ReturnsNull(int? raw)
    {
        Assert.Null(SalesRequestPriceCeiling.Normalize(raw));
    }

    [Fact]
    public void Normalize_RoundsAwayFromZero()
    {
        Assert.Equal(10m, SalesRequestPriceCeiling.Normalize(10.4m));
        Assert.Equal(11m, SalesRequestPriceCeiling.Normalize(10.5m));
    }
}
