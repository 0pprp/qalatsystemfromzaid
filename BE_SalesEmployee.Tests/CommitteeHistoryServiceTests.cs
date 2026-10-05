using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.MainFollower.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CommitteeHistoryServiceTests
{
    private static (CommitteeService Committees, SaleReviewService Sales, StaffReviewService Staff, CommitteeHistoryService History) Sut()
    {
        var committees = new InMemoryCommitteeStore();
        var sales = new InMemorySaleReviewStore();
        var staff = new InMemoryStaffReviewStore();
        return (
            new CommitteeService(committees),
            new SaleReviewService(committees, sales),
            new StaffReviewService(committees, staff),
            new CommitteeHistoryService(committees, sales, staff));
    }

    [Fact]
    public async Task City_history_survives_across_missions_for_same_province()
    {
        var (committees, sales, staff, history) = Sut();
        var c1 = await committees.CreateCommitteeAsync("dm", "DM", "لجنة أ", ["mf1", "mf2"], default);
        var m1 = await committees.CreateMissionAsync("dm", c1.Id, ["DatabaseCompanyBasra"], "جولة 1", default);
        await sales.SubmitAsync("mf1", m1.Id, "DatabaseCompanyBasra", 10, "زبون", 8, "جيد",
            shopStockEstimatedValue: 1_000_000, estimatedDailyRevenue: 50_000, ct: default);
        await staff.SubmitAsync("mf1", m1.Id, "DatabaseCompanyBasra", StaffTargetTypes.Delegate, 5, "مندوب", 5, 7, "ملاحظة", default);

        var c2 = await committees.CreateCommitteeAsync("dm", "DM", "لجنة ب", ["mf1", "mf2"], default);
        var m2 = await committees.CreateMissionAsync("dm", c2.Id, ["DatabaseCompanyBasra"], "جولة 2", default);

        var rows = await history.ListCityHistoryForMemberAsync("mf1", "DatabaseCompanyBasra", default);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Kind == "sale");
        Assert.Contains(rows, r => r.Kind == "staff");

        var summary = await history.SummarizeMissionAsync(m1.Id, default);
        Assert.Equal(1, summary.SaleReviewCount);
        Assert.Equal(1, summary.StaffReviewCount);
        Assert.Equal(8, summary.AvgSaleScore);
        _ = m2;
    }
}
