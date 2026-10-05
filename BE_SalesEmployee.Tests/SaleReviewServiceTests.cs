using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.MainFollower.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SaleReviewServiceTests
{
    private static (CommitteeService Committees, SaleReviewService Reviews) Sut()
    {
        var committees = new InMemoryCommitteeStore();
        var reviews = new InMemorySaleReviewStore();
        return (new CommitteeService(committees), new SaleReviewService(committees, reviews));
    }

    private static Task<SaleReviewRecord> SubmitOk(
        SaleReviewService reviews,
        string user,
        int missionId,
        string city,
        int saleId,
        int score = 8,
        string? note = null) =>
        reviews.SubmitAsync(
            user, missionId, city, saleId, "زبون", score, note,
            shopStockEstimatedValue: 1_000_000,
            estimatedDailyRevenue: 50_000,
            ct: default);

    [Fact]
    public async Task Member_can_submit_sale_auth_on_assigned_city()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync(
            "dm", committee.Id, ["DatabaseCompanyBasra"], "جولة", default);

        var saved = await SubmitOk(reviews, "mf1", mission.Id, "DatabaseCompanyBasra", 101);

        Assert.True(saved.Id > 0);
        Assert.Equal(8, saved.Score);
        Assert.Equal(1_000_000, saved.ShopStockEstimatedValue);
        Assert.Equal(50_000, saved.EstimatedDailyRevenue);
        Assert.Equal(mission.Id, saved.MissionId);

        var list = await reviews.ListForMissionAsync(mission.Id, default);
        Assert.Single(list);
    }

    [Fact]
    public async Task Rejects_score_without_note_when_le_6()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync(
            "dm", committee.Id, ["DatabaseCompanyBasra"], null, default);

        await Assert.ThrowsAsync<CommitteeException>(() =>
            SubmitOk(reviews, "mf1", mission.Id, "DatabaseCompanyBasra", 1, score: 6, note: null));
    }

    [Fact]
    public async Task Rejects_missing_shop_stock()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync(
            "dm", committee.Id, ["DatabaseCompanyBasra"], null, default);

        var ex = await Assert.ThrowsAsync<CommitteeException>(() =>
            reviews.SubmitAsync(
                "mf1", mission.Id, "DatabaseCompanyBasra", 1, "زبون", 8, null,
                shopStockEstimatedValue: 0, estimatedDailyRevenue: 50_000, ct: default));
        Assert.Contains("بضاعة", ex.Message);
    }

    [Fact]
    public async Task Rejects_second_auth_for_same_sale()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync(
            "dm", committee.Id, ["DatabaseCompanyBasra"], null, default);

        await SubmitOk(reviews, "mf1", mission.Id, "DatabaseCompanyBasra", 42);

        var ex = await Assert.ThrowsAsync<CommitteeException>(() =>
            SubmitOk(reviews, "mf1", mission.Id, "DatabaseCompanyBasra", 42, score: 9));
        Assert.Contains("سبق", ex.Message);
    }

    [Fact]
    public async Task Rejects_review_for_city_not_on_mission()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync(
            "dm", committee.Id, ["DatabaseCompanyBasra"], null, default);

        await Assert.ThrowsAsync<CommitteeException>(() =>
            SubmitOk(reviews, "mf1", mission.Id, "DatabaseCompanyNajaf", 1));
    }

    [Fact]
    public async Task Non_member_cannot_submit()
    {
        var (committees, reviews) = Sut();
        var committee = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync(
            "dm", committee.Id, ["DatabaseCompanyBasra"], null, default);

        await Assert.ThrowsAsync<CommitteeException>(() =>
            SubmitOk(reviews, "stranger", mission.Id, "DatabaseCompanyBasra", 1));
    }
}
