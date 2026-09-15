using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class ExceptionReviewCustomerMatcherTests
{
    private const string Basra = "basra-demo";
    private const string Najaf = "najaf-demo";

    private static ExceptionReviewCandidate C(
        int id,
        string name,
        string? phone,
        string? city,
        string? province = null) => new()
    {
        CustomerId = id,
        FullName = name,
        Phone = phone,
        CityValue = city,
        CityName = province,
        Province = province
    };

    [Fact]
    public void No_Same_Province_Match_Is_New()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07801111111", Basra, Basra,
            [C(1, "شخص آخر", "07802222222", Basra)]);
        var cls = ExceptionReviewCustomerMatcher.Classify(hits);
        Assert.Empty(hits);
        Assert.Equal("New", cls.Type);
        Assert.Equal(ExceptionReviewCustomerMatcher.LabelNew, cls.LabelArabic);
        Assert.Contains("لم يتم العثور", cls.ExplanationArabic);
    }

    [Fact]
    public void Same_Phone_Same_Province_Is_Existing()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "اسم مختلف تماما فلان", "07801111111", Basra, Basra,
            [C(2, "زبون قديم آخر", "7801111111", Basra, "البصرة")]);
        Assert.Single(hits);
        Assert.True(hits[0].PhoneMatch);
        Assert.False(hits[0].TripleNameMatch);
        Assert.Equal([ExceptionReviewCustomerMatcher.ReasonPhone], hits[0].MatchReasons);
        Assert.Equal("Existing", ExceptionReviewCustomerMatcher.Classify(hits).Type);
    }

    [Fact]
    public void Triple_Name_Same_Province_Is_Existing()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07809999999", Basra, Basra,
            [C(3, "محمد علي حسن", "07808888888", Basra)]);
        Assert.Single(hits);
        Assert.True(hits[0].TripleNameMatch);
        Assert.Equal([ExceptionReviewCustomerMatcher.ReasonTriple], hits[0].MatchReasons);
    }

    [Fact]
    public void Two_Part_Name_Is_Not_Existing()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "علي حسن", "07809999999", Basra, Basra,
            [C(4, "علي حسن محمد", "07807777777", Basra)]);
        Assert.Empty(hits);
        Assert.Equal("New", ExceptionReviewCustomerMatcher.Classify(hits).Type);
    }

    [Fact]
    public void Same_Phone_Different_Province_Is_Not_Match()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07801111111", Basra, Basra,
            [C(5, "محمد علي حسن", "07801111111", Najaf, "النجف")]);
        Assert.Empty(hits);
    }

    [Fact]
    public void Same_Triple_Name_Different_Province_Is_Not_Match()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07809999999", Basra, Basra,
            [C(6, "محمد علي حسن", "07808888888", Najaf)]);
        Assert.Empty(hits);
    }

    [Fact]
    public void Whitespace_Normalized_Triple_Name_Is_Existing()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "  محمد   علي   حسن  ", "07809999999", Basra, Basra,
            [C(7, "محمد علي حسن", "07806666666", Basra)]);
        Assert.Single(hits);
        Assert.True(hits[0].TripleNameMatch);
    }

    [Fact]
    public void Multiple_Same_Province_Matches_All_Returned()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07801111111", Basra, Basra,
            [
                C(8, "محمد علي حسن", "07808888888", Basra),
                C(9, "شخص مختلف", "07801111111", Basra),
                C(10, "محمد علي حسن", "07801111111", Najaf) // foreign — ignored
            ]);
        Assert.Equal(2, hits.Count);
        Assert.Equal(2, ExceptionReviewCustomerMatcher.Classify(hits).MatchCount);
        Assert.DoesNotContain(hits, h => h.Customer.CustomerId == 10);
    }

    [Fact]
    public void Phone_And_Name_Yield_Both_Reason()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07801111111", Basra, Basra,
            [C(11, "محمد علي حسن", "07801111111", Basra)]);
        Assert.Single(hits);
        Assert.Equal([ExceptionReviewCustomerMatcher.ReasonBoth], hits[0].MatchReasons);
    }

    [Fact]
    public void No_Fuzzy_False_Positive_On_Partial_Name()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07809999999", Basra, Basra,
            [C(12, "محمد علي كاظم", "07807777777", Basra)]);
        Assert.Empty(hits);
    }

    [Fact]
    public void Kinship_Only_Does_Not_Classify_Existing()
    {
        // Different first name, same father+grandfather, different phone → kinship would match in SM eval,
        // but must NOT classify Existing for DM review.
        Assert.True(SalesRequestNameSimilarity.IsFatherOrGrandfatherMatch(
            "صادق جعفر حنيو", "محمد جعفر حنيو"));
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "صادق جعفر حنيو", "07801111111", Basra, Basra,
            [C(13, "محمد جعفر حنيو", "07802222222", Basra)]);
        Assert.Empty(hits);
        Assert.Equal("New", ExceptionReviewCustomerMatcher.Classify(hits).Type);
    }

    [Fact]
    public void Missing_Candidate_CityValue_Without_Branch_Stamp_Is_Excluded()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07801111111", Basra, branchCityValue: null,
            [C(14, "محمد علي حسن", "07801111111", city: null)]);
        Assert.Empty(hits);
    }

    [Fact]
    public void Branch_Stamp_Allows_Catalog_Rows_Without_Per_Row_City()
    {
        var hits = ExceptionReviewCustomerMatcher.FindMatches(
            "محمد علي حسن", "07801111111", Basra, Basra,
            [C(15, "محمد علي حسن", "07801111111", city: null)]);
        Assert.Single(hits);
    }
}

