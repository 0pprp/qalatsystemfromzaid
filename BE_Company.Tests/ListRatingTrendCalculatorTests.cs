using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Tests;

public sealed class ListRatingTrendCalculatorTests
{
    [Fact]
    public void FollowerAverage_Pools_Customers_Not_Mean_Of_List_Averages()
    {
        var day = new DateTime(2026, 9, 1);
        // List A: 9 excellent; List B: 1 متلكئ → pooled (90-5)/10 = 8.5
        // Mean of list averages would be (10 + -5)/2 = 2.5
        var byList = new Dictionary<int, IReadOnlyList<CustomerRatingFacts>>
        {
            [10] = Enumerable.Range(1, 9).Select(i => Fact(i, 10, settled: true, daysToSettle: 100, sale: day)).ToList(),
            [11] = [Fact(100, 11, settled: true, daysToSettle: 190, sale: day)],
        };

        var series = ListRatingTrendCalculator.DailyAverageAcrossLists([10, 11], byList, day, day);
        Assert.Equal(8.5, series[day], 2);
    }

    [Fact]
    public void EmptyFollowedLists_YieldZero()
    {
        var day = new DateTime(2026, 9, 1);
        var series = ListRatingTrendCalculator.DailyAverageAcrossLists(
            [],
            new Dictionary<int, IReadOnlyList<CustomerRatingFacts>>(),
            day,
            day);
        Assert.Equal(0.0, series[day]);
    }

    [Fact]
    public void SingleList_UsesListAverageScore()
    {
        var day = new DateTime(2026, 9, 1);
        var facts = new[] { Fact(1, 5, settled: true, daysToSettle: 100, sale: day) };
        var series = ListRatingTrendCalculator.DailyListScores(facts, day, day);
        Assert.Equal(10.0, series[day]);
    }

    [Fact]
    public void Points_AreChronologicalLeftToRight()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 3);
        var facts = new[] { Fact(1, 5, settled: true, daysToSettle: 100, sale: from) };
        var series = ListRatingTrendCalculator.DailyListScores(facts, from, to);
        var keys = series.Keys.OrderBy(d => d).ToList();
        Assert.Equal(new[] { from, from.AddDays(1), to }, keys);
    }

    [Fact]
    public void Chart_Extends_Past_Sale_Window_Through_AsOf()
    {
        var saleFrom = new DateTime(2026, 8, 1);
        var saleTo = new DateTime(2026, 8, 31);
        var asOf = new DateTime(2026, 9, 10);
        var facts = new[] { Fact(1, 5, settled: true, daysToSettle: 100, sale: new DateTime(2026, 8, 15)) };
        var series = ListRatingTrendCalculator.DailyListScores(facts, saleFrom, saleTo, asOf);
        Assert.Equal(asOf, series.Keys.Max());
        Assert.Equal(10.0, series[asOf], 2);
    }

    private static CustomerRatingFacts Fact(
        int customerId,
        int? delegateId,
        bool settled = false,
        int daysToSettle = 150,
        bool isLegal = false,
        DateTime? sale = null)
    {
        var saleDate = (sale ?? new DateTime(2026, 9, 1)).Date;
        DateTime? lastPay = settled ? saleDate.AddDays(daysToSettle - 1) : null;
        double total = 1_000_000;
        double paid = settled ? total : total * 0.3;
        return new CustomerRatingFacts(
            customerId,
            delegateId,
            isLegal,
            IsFakeSale: false,
            DateSaleDevice: saleDate,
            LastPaymentDate: lastPay,
            AmountTotalSales: total,
            ReceiptsTotal: paid,
            AmountRemaining: total - paid,
            ReceiptCount: settled ? 5 : 2);
    }
}
