using BE_SalesEmployee.DelegatedManager.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class BmBranchVisitRulesTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(-1, false)]
    [InlineData(11, false)]
    public void IsValidDelegateScore_0_to_10(int score, bool expected)
        => Assert.Equal(expected, BmBranchVisitRules.IsValidDelegateScore(score));

    [Fact]
    public void CanOpen_rejects_when_actor_already_has_open_visit()
    {
        Assert.False(BmBranchVisitRules.CanOpen(hasOpenVisitForActor: true));
        Assert.True(BmBranchVisitRules.CanOpen(hasOpenVisitForActor: false));
    }

    [Fact]
    public void CanDepart_only_when_open_and_opened_by_actor()
    {
        Assert.True(BmBranchVisitRules.CanDepart(isOpen: true, openedByActor: true));
        Assert.False(BmBranchVisitRules.CanDepart(isOpen: true, openedByActor: false));
        Assert.False(BmBranchVisitRules.CanDepart(isOpen: false, openedByActor: true));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("ملاحظة قصيرة", false)]
    [InlineData("هذه ملاحظة طويلة بما يكفي لوصف العمل الذي تم في الفرع اليوم مع التفاصيل المطلوبة.", true)]
    public void IsValidDepartNote_requires_meaningful_text(string? note, bool expected)
        => Assert.Equal(expected, BmBranchVisitRules.IsValidDepartNote(note));

    [Fact]
    public void AreAllDelegatesRated_requires_every_id()
    {
        Assert.True(BmBranchVisitRules.AreAllDelegatesRated([], [1]));
        Assert.False(BmBranchVisitRules.AreAllDelegatesRated([1, 2], [1]));
        Assert.True(BmBranchVisitRules.AreAllDelegatesRated([1, 2], [2, 1, 9]));
    }

    [Fact]
    public void MembersForCity_includes_active_users_assigned_to_city()
    {
        IReadOnlyList<(Guid Id, string DisplayName, bool IsActive, IReadOnlyList<string> Cities)> users =
        [
            (Guid.Parse("11111111-1111-1111-1111-111111111111"), "أحمد", true, new[] { "najaf", "basra" }),
            (Guid.Parse("22222222-2222-2222-2222-222222222222"), "سارة", true, new[] { "basra" }),
            (Guid.Parse("33333333-3333-3333-3333-333333333333"), "خالد", false, new[] { "najaf" }),
            (Guid.Parse("44444444-4444-4444-4444-444444444444"), "زينب", true, new[] { "kirkuk" }),
        ];

        var members = BmBranchVisitRules.SelectMembersForCity(users, "najaf");
        Assert.Single(members);
        Assert.Equal("أحمد", members[0].DisplayName);
    }
}
