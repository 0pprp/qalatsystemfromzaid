using BE_SalesEmployee.MainFollower.Domain;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class MissionAccessRulesTests
{
    [Fact]
    public void Member_may_access_city_on_mission()
    {
        var mission = new MissionRecord
        {
            Id = 1,
            MemberUserNames = ["mf1", "mf2"],
            CityValues = ["DatabaseCompanyBasra", "DatabaseCompanyNajaf"]
        };
        Assert.Null(MissionAccessRules.AuthorizeMemberCity(mission, "mf1", "DatabaseCompanyBasra"));
    }

    [Fact]
    public void Rejects_non_member_or_wrong_city()
    {
        var mission = new MissionRecord
        {
            Id = 1,
            MemberUserNames = ["mf1", "mf2"],
            CityValues = ["DatabaseCompanyBasra"]
        };
        Assert.NotNull(MissionAccessRules.AuthorizeMemberCity(mission, "x", "DatabaseCompanyBasra"));
        Assert.NotNull(MissionAccessRules.AuthorizeMemberCity(mission, "mf1", "DatabaseCompanyNajaf"));
    }

    [Fact]
    public void StaffNotes_Scope_UsesServerSideAssignments_NotCallerCityAlone()
    {
        var missions = new[]
        {
            new MissionRecord
            {
                Id = 1,
                MemberUserNames = ["mf1"],
                CityValues = ["DatabaseCompanyBasra"]
            },
            new MissionRecord
            {
                Id = 2,
                MemberUserNames = ["other"],
                CityValues = ["DatabaseCompanyNajaf"]
            }
        };

        var allowed = MissionAccessRules.AllowedCitiesForMember(missions, "mf1");
        Assert.Single(allowed);
        Assert.Contains("DatabaseCompanyBasra", allowed);
        Assert.Null(MissionAccessRules.AuthorizeOptionalCityFilter(allowed, null));
        Assert.Null(MissionAccessRules.AuthorizeOptionalCityFilter(allowed, "DatabaseCompanyBasra"));
        Assert.NotNull(MissionAccessRules.AuthorizeOptionalCityFilter(allowed, "DatabaseCompanyNajaf"));
        Assert.False(MissionAccessRules.CityIsAllowed(allowed, "DatabaseCompanyNajaf"));
        Assert.True(MissionAccessRules.CityIsAllowed(allowed, "DatabaseCompanyBasra"));
    }
}
