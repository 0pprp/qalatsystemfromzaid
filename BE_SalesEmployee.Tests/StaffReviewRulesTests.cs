using BE_SalesEmployee.MainFollower.Domain;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class StaffReviewRulesTests
{
    [Theory]
    [InlineData("Delegate")]
    [InlineData("SubFollower")]
    [InlineData("SalesEmployee")]
    public void Accepts_known_target_types(string type)
    {
        Assert.Null(StaffReviewRules.ValidateTargetType(type));
    }

    [Fact]
    public void Rejects_unknown_target_type()
    {
        Assert.NotNull(StaffReviewRules.ValidateTargetType("Boss"));
    }

    [Fact]
    public void Score_rules_match_sale_reviews()
    {
        // SaleReviewRules: note optional when score >= OptionalNoteMinScore (7).
        Assert.Null(StaffReviewRules.ValidateScoreAndNote(9, null));
        Assert.Null(StaffReviewRules.ValidateScoreAndNote(9, "ممتاز"));
        Assert.NotNull(StaffReviewRules.ValidateScoreAndNote(4, null));
    }

    [Fact]
    public void Builds_durable_note_for_target_feed()
    {
        var text = StaffReviewRules.BuildFeedNote(
            committeeName: "لجنة 1",
            targetType: StaffTargetTypes.Delegate,
            targetName: "مندوب أحمد",
            score: 8,
            userNote: "أداء جيد",
            atUtc: new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc));
        Assert.Contains("لجنة 1", text);
        Assert.Contains("مندوب أحمد", text);
        Assert.Contains("8/10", text);
        Assert.Contains("أداء جيد", text);
    }
}
