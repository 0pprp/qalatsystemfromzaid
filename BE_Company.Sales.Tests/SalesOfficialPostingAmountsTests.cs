using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesOfficialPostingAmountsTests
{
    [Fact]
    public void ToStoredDiscounts_applies_override_as_dinar_discount_over_1448()
    {
        // catalog 2,735,000 → override 2,900,000 means negative discount (price increase)
        var (total, day) = SalesOfficialPostingAmounts.ToStoredDiscounts(
            catalogTotalDenar: 2_735_000m,
            catalogDailyDenar: 21_000m,
            finalTotalDenar: 2_900_000m,
            finalDailyDenar: 20_000m);

        Assert.Equal((2_735_000d - 2_900_000d) / 1448d, total, 6);
        Assert.Equal((21_000d - 20_000d) / 1448d, day, 6);
    }

    [Fact]
    public void ToStoredDiscounts_lower_override_reduces_posted_sale_amount()
    {
        var (total, _) = SalesOfficialPostingAmounts.ToStoredDiscounts(
            2_735_000m, 21_000m, 2_500_000m, 21_000m);

        // AmountTotalSalesDenar = catalog - discount*1448 = 2,500,000
        var posted = 2_735_000d - total * 1448d;
        Assert.Equal(2_500_000d, posted, 3);
    }

    [Fact]
    public void ResolveShopName_prefers_shop_profile_never_mukhtar()
    {
        Assert.Equal("معرض الوطنية", SalesOfficialPostingAmounts.ResolveShopName("معرض الوطنية", "حسن المختار"));
        Assert.Null(SalesOfficialPostingAmounts.ResolveShopName(null, "حسن المختار"));
        Assert.Null(SalesOfficialPostingAmounts.ResolveShopName("  ", "حسن"));
    }
}
