using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class DirectorSalesFollowupRulesTests
{
    private sealed record Row(int Score, DateTime Date, string Note);

    [Fact]
    public void SplitNewestFirst_Puts_7Plus_In_High_And_6Minus_In_Low()
    {
        var rows = new[]
        {
            new Row(9, new DateTime(2026, 10, 20), "a"),
            new Row(6, new DateTime(2026, 10, 19), "b"),
            new Row(7, new DateTime(2026, 10, 18), "c"),
            new Row(3, new DateTime(2026, 10, 17), "d"),
            new Row(10, new DateTime(2026, 10, 16), "e"),
            new Row(0, new DateTime(2026, 10, 15), "f"),
        };

        var (high, low) = DirectorSalesFollowupRules.SplitNewestFirst(
            rows, r => r.Score, r => r.Date, limit: 30);

        Assert.Equal(new[] { 9, 7, 10 }, high.Select(r => r.Score).ToArray());
        Assert.Equal(new[] { 6, 3, 0 }, low.Select(r => r.Score).ToArray());
    }

    [Fact]
    public void SplitNewestFirst_Respects_Limit_Per_Bucket()
    {
        var rows = Enumerable.Range(0, 40)
            .Select(i => new Row(i % 2 == 0 ? 8 : 4, new DateTime(2026, 1, 1).AddDays(i), $"{i}"))
            .ToList();

        var (high, low) = DirectorSalesFollowupRules.SplitNewestFirst(
            rows, r => r.Score, r => r.Date, limit: 5);

        Assert.Equal(5, high.Count);
        Assert.Equal(5, low.Count);
        Assert.All(high, r => Assert.True(r.Score >= 7));
        Assert.All(low, r => Assert.True(r.Score <= 6));
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(10, true)]
    [InlineData(6, false)]
    [InlineData(0, false)]
    public void IsHigh_Threshold(int score, bool high)
    {
        Assert.Equal(high, DirectorSalesFollowupRules.IsHigh(score));
        Assert.Equal(!high, DirectorSalesFollowupRules.IsLow(score));
    }

    [Fact]
    public void AnonymizeLowOnly_Drops_High_And_Customer_Identity()
    {
        var rows = new[]
        {
            new DirectorSalesFollowupRatingRowDto
            {
                Score = 9, Note = "ok", RatingDate = new DateTime(2026, 10, 20),
                CustomerName = "Ali", ListName = "ListA", Kind = "auth",
            },
            new DirectorSalesFollowupRatingRowDto
            {
                Score = 4, Note = "weak", RatingDate = new DateTime(2026, 10, 19),
                CustomerName = "Omar", ListName = "ListB", Kind = "shop",
            },
            new DirectorSalesFollowupRatingRowDto
            {
                Score = 2, Note = "bad", RatingDate = new DateTime(2026, 10, 18),
                CustomerName = "Sara", Kind = "collector",
            },
        };

        var low = DirectorSalesFollowupRules.AnonymizeLowOnly(rows);

        Assert.Equal(2, low.Count);
        Assert.Equal(new[] { 4, 2 }, low.Select(r => r.Score).ToArray());
        Assert.All(low, r =>
        {
            Assert.Null(r.CustomerName);
            Assert.Null(r.ListName);
            Assert.Null(r.DelegateName);
            Assert.False(string.IsNullOrEmpty(r.Note));
        });
    }

    [Theory]
    [InlineData(false, 1000, 1000, 0, true)]   // paid covers sale, remaining 0
    [InlineData(false, 1000, 1200, 0, true)]   // overpaid still zeroed
    [InlineData(false, 1000, 0, 1000, false)]  // open balance
    [InlineData(false, 1000, 500, 500, false)] // partial pay
    [InlineData(false, 0, 0, 0, false)]        // empty financial row — NOT zeroed
    [InlineData(true, 1000, 1000, 0, false)]   // legal
    public void IsZeroedCustomer_Paid_Covers_Sale_With_Real_Sale(
        bool isLegal, double sale, double paid, double remaining, bool expected)
    {
        var asOf = new DateTime(2026, 10, 26);
        var fact = new BE_Company.Sales.Rating.CustomerRatingFacts(
            1, 10, isLegal, false, asOf.AddDays(-10), asOf, sale, paid, remaining);

        Assert.Equal(expected, DirectorSalesFollowupRules.IsZeroedCustomer(fact));
    }

    [Fact]
    public void CountZeroedCustomers_Ignores_Empty_Legal_And_Open_Balance()
    {
        var asOf = new DateTime(2026, 10, 26);
        var facts = new[]
        {
            new BE_Company.Sales.Rating.CustomerRatingFacts(
                1, 10, false, false, asOf.AddDays(-10), asOf, 1000, 1000, 0),
            new BE_Company.Sales.Rating.CustomerRatingFacts(
                2, 10, false, false, asOf.AddDays(-10), asOf, 1000, 0, 1000),
            new BE_Company.Sales.Rating.CustomerRatingFacts(
                3, 10, true, false, asOf.AddDays(-10), asOf, 1000, 1000, 0),
            new BE_Company.Sales.Rating.CustomerRatingFacts(
                4, 10, false, false, asOf.AddDays(-10), asOf, 0, 0, 0),
        };

        Assert.Equal(1, DirectorSalesFollowupRules.CountZeroedCustomers(facts));
    }

    [Fact]
    public void BuildSystemAverageHistory_Returns_Requested_Days_Newest_First()
    {
        var asOf = new DateTime(2026, 10, 26);
        var facts = new[]
        {
            new BE_Company.Sales.Rating.CustomerRatingFacts(
                1, 10, false, false, asOf.AddDays(-40), asOf.AddDays(-1), 1000, 200, 800),
        };

        var hist = DirectorSalesFollowupRules.BuildSystemAverageHistory(facts, asOf, days: 5);

        Assert.Equal(5, hist.Count);
        Assert.Equal(asOf.Date, hist[0].RatingDate.Date);
        Assert.Equal(asOf.Date.AddDays(-4), hist[4].RatingDate.Date);
    }
}
