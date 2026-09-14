using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class LegacyGlobalManagerAdoptionTests
{
    private static GlobalManagerCandidate C(
        int id,
        string name,
        Guid? gid,
        bool active = true,
        string userType = "مدير مبيعات",
        string? email = "a@x.com",
        string? phone = "0700",
        bool passwordMatches = true) =>
        new()
        {
            UserId = id,
            UserName = name,
            GlobalAccountId = gid,
            Active = active,
            UserType = userType,
            Email = email,
            PhoneNumber = phone,
            PasswordMatches = passwordMatches
        };

    private static GlobalManagerIdentityRequest IdReq(
        string name = "legacy-sm",
        string? email = "a@x.com",
        string? phone = "0700",
        string? password = "secret") =>
        new()
        {
            UserName = name,
            Email = email,
            PhoneNumber = phone,
            Password = password
        };

    [Fact]
    public void Legacy_Null_Global_Username_Only_Is_Conflict_Without_Identity()
    {
        // Username alone must never be enough to adopt.
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(1, "legacy-sm", null)],
            new GlobalManagerIdentityRequest { UserName = "legacy-sm", GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyConflict, r.Status);
        Assert.False(GlobalSalesManagerRules.CanProceedWithCreate(r.Status));
        Assert.False(GlobalSalesManagerRules.CanAdoptLegacy(r.Status));
    }

    [Fact]
    public void Matching_Active_Legacy_Is_Adoptable()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(9, "legacy-sm", null)],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyAdoptable, r.Status);
        Assert.Equal(9, r.UserId);
        Assert.True(GlobalSalesManagerRules.CanAdoptLegacy(r.Status));
        Assert.True(GlobalSalesManagerRules.CanProceedWithCreate(r.Status));
    }

    [Fact]
    public void Wrong_Role_Is_LegacyConflict()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(1, "legacy-sm", null, userType: "محاسب رئيسي")],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyConflict, r.Status);
    }

    [Fact]
    public void Duplicate_Active_Username_Is_Ambiguous_Not_Adopt()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(1, "legacy-sm", null), C(2, "legacy-sm", null)],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.DuplicateAmbiguous, r.Status);
        Assert.False(GlobalSalesManagerRules.CanAdoptLegacy(r.Status));
    }

    [Fact]
    public void Conflicting_Email_Is_LegacyConflict()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(1, "legacy-sm", null, email: "other@x.com")],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyConflict, r.Status);
    }

    [Fact]
    public void Conflicting_Phone_Is_LegacyConflict()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(1, "legacy-sm", null, phone: "0999")],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyConflict, r.Status);
    }

    [Fact]
    public void Password_Mismatch_Is_LegacyConflict()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(1, "legacy-sm", null, passwordMatches: false)],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyConflict, r.Status);
    }

    [Fact]
    public void Disabled_Legacy_Is_LegacyConflict_No_Silent_Enable()
    {
        var gid = Guid.NewGuid();
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(1, "legacy-sm", null, active: false)],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyConflict, r.Status);
        Assert.False(GlobalSalesManagerRules.CanAdoptLegacy(r.Status));
    }

    [Fact]
    public void Existing_Same_Global_Still_Idempotent()
    {
        var gid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(5, "legacy-sm", gid)],
            IdReq() with { GlobalAccountId = gid });
        Assert.Equal(GlobalManagerPreflightStatus.ExistingSameGlobalAccount, r.Status);
    }

    [Fact]
    public void Different_Global_On_Username_Is_UsernameConflict()
    {
        var existing = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var requested = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(5, "legacy-sm", existing)],
            IdReq() with { GlobalAccountId = requested });
        Assert.Equal(GlobalManagerPreflightStatus.UsernameConflict, r.Status);
    }

    [Fact]
    public void Discovery_Without_GlobalId_Bound_Row_Returns_ExistingSame()
    {
        var gid = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(5, "legacy-sm", gid)],
            IdReq() with { GlobalAccountId = null });
        Assert.Equal(GlobalManagerPreflightStatus.ExistingSameGlobalAccount, r.Status);
        Assert.Equal(gid, r.GlobalAccountId);
    }

    [Fact]
    public void Discovery_Without_GlobalId_Legacy_Null_Is_Adoptable_With_Identity()
    {
        var r = GlobalSalesManagerRules.EvaluatePreflight(
            [C(5, "legacy-sm", null)],
            IdReq() with { GlobalAccountId = null });
        Assert.Equal(GlobalManagerPreflightStatus.LegacyAdoptable, r.Status);
    }
}

public sealed class LegacyAdoptionOrchestrationPolicyTests
{
    [Fact]
    public void All_LegacyAdoptable_Allows_Adopt()
    {
        var decision = GlobalSalesManagerRules.DecideCreateWritePlan(
            ["LegacyAdoptable", "LegacyAdoptable", "LegacyAdoptable"]);
        Assert.Equal(GlobalCreateWritePlan.AdoptOrCreate, decision);
    }

    [Fact]
    public void All_NotFound_Allows_Create()
    {
        var decision = GlobalSalesManagerRules.DecideCreateWritePlan(
            ["NotFound", "NotFound"]);
        Assert.Equal(GlobalCreateWritePlan.AdoptOrCreate, decision);
    }

    [Fact]
    public void Mix_NotFound_And_LegacyAdoptable_Allows_Reconciliation()
    {
        var decision = GlobalSalesManagerRules.DecideCreateWritePlan(
            ["NotFound", "LegacyAdoptable", "NotFound"]);
        Assert.Equal(GlobalCreateWritePlan.AdoptOrCreate, decision);
    }

    [Fact]
    public void Mix_ExistingSame_And_Legacy_Allows_Reconciliation()
    {
        var decision = GlobalSalesManagerRules.DecideCreateWritePlan(
            ["ExistingSameGlobalAccount", "LegacyAdoptable", "NotFound"]);
        Assert.Equal(GlobalCreateWritePlan.AdoptOrCreate, decision);
    }

    [Fact]
    public void Any_LegacyConflict_Blocks()
    {
        var decision = GlobalSalesManagerRules.DecideCreateWritePlan(
            ["LegacyAdoptable", "LegacyConflict", "NotFound"]);
        Assert.Equal(GlobalCreateWritePlan.Conflict, decision);
    }

    [Fact]
    public void UsernameConflict_Blocks()
    {
        var decision = GlobalSalesManagerRules.DecideCreateWritePlan(
            ["NotFound", "UsernameConflict"]);
        Assert.Equal(GlobalCreateWritePlan.Conflict, decision);
    }

    [Fact]
    public void DuplicateAmbiguous_Blocks()
    {
        var decision = GlobalSalesManagerRules.DecideCreateWritePlan(
            ["DuplicateAmbiguous"]);
        Assert.Equal(GlobalCreateWritePlan.Conflict, decision);
    }

    [Fact]
    public void Two_Different_Existing_Globals_Detected_By_Statuses_Plus_Ids()
    {
        var g1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var g2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Assert.True(GlobalSalesManagerRules.HasConflictingGlobalIds([g1, g2, null]));
        Assert.False(GlobalSalesManagerRules.HasConflictingGlobalIds([g1, g1, null]));
    }
}
