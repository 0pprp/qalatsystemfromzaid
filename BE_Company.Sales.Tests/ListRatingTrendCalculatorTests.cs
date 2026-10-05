using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class ListRatingTrendCalculatorTests
{
    private static CustomerRatingFacts Fact(int id, DateTime? sale, double total = 100, double remaining = 50) =>
        new(id, 1, false, false, sale, sale?.AddDays(1), total, total - remaining, remaining, 1);

    /// <summary>Settled sale with known level via settle-day bands (Excellent≤140, Good≤160, Weak&lt;180, متلكئ 180–199, سيء≥200).</summary>
    private static CustomerRatingFacts Settled(
        int id,
        DateTime sale,
        int settleDays,
        int? delegateId = 1,
        bool legal = false) =>
        new(
            id,
            delegateId,
            legal,
            false,
            sale,
            sale.AddDays(settleDays - 1),
            1000,
            1000,
            0,
            1);

    [Fact]
    public void DailyListScores_Only_Includes_Sales_Inside_Period()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 3);
        var facts = new[]
        {
            Fact(1, new DateTime(2026, 8, 20)), // before period — excluded
            Fact(2, new DateTime(2026, 9, 1)),  // in period
            Fact(3, new DateTime(2026, 9, 5)),  // after period — excluded
        };

        var series = ListRatingTrendCalculator.DailyListScores(facts, from, to);

        Assert.Equal(3, series.Count);
        Assert.Contains(from, series.Keys);

        var day1Facts = ListRatingTrendCalculator.FactsSoldInPeriod(facts, from, to, from).ToList();
        Assert.Single(day1Facts);
        Assert.Equal(2, day1Facts[0].CustomerId);

        var day3Facts = ListRatingTrendCalculator.FactsSoldInPeriod(facts, from, to, to).ToList();
        Assert.Single(day3Facts);
        Assert.Equal(2, day3Facts[0].CustomerId);
    }

    [Fact]
    public void FactsSoldInPeriod_Excludes_Missing_Sale_Date()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 10);
        var facts = new[]
        {
            Fact(1, null),
            Fact(2, new DateTime(2026, 9, 5)),
        };
        var hit = ListRatingTrendCalculator.FactsSoldInPeriod(facts, from, to, to).ToList();
        Assert.Single(hit);
        Assert.Equal(2, hit[0].CustomerId);
    }

    [Fact]
    public void PeriodAggregate_Uses_Level_Points_Over_Customer_Count()
    {
        // 5 ممتاز(10) + 3 جيد(5) + 1 ضعيف(0) + 1 متلكئ(-5) = 60 → متوسط 6
        var sale = new DateTime(2026, 9, 5);
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);
        var facts = new List<CustomerRatingFacts>();
        for (var i = 1; i <= 5; i++)
            facts.Add(Settled(i, sale, 100)); // Excellent
        for (var i = 6; i <= 8; i++)
            facts.Add(Settled(i, sale, 150)); // Good
        facts.Add(Settled(9, sale, 170)); // Weak
        facts.Add(Settled(10, sale, 190)); // متلكئ (180–199)
        // Sale before period must not affect score
        facts.Add(Settled(99, new DateTime(2026, 8, 31), 100));

        var agg = ListRatingTrendCalculator.PeriodAggregate(facts, from, to, to);

        Assert.Equal(10, agg.CustomerCount);
        Assert.Equal(5, agg.ExcellentCount);
        Assert.Equal(3, agg.GoodCount);
        Assert.Equal(1, agg.WeakCount);
        Assert.Equal(1, agg.RejectedCount);
        Assert.Equal(0, agg.BadCount);
        Assert.Equal(0, agg.LegalCount);
        Assert.Equal(60, agg.TotalPoints);
        Assert.Equal(6.0, agg.AverageScore, 2);
    }

    [Fact]
    public void PeriodAggregate_Bad_Band_At_200_Days()
    {
        var sale = new DateTime(2026, 9, 5);
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);
        var facts = new[] { Settled(1, sale, 200) };
        var agg = ListRatingTrendCalculator.PeriodAggregate(facts, from, to, to);
        Assert.Equal(1, agg.BadCount);
        Assert.Equal(0, agg.RejectedCount);
        Assert.Equal(-10, agg.TotalPoints);
        Assert.Equal(CustomerRatingLabels.Bad, agg.FinalRatingLabel);
    }

    [Fact]
    public void DailyListScores_Returns_One_Point_Per_Calendar_Day()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 30);
        var facts = new[]
        {
            Settled(1, new DateTime(2026, 9, 1), 100),
            Settled(2, new DateTime(2026, 9, 15), 100),
        };

        var series = ListRatingTrendCalculator.DailyListScores(facts, from, to);

        Assert.Equal(30, series.Count);
        Assert.Equal(from, series.Keys.Min());
        Assert.Equal(to, series.Keys.Max());
        // Sep 1 only first customer; Sep 15+ both → score stays 10 (both excellent)
        Assert.Equal(10.0, series[from], 2);
        Assert.Equal(10.0, series[to], 2);
    }

    [Fact]
    public void DailyAverageAcrossLists_Pools_Customers_Not_Mean_Of_List_Averages()
    {
        var from = new DateTime(2026, 9, 1);
        var to = new DateTime(2026, 9, 10);
        var sale = new DateTime(2026, 9, 2);
        // List A: 9 excellent; List B: 1 متلكئ → pooled avg = (90-5)/10 = 8.5
        // Mean of list averages would be (10 + -5)/2 = 2.5
        var byList = new Dictionary<int, IReadOnlyList<CustomerRatingFacts>>
        {
            [1] = Enumerable.Range(1, 9).Select(i => Settled(i, sale, 100, 1)).ToList(),
            [2] = [Settled(100, sale, 190, 2)],
        };

        var series = ListRatingTrendCalculator.DailyAverageAcrossLists([1, 2], byList, from, to);

        Assert.Equal(10, series.Count);
        Assert.Equal(8.5, series[to], 2);
    }

    [Fact]
    public void DailyListScores_Extends_Past_Sale_Window_With_Fixed_Cohort()
    {
        // August sales only; chart continues into September — Sept sale must stay out.
        var saleFrom = new DateTime(2026, 8, 1);
        var saleTo = new DateTime(2026, 8, 31);
        var asOfEnd = new DateTime(2026, 9, 15);
        var facts = new[]
        {
            Settled(1, new DateTime(2026, 8, 10), 100), // excellent
            Settled(2, new DateTime(2026, 9, 5), 190),  // after sale window — excluded
        };

        var series = ListRatingTrendCalculator.DailyListScores(facts, saleFrom, saleTo, asOfEnd);

        Assert.Equal((asOfEnd - saleFrom).Days + 1, series.Count);
        Assert.Equal(saleFrom, series.Keys.Min());
        Assert.Equal(asOfEnd, series.Keys.Max());

        var endAgg = ListRatingTrendCalculator.PeriodAggregate(facts, saleFrom, saleTo, asOfEnd);
        Assert.Equal(1, endAgg.CustomerCount);
        Assert.Equal(10.0, endAgg.AverageScore, 2);
        Assert.Equal(10.0, series[asOfEnd], 2);
    }

    [Fact]
    public void PeriodAggregate_AsOf_After_Sale_Window_Still_Uses_Sale_Cohort()
    {
        var saleFrom = new DateTime(2026, 8, 1);
        var saleTo = new DateTime(2026, 8, 31);
        var today = new DateTime(2026, 9, 28);
        var facts = new[]
        {
            Settled(1, new DateTime(2026, 8, 15), 100),
            Settled(2, new DateTime(2026, 7, 20), 100), // before window
            Settled(3, new DateTime(2026, 9, 1), 100),  // after window
        };

        var agg = ListRatingTrendCalculator.PeriodAggregate(facts, saleFrom, saleTo, today);

        Assert.Equal(1, agg.CustomerCount);
        Assert.Equal(10.0, agg.AverageScore, 2);
    }
}
