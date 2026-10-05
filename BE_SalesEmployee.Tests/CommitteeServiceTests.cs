using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.MainFollower.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CommitteeServiceTests
{
    private static CommitteeService Sut() => new(new InMemoryCommitteeStore());

    [Fact]
    public async Task Dm_creates_committee_with_two_members()
    {
        var svc = Sut();
        var committee = await svc.CreateCommitteeAsync(
            createdByUserName: "dm1",
            createdByDisplayName: "المدير المفوض",
            name: "لجنة 1",
            memberUserNames: ["mf1", "mf2"],
            ct: default);

        Assert.True(committee.Id > 0);
        Assert.Equal("لجنة 1", committee.Name);
        Assert.Equal(2, committee.MemberUserNames.Count);
    }

    [Fact]
    public async Task Dm_rejects_committee_with_one_member()
    {
        var svc = Sut();
        var ex = await Assert.ThrowsAsync<CommitteeException>(() =>
            svc.CreateCommitteeAsync("dm1", "DM", "لجنة", ["only"], default));
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public async Task Dm_creates_mission_with_cities_and_member_sees_it()
    {
        var svc = Sut();
        var committee = await svc.CreateCommitteeAsync("dm1", "DM", "لجنة 1", ["mf1", "mf2"], default);
        var mission = await svc.CreateMissionAsync(
            createdByUserName: "dm1",
            committeeId: committee.Id,
            cityValues: ["DatabaseCompanyBasra", "DatabaseCompanyNajaf"],
            note: "جولة أسبوع",
            ct: default);

        Assert.Equal(2, mission.CityValues.Count);
        Assert.Equal(CommitteeMissionStatuses.Active, mission.Status);

        var forMf1 = await svc.ListMissionsForMemberAsync("mf1", default);
        Assert.Single(forMf1);
        Assert.Equal(mission.Id, forMf1[0].Id);

        var forStranger = await svc.ListMissionsForMemberAsync("other", default);
        Assert.Empty(forStranger);
    }

    [Fact]
    public async Task Mission_requires_at_least_one_city()
    {
        var svc = Sut();
        var committee = await svc.CreateCommitteeAsync("dm1", "DM", "لجنة", ["a", "b"], default);
        await Assert.ThrowsAsync<CommitteeException>(() =>
            svc.CreateMissionAsync("dm1", committee.Id, [], null, default));
    }
}