public class ExceptionReviewSourceMapperTests
{
    [Fact]
    public void Delegate_Maps_To_Mandoub()
    {
        var src = ExceptionReviewSourceMapper.FromRequest(new SalesRequestDTO
        {
            CustomerSourceType = SalesRequestSources.Delegate,
            CreatedByName = "أحمد علي",
            CityName = "البصرة",
            SourceListId = 5
        }, listName: "العشار الأولى");
        Assert.Equal("مندوب", src.DisplayLabel);
        Assert.Equal("أحمد علي", src.PersonName);
        Assert.Equal("العشار الأولى", src.ListName);
        Assert.Equal("البصرة", src.BranchName);
    }

    [Fact]
    public void Delegate_Missing_List_Name_Shows_Unavailable()
    {
        var src = ExceptionReviewSourceMapper.FromRequest(new SalesRequestDTO
        {
            CustomerSourceType = SalesRequestSources.Delegate,
            CreatedByName = "أحمد",
            SourceListId = 9,
            CityName = "النجف"
        }, listName: null);
        Assert.Equal(ExceptionReviewSourceMapper.Unavailable, src.ListName);
    }

    [Fact]
    public void Follower_Maps_With_List()
    {
        var src = ExceptionReviewSourceMapper.FromRequest(new SalesRequestDTO
        {
            CustomerSourceType = SalesRequestSources.Follower,
            CreatedByName = "وليد راشد",
            CityName = "البصرة",
            SourceListId = 3
        }, "الزبير الأولى");
        Assert.Equal("متابع", src.DisplayLabel);
        Assert.Equal("وليد راشد", src.PersonName);
        Assert.Equal("الزبير الأولى", src.ListName);
    }

    [Fact]
    public void Employee_Submitted_Maps_Employee_Name()
    {
        var src = ExceptionReviewSourceMapper.FromRequest(new SalesRequestDTO
        {
            CustomerSourceType = SalesRequestSources.EmployeeSubmitted,
            CreatedByName = "سامر كريم",
            CityName = "الكرخ"
        });
        Assert.Equal("موظف مبيعات", src.DisplayLabel);
        Assert.Equal("سامر كريم", src.PersonName);
        Assert.Null(src.ListName);
    }

    [Fact]
    public void Missing_Source_Metadata_Is_Unavailable()
    {
        var src = ExceptionReviewSourceMapper.FromRequest(null);
        Assert.Equal(ExceptionReviewSourceMapper.Unavailable, src.DisplayLabel);
        Assert.Equal(ExceptionReviewSourceMapper.Unavailable, src.PersonName);
    }
}
