using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class GlobalSalesManagerRulesTests
{
    private static (int, string, Guid?, bool) R(int id, string name, Guid? gid, bool active = true) =>
        (id, name, gid, active);

    [Fact]
    public void Empty_Is_NotFound_And_Can_Create()
    {
        var r = GlobalSalesManagerRules.EvaluatePreflight([], "sm1", Guid.NewGuid());
        Assert.Equal(GlobalManagerPreflightStatus.NotFound, r.Status);
        Assert.True(GlobalSalesManagerRules.CanProceedWithCreate(r.Status));
    }

    [Fact]
    public void Same_Global_Is_Idempotent()
    {
        var gid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [R(5, "sm1", gid)], "sm1", gid);
        Assert.Equal(GlobalManagerPreflightStatus.ExistingSameGlobalAccount, r.Status);
        Assert.True(GlobalSalesManagerRules.CanProceedWithCreate(r.Status));
    }

    [Fact]
    public void Username_With_Different_Global_Is_Conflict()
    {
        var existing = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var requested = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [R(5, "sm1", existing)], "sm1", requested);
        Assert.Equal(GlobalManagerPreflightStatus.UsernameConflict, r.Status);
        Assert.False(GlobalSalesManagerRules.CanProceedWithCreate(r.Status));
    }

    [Fact]
    public void Duplicate_Active_Username_Is_Ambiguous()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [R(1, "sm1", gid), R(2, "sm1", Guid.NewGuid())], "sm1", gid);
        Assert.Equal(GlobalManagerPreflightStatus.DuplicateAmbiguous, r.Status);
    }

    [Fact]
    public void Forced_UserType_Is_SalesManager_Only()
    {
        Assert.Equal("مدير مبيعات", GlobalSalesManagerRules.ForcedUserType);
    }

    [Fact]
    public void Rename_Same_Global_Different_Name_Is_ExistingSame()
    {
        var gid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [R(5, "old-name", gid)], "new-name", gid);
        Assert.Equal(GlobalManagerPreflightStatus.ExistingSameGlobalAccount, r.Status);
        Assert.Equal(5, r.UserId);
    }
}
