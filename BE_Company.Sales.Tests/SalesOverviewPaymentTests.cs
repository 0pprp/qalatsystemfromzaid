using BE_Company.Sales.DTO;
using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class SalesOverviewPaymentTests
{
    [Fact]
    public void Apply_UsesAppPaymentRatingNotStaffScore()
    {
        var asOf = new DateTime(2026, 11, 1);
        var facts = new Dictionary<int, CustomerRatingFacts>
        {
            [1] = new(1, null, false, false, asOf.AddDays(-30), asOf, 1000, 1000, 0),
            [2] = new(2, null, false, false, asOf.AddDays(-250), null, 1000, 0, 1000),
            [3] = new(3, null, false, false, null, null, 0, 0, 0)
        };
        var dto = new SalesManagerDashboardDTO();

        SalesOverviewPayment.Apply(dto, [
            new SalesOverviewPayment.SaleLine(1, "أحمد", 100),
            new SalesOverviewPayment.SaleLine(1, "أحمد", 40),
            new SalesOverviewPayment.SaleLine(2, "أحمد", 50),
            new SalesOverviewPayment.SaleLine(3, "سامي", 20)
        ], facts, asOf);

        Assert.Equal(2, dto.ExcellentSales);
        Assert.Equal(1, dto.BadSales);
        Assert.Equal(3, dto.ScoredSalesCount);
        Assert.Equal(1, dto.UnscoredSalesCount);
        Assert.Equal(3.3m, dto.AverageSaleScore);
        Assert.Equal("ممتاز", dto.SaleScores[0].Label);
        Assert.Equal(10, dto.SaleScores[0].Score);
        Assert.Equal(2, dto.SaleScores[0].Count);
        Assert.Equal("سيء", dto.SaleScores[1].Label);

        var seller = dto.Sellers.Single(x => x.Name == "أحمد");
        Assert.Equal(3, seller.SalesCount);
        Assert.Equal(2, seller.ExcellentSales);
        Assert.Equal(1, seller.BadSales);
        Assert.Equal(3, seller.ScoredSales);
        Assert.Equal(3.3m, seller.AverageScore);
        Assert.Equal(0, seller.HighScores);
    }
}
