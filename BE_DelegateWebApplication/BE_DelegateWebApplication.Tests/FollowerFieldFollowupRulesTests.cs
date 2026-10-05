using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class FollowerFieldFollowupRulesTests
{
    [Fact]
    public void CompletionNote_IsRequired()
    {
        Assert.False(FollowerFieldFollowupRules.IsValidCompletionNote(null));
        Assert.False(FollowerFieldFollowupRules.IsValidCompletionNote(""));
        Assert.False(FollowerFieldFollowupRules.IsValidCompletionNote("   "));
        Assert.True(FollowerFieldFollowupRules.IsValidCompletionNote("تم الزيارة"));
    }

    [Fact]
    public void GroupPending_ByListName()
    {
        var rows = new[]
        {
            new FollowerFieldFollowupRow { Id = 1, ListId = 10, ListName = "السوق", CustomerName = "أ" },
            new FollowerFieldFollowupRow { Id = 2, ListId = 10, ListName = "السوق", CustomerName = "ب" },
            new FollowerFieldFollowupRow { Id = 3, ListId = 20, ListName = "الصناعي", CustomerName = "ج" },
        };
        var groups = rows
            .GroupBy(r => r.ListId)
            .OrderBy(g => g.First().ListName)
            .Select(g => (g.Key, g.First().ListName, Count: g.Count()))
            .ToList();
        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.ListName == "السوق" && g.Count == 2);
        Assert.Contains(groups, g => g.ListName == "الصناعي" && g.Count == 1);
    }
}
