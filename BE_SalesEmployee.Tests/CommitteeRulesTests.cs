using BE_SalesEmployee.MainFollower.Domain;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CommitteeRulesTests
{
    [Fact]
    public void CreateCommittee_requires_at_least_two_distinct_members()
    {
        var err = CommitteeRules.ValidateMembers(["u1"]);
        Assert.NotNull(err);

        err = CommitteeRules.ValidateMembers(["u1", "u1"]);
        Assert.NotNull(err);

        err = CommitteeRules.ValidateMembers(["u1", "u2"]);
        Assert.Null(err);
    }

    [Fact]
    public void CreateMission_requires_at_least_one_city()
    {
        Assert.NotNull(CommitteeRules.ValidateCities([]));
        Assert.NotNull(CommitteeRules.ValidateCities(["", "  "]));
        Assert.Null(CommitteeRules.ValidateCities(["DatabaseCompanyBasra"]));
        Assert.Null(CommitteeRules.ValidateCities(["DatabaseCompanyBasra", "DatabaseCompanyNajaf"]));
    }

    [Fact]
    public void NormalizeCities_trims_and_dedupes_case_insensitive()
    {
        var cities = CommitteeRules.NormalizeCities(["  Basra ", "basra", "Najaf"]);
        Assert.Equal(2, cities.Count);
        Assert.Contains(cities, c => c.Equals("Basra", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cities, c => c.Equals("Najaf", StringComparison.OrdinalIgnoreCase));
    }
}
