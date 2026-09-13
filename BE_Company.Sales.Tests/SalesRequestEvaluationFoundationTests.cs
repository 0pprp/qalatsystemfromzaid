using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesPhoneNormalizerTests
{
    [Theory]
    [InlineData("07804924373", "7804924373")]
    [InlineData("7804924373", "7804924373")]
    [InlineData("+9647804924373", "7804924373")]
    [InlineData("009647804924373", "7804924373")]
    [InlineData("0780-492-4373", "7804924373")]
    public void ForMatch_Unifies_Iraqi_Mobile(string input, string expected) =>
        Assert.Equal(expected, SalesPhoneNormalizer.ForMatch(input));

    [Theory]
    [InlineData("7804924373", "07804924373")]
    [InlineData("07804924373", "07804924373")]
    [InlineData("+9647804924373", "07804924373")]
    public void ForStorage_Adds_Leading_Zero_When_Needed(string input, string expected) =>
        Assert.Equal(expected, SalesPhoneNormalizer.ForStorage(input));

    [Fact]
    public void Matches_Ignores_Leading_Zero() =>
        Assert.True(SalesPhoneNormalizer.Matches("07804924373", "7804924373"));

    [Fact]
    public void ForMatch_Rejects_Short_Partial() =>
        Assert.Equal(string.Empty, SalesPhoneNormalizer.ForMatch("780492"));

    [Fact]
    public void Excel_Scientific_TenDigit_Mobile_Parses()
    {
        // Excel may emit scientific notation for 10-digit phones stored as numbers.
        Assert.Equal("07804924373", SalesPhoneNormalizer.ForStorage("7.804924373E9"));
    }
}

public class SalesRequestNameSimilarityTests
{
    [Fact]
    public void TripleName_Matches_Trailing_Numeric_Variants()
    {
        Assert.True(SalesRequestNameSimilarity.IsTripleNameMatch(
            "أحمد منتظر سرحان", "أحمد منتظر سرحان 2"));
        Assert.True(SalesRequestNameSimilarity.IsTripleNameMatch(
            "أحمد منتظر سرحان", "أحمد منتظر سرحان 3"));
        Assert.True(SalesRequestNameSimilarity.IsTripleNameMatch(
            "أحمد منتظر سرحان", "احمد منتظر سرحان"));
    }

    [Fact]
    public void TripleName_Rejects_Clearly_Different()
    {
        Assert.False(SalesRequestNameSimilarity.IsTripleNameMatch(
            "أحمد منتظر سرحان", "علي حسين كاظم"));
    }

    [Theory]
    [InlineData("صادق جعفر حنيو", "محمد جعفر حنيو")]
    [InlineData("صادق جعفر حنيو", "علي جعفر حنيو")]
    [InlineData("صادق جعفر حنيو", "محمد جعفر حنيو 2")]
    [InlineData("صادق جعفر حنيو", "محمّد جعفر حنيو")]
    [InlineData("صادق جعفر حنيو", "احمد جعفر حنيو")]
    public void FatherGrandfather_Requires_Father_And_Grandfather_Pair(string query, string candidate)
    {
        Assert.True(SalesRequestNameSimilarity.IsFatherOrGrandfatherMatch(query, candidate));
        Assert.Equal(
            "تطابق اسم الأب والجد",
            SalesRequestNameSimilarity.FatherGrandfatherMatchReason(query, candidate));
    }

    [Theory]
    [InlineData("صادق جعفر حنيو", "حسين جعفر كريم")] // father only
    [InlineData("صادق جعفر حنيو", "موسى هادي حنيو")] // grandfather only
    [InlineData("صادق جعفر حنيو", "جعفر حنيو صادق")] // wrong token positions
    [InlineData("صادق جعفر حنيو", "صادق علي محمد")] // first name only
    [InlineData("أحمد منتظر سرحان", "هادي حيدر سرحان")] // grandfather only (legacy OR case)
    [InlineData("أحمد منتظر سرحان", "علي منتظر كاظم")] // father only (legacy OR case)
    public void FatherGrandfather_Rejects_Single_Token_Or_Wrong_Pair(string query, string candidate)
    {
        Assert.False(SalesRequestNameSimilarity.IsFatherOrGrandfatherMatch(query, candidate));
    }

    [Fact]
    public void FatherGrandfather_Normalization_Keeps_Pair_Match()
    {
        Assert.True(SalesRequestNameSimilarity.IsFatherOrGrandfatherMatch(
            "صادق جعفر حنيو", "محمّد جعفر حنيو"));
        Assert.True(SalesRequestNameSimilarity.IsFatherOrGrandfatherMatch(
            "صادق جعفر حنيو", "محمد جعفر حنيوـ"));
        Assert.Equal(
            "تطابق اسم الأب والجد",
            SalesRequestNameSimilarity.FatherGrandfatherMatchReason("صادق جعفر حنيو", "علي جعفر حنيو"));
    }
}

public class SalesRequestEvaluationRatingAggregateTests
{
    [Fact]
    public void Category_Worst_Is_Minimum_Score()
    {
        var worst = SalesRequestEvaluationAggregator.WorstOf(
        [
            (CustomerRatingLevel.Excellent, 10),
            (CustomerRatingLevel.Good, 5),
            (CustomerRatingLevel.Legal, -10)
        ]);
        Assert.Equal(CustomerRatingLevel.Legal, worst!.Value.Level);
        Assert.Equal(-10, worst.Value.Score);
    }

    [Fact]
    public void Overall_Ignores_Empty_Categories()
    {
        var overall = SalesRequestEvaluationAggregator.Overall(
            triple: (CustomerRatingLevel.Excellent, 10),
            phone: null,
            kinship: (CustomerRatingLevel.Rejected, -5));
        Assert.Equal(CustomerRatingLevel.Rejected, overall!.Value.Level);
        Assert.Equal(-5, overall.Value.Score);
    }

    [Fact]
    public void Overall_No_Matches_Is_Null()
    {
        Assert.Null(SalesRequestEvaluationAggregator.Overall(null, null, null));
    }
}
