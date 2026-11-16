using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class SalesOverviewMathTests
{
    [Fact]
    public void Growth_UsesPreviousMonthAsBase()
    {
        Assert.Equal(0m, SalesOverviewMath.GrowthPercent(0, 0));
        Assert.Null(SalesOverviewMath.GrowthPercent(10, 0));
        Assert.Equal(25m, SalesOverviewMath.GrowthPercent(125, 100));
        Assert.Equal(-50m, SalesOverviewMath.GrowthPercent(50, 100));
    }

    [Fact]
    public void AverageScore_UsesScoredSalesOnly()
    {
        Assert.Null(SalesOverviewMath.AverageScore(0, 0));
        Assert.Equal(6.5m, SalesOverviewMath.AverageScore(26, 4));
    }

    [Fact]
    public void Categories_GroupPhonesBikesAndOther()
    {
        Assert.Equal("هواتف", SalesOverviewMath.CategorizeItem("ايفون 13"));
        Assert.Equal("هواتف", SalesOverviewMath.CategorizeItem("سامسونج A54"));
        Assert.Equal("دراجات", SalesOverviewMath.CategorizeItem("دراجة كهربائية"));
        Assert.Equal("دراجات", SalesOverviewMath.CategorizeItem("سكوتر"));
        Assert.Equal("حواسيب", SalesOverviewMath.CategorizeItem("لابتوب"));
        Assert.Equal("شاشات", SalesOverviewMath.CategorizeItem("شاشة 55"));
        Assert.Equal("أخرى", SalesOverviewMath.CategorizeItem("مكيف"));
        Assert.Equal("أخرى", SalesOverviewMath.CategorizeItem("  "));
    }

    [Fact]
    public void CategoriesFrom_SumsQuantityAndPrice()
    {
        var rows = SalesOverviewMath.CategoriesFrom(
        [
            new SalesOverviewItemDTO { ItemName = "ايفون", Category = "هواتف", Quantity = 2, SalesCount = 2, SalePrice = 100 },
            new SalesOverviewItemDTO { ItemName = "سامسونج", Category = "هواتف", Quantity = 1, SalesCount = 1, SalePrice = 40 },
            new SalesOverviewItemDTO { ItemName = "دراجة", Category = "دراجات", Quantity = 3, SalesCount = 3, SalePrice = 90 }
        ]);

        var phones = Assert.Single(rows, r => r.Category == "هواتف");
        Assert.Equal(3, phones.Quantity);
        Assert.Equal(140m, phones.SalePrice);
        Assert.Equal("هواتف", rows[0].Category);
    }

    [Fact]
    public void CollectedPercent_CapsAtTheSalePrice()
    {
        Assert.Equal(0m, SalesOverviewMath.CollectedPercent(0, 0));
        Assert.Equal(0m, SalesOverviewMath.CollectedPercent(10, 0));
        Assert.Equal(25m, SalesOverviewMath.CollectedPercent(250, 1000));
        Assert.Equal(100m, SalesOverviewMath.CollectedPercent(1500, 1000));
    }

    [Fact]
    public void Conversion_IsZeroWhenNoOrders()
    {
        Assert.Equal(0m, SalesOverviewMath.ConversionPercent(0, 0));
        Assert.Equal(50m, SalesOverviewMath.ConversionPercent(1, 2));
    }
}
