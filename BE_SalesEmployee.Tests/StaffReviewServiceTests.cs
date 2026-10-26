using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.MainFollower.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class StaffReviewServiceTests
{
    private static (CommitteeService Committees, StaffReviewService Reviews) Sut()
    {
        var committees = new InMemoryCommitteeStore();
        var reviews = new InMemoryStaffReviewStore();
        return (new CommitteeService(committees), new StaffReviewService(committees, reviews));
    }

    [Fact]
    public async Task Submits_delegate_review_and_lists_on_target_feed()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة الرقابة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync("dm", committee.Id, ["DatabaseCompanyBasra"], null, default);

        var saved = await reviews.SubmitAsync(
            "mf1", mission.Id, "DatabaseCompanyBasra",
            StaffTargetTypes.Delegate, targetId: 55, targetName: "مندوب البصرة",
            listId: 12, score: 8, userNote: "ممتاز في التحصيل", ct: default);

        Assert.Contains("لجنة الرقابة", saved.FeedNote);
        Assert.Contains("8/10", saved.FeedNote);

        var feed = await reviews.ListForTargetAsync(StaffTargetTypes.Delegate, 55, "DatabaseCompanyBasra", default);
        Assert.Single(feed);
        Assert.Equal(saved.Id, feed[0].Id);
    }

    [Fact]
    public async Task Rejects_low_score_without_note()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync("dm", committee.Id, ["DatabaseCompanyBasra"], null, default);

        await Assert.ThrowsAsync<CommitteeException>(() =>
            reviews.SubmitAsync("mf1", mission.Id, "DatabaseCompanyBasra",
                StaffTargetTypes.SalesEmployee, 9, "صالح", null, 6, null, default));
    }
}
