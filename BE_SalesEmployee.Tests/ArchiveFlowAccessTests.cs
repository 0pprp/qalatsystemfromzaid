using BE_SalesEmployee.Middleware;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class ArchiveFlowAccessTests
{
    [Fact]
    public void ArchiveManager_IsItsOwnModuleRole()
    {
        Assert.Equal(SalesRoles.ArchiveManager, SalesRoles.ToModuleRole("مسؤول الارشيف"));
        Assert.True(OversightAccessRules.IsKnownRole("مسؤول الارشيف"));
        Assert.False(OversightAccessRules.IsCommitteeRole("مسؤول الارشيف"));
    }

    [Theory]
    [InlineData("المدير", true)]
    [InlineData("مسؤول المراقبين", false)]
    [InlineData("مسؤول لجنة المراقبة", false)]
    public void OnlyDirector_CreatesArchiveManager(string actor, bool allowed)
    {
        Assert.Equal(allowed, OversightAccessRules.CanCreate(actor, "مسؤول الارشيف"));
    }

    [Fact]
    public void Committee_MayReachArchiveFlowOnly()
    {
        Assert.True(CommitteeScopeMiddleware.IsAllowedPath("/api/archive-flow/committee/items"));
        Assert.False(CommitteeScopeMiddleware.IsAllowedPath("/api/archive-flowx"));
    }

    [Fact]
    public void Recipients_AreCommitteeAccountsCoveringTheBranch_HeadFirst()
    {
        var accounts = new[]
        {
            new OversightAccountRecord { Id = 1, UserName = "مساعد", DisplayName = "مساعد", Role = "مساعد مسؤول لجنة المراقبة", CityValues = ["najaf"] },
            new OversightAccountRecord { Id = 2, UserName = "رئيس", DisplayName = "رئيس", Role = "مسؤول لجنة المراقبة", CityValues = ["NAJAF", "karbala"] },
            new OversightAccountRecord { Id = 3, UserName = "بعيد", DisplayName = "بعيد", Role = "مسؤول لجنة المراقبة", CityValues = ["basra"] },
            new OversightAccountRecord { Id = 4, UserName = "ارشيف", DisplayName = "ارشيف", Role = "مسؤول الارشيف" },
        };
        var rows = ArchiveRecipientsService.Filter(accounts, "najaf");
        Assert.Equal(["رئيس", "مساعد"], rows.Select(r => r.Name).ToArray());
    }
}
