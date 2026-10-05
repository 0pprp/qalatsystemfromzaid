using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerRating;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class FollowerListStatsRulesExtendedTests
{
    [Fact]
    public void ActiveCustomer_RequiresStrictlyAbove38Percent()
    {
        Assert.False(FollowerActiveCustomerRules.IsActive(3800, 100000)); // exactly 3.8%
        Assert.True(FollowerActiveCustomerRules.IsActive(3801, 100000));
        Assert.False(FollowerActiveCustomerRules.IsActive(0, 100000));
        Assert.False(FollowerActiveCustomerRules.IsActive(5000, 0));
    }

    [Fact]
    public void RatingCalculator_IsLegal_Does_Not_Override()
    {
        var asOf = new DateTime(2026, 3, 1);
        var r = FollowerCustomerRatingCalculator.Evaluate(
            new FollowerCustomerRatingFacts(1, 5, "أ", null, true, asOf.AddDays(-99),
                asOf, 100000, 100000, 0, 10000),
            asOf);
        Assert.Equal(FollowerCustomerRatingLevel.Excellent, r.Level);
        Assert.Equal(10, r.Score);
        Assert.Equal("ممتاز", r.RatingLabel);
    }

    [Fact]
    public void ListAggregator_AveragesScores()
    {
        var facts = new[]
        {
            new FollowerCustomerRatingResult(1, 1, "a", null, FollowerCustomerRatingLevel.Excellent, "ممتاز", 10, null, 0, 0, 0),
            new FollowerCustomerRatingResult(2, 1, "b", null, FollowerCustomerRatingLevel.Rejected, "متلكئ", -5, null, 0, 0, 0),
        };
        var agg = FollowerListRatingAggregator.Aggregate(facts);
        Assert.Equal(2, agg.CustomerCount);
        Assert.Equal(1, agg.ExcellentCount);
        Assert.Equal(1, agg.RejectedCount);
        Assert.Equal(0, agg.BadCount);
        Assert.Equal(2.5, agg.AverageScore);
        Assert.Equal("ضعيف", agg.FinalRatingLabel);
    }

    [Fact]
    public void RatingCalculator_Settled_200_Is_Bad()
    {
        var asOf = new DateTime(2026, 3, 1);
        var r = FollowerCustomerRatingCalculator.Evaluate(
            new FollowerCustomerRatingFacts(1, 5, "أ", null, false, asOf.AddDays(-199),
                asOf, 100000, 100000, 0, 10000),
            asOf);
        Assert.Equal(FollowerCustomerRatingLevel.Bad, r.Level);
        Assert.Equal(-10, r.Score);
        Assert.Equal("سيء", r.RatingLabel);
    }

    [Fact]
    public void RatingCalculator_Settled_180_Is_Mutalaki()
    {
        var asOf = new DateTime(2026, 3, 1);
        var r = FollowerCustomerRatingCalculator.Evaluate(
            new FollowerCustomerRatingFacts(1, 5, "أ", null, false, asOf.AddDays(-179),
                asOf, 100000, 100000, 0, 10000),
            asOf);
        Assert.Equal(FollowerCustomerRatingLevel.Rejected, r.Level);
        Assert.Equal(-5, r.Score);
        Assert.Equal("متلكئ", r.RatingLabel);
    }

    [Fact]
    public void MonthDiff_IsNewMinusZeroed()
    {
        Assert.Equal(3, 5 - 2);
        Assert.Equal(1000m, FollowerListStatsRules.Delta(5000m, 4000m));
    }
}
