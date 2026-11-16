using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CommitteeBranchScopeTests
{
    private static readonly IReadOnlyList<AdminCity> AllBranches =
    [
        new AdminCity { Value = "najaf", Name = "النجف" },
        new AdminCity { Value = "karbala", Name = "كربلاء" },
        new AdminCity { Value = "basra", Name = "البصرة" },
    ];

    [Theory]
    [InlineData("مسؤول لجنة المراقبة")]
    [InlineData("مساعد مسؤول لجنة المراقبة")]
    public void Committee_fanout_only_reaches_its_branches(string role)
    {
        var user = new GatewayUser { UserType = role, AllowedFilterCities = ["najaf", "basra"] };

        var scoped = SalesManagerBranchAggregator.ScopeToCommittee(user, AllBranches);

        Assert.Equal(["najaf", "basra"], scoped.Select(c => c.Value));
    }

    [Fact]
    public void Committee_without_branches_gets_nothing()
    {
        var user = new GatewayUser { UserType = "مسؤول لجنة المراقبة" };

        Assert.Empty(SalesManagerBranchAggregator.ScopeToCommittee(user, AllBranches));
    }

    [Theory]
    [InlineData("المدير")]
    [InlineData("مسؤول المراقبين")]
    [InlineData("مدير مبيعات")]
    public void Non_committee_roles_are_not_scoped(string role)
    {
        var user = new GatewayUser { UserType = role };

        Assert.Equal(3, SalesManagerBranchAggregator.ScopeToCommittee(user, AllBranches).Count);
    }
}
