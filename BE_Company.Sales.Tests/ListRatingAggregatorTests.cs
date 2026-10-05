using BE_Company.Sales.Rating;
using Xunit;

namespace BE_Company.Sales.Tests;

public class ListRatingAggregatorTests
{
    private static readonly DateTime Cutoff = ListRatingAggregator.RecentCutoffDate;

    [Fact]
    public void Aggregates_Counts_Points_And_Average()
    {
        var items = new[]
        {
            Rated(1, CustomerRatingLevel.Excellent, 10, Cutoff.AddDays(-10)),
            Rated(2, CustomerRatingLevel.Good, 5, Cutoff.AddDays(-10)),
            Rated(3, CustomerRatingLevel.Weak, 0, Cutoff.AddDays(-10)),
            Rated(4, CustomerRatingLevel.Rejected, -5, Cutoff.AddDays(-10)),
            Rated(5, CustomerRatingLevel.Bad, -10, Cutoff.AddDays(-10)),
        };

        var result = ListRatingAggregator.Aggregate(items);
        Assert.Equal(5, result.CustomerCount);
        Assert.Equal(1, result.ExcellentCount);
        Assert.Equal(1, result.GoodCount);
        Assert.Equal(1, result.WeakCount);
        Assert.Equal(1, result.RejectedCount);
        Assert.Equal(0, result.LegalCount);
        Assert.Equal(1, result.BadCount);
        Assert.Equal(0, result.TotalPoints); // 10+5+0-5-10
        Assert.Equal(0, result.AverageScore);
        Assert.Equal(CustomerRatingLabels.Weak, result.FinalRatingLabel);
        Assert.Equal(0, result.LegalPercentage);
        Assert.Equal(ListRiskIndicator.High, result.RiskIndicator); // bad 20%
    }

    [Fact]
    public void Empty_List_No_Division_By_Zero()
    {
        var result = ListRatingAggregator.Aggregate(Array.Empty<CustomerRatingResult>());
        Assert.Equal(0, result.CustomerCount);
        Assert.Equal(0, result.AverageScore);
        Assert.Equal(0, result.BadCount);
        Assert.Equal(0, result.LegalCount);
        Assert.Equal(CustomerRatingLabels.Weak, result.FinalRatingLabel);
        Assert.Equal(ListRiskIndicator.None, result.RiskIndicator);
    }

    [Fact]
    public void Recent_Includes_Exact_Cutoff_Date()
    {
        var items = new[]
        {
            Rated(1, CustomerRatingLevel.Excellent, 10, Cutoff),
            Rated(2, CustomerRatingLevel.Good, 5, Cutoff.AddDays(-1)),
            Rated(3, CustomerRatingLevel.Weak, 0, null),
        };

        var recent = ListRatingAggregator.AggregateRecent(items);
        Assert.Equal(1, recent.CustomerCount);
        Assert.Equal(10, recent.AverageScore);
        Assert.Equal(CustomerRatingLabels.Excellent, recent.FinalRatingLabel);
    }

    [Fact]
    public void FinalRating_Boundaries()
    {
        Assert.Equal(CustomerRatingLabels.Excellent, ListRatingAggregator.ClassifyAverage(7).Label);
        Assert.Equal(CustomerRatingLabels.Good, ListRatingAggregator.ClassifyAverage(3).Label);
        Assert.Equal(CustomerRatingLabels.Weak, ListRatingAggregator.ClassifyAverage(0).Label);
        Assert.Equal(CustomerRatingLabels.Rejected, ListRatingAggregator.ClassifyAverage(-0.1).Label);
        Assert.Equal(CustomerRatingLabels.Rejected, ListRatingAggregator.ClassifyAverage(-7.4).Label);
        Assert.Equal(CustomerRatingLabels.Bad, ListRatingAggregator.ClassifyAverage(-7.5).Label);
        Assert.Equal(CustomerRatingLabels.Bad, ListRatingAggregator.ClassifyAverage(-10).Label);
    }

    [Theory]
    [InlineData(0, ListRiskIndicator.None)]
    [InlineData(0.1, ListRiskIndicator.Low)]
    [InlineData(4.9, ListRiskIndicator.Low)]
    [InlineData(5, ListRiskIndicator.Medium)]
    [InlineData(14.9, ListRiskIndicator.Medium)]
    [InlineData(15, ListRiskIndicator.High)]
    public void RiskIndicator_From_BadPercentage(double pct, ListRiskIndicator expected)
    {
        Assert.Equal(expected, ListRatingAggregator.ClassifyRisk(pct));
    }

    [Fact]
    public void Aggregate_OneHundredThousand_Completes_Under_Budget()
    {
        var items = new CustomerRatingResult[100_000];
        for (var i = 0; i < items.Length; i++)
        {
            var level = (i % 5) switch
            {
                0 => CustomerRatingLevel.Excellent,
                1 => CustomerRatingLevel.Good,
                2 => CustomerRatingLevel.Weak,
                3 => CustomerRatingLevel.Rejected,
                _ => CustomerRatingLevel.Bad
            };
            var score = level switch
            {
                CustomerRatingLevel.Excellent => 10,
                CustomerRatingLevel.Good => 5,
                CustomerRatingLevel.Weak => 0,
                CustomerRatingLevel.Rejected => -5,
                _ => -10
            };
            items[i] = new CustomerRatingResult(
                i, 1, level, CustomerRatingLabels.FromLevel(level), score, "t",
                false, false, null, null, null, Cutoff);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = ListRatingAggregator.Aggregate(items);
        sw.Stop();

        Assert.Equal(100_000, result.CustomerCount);
        Assert.Equal(0, result.LegalCount);
        Assert.True(result.ExcellentCount > 0);
        Assert.True(result.RejectedCount > 0);
        Assert.True(result.BadCount > 0);
        Assert.True(sw.ElapsedMilliseconds < 2_000, $"Aggregate 100k took {sw.ElapsedMilliseconds}ms");
    }

    private static CustomerRatingResult Rated(int id, CustomerRatingLevel level, int score, DateTime? saleDate) =>
        new(id, 1, level, CustomerRatingLabels.FromLevel(level), score, "t",
            false, false, null, null, null, saleDate);
}
