using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class UserLoginDiagnosticsTests
{
    private static LoginCandidate Row(
        int id,
        string name,
        string password,
        string type,
        bool active) =>
        new()
        {
            UserId = id,
            UserName = name,
            Password = password,
            UserType = type,
            IsActive = active
        };

    [Fact]
    public void Active_MainAccountant_Correct_Password_LoginOk()
    {
        var rows = new[]
        {
            Row(1, "acc1", "secret", "محاسب رئيسي", true)
        };
        var (code, user) = UserLoginDiagnostics.Classify(rows, "secret", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.LoginOk, code);
        Assert.Equal(1, user!.UserId);
    }

    [Fact]
    public void Disabled_Only_Is_UserDisabled()
    {
        var rows = new[]
        {
            Row(1, "acc1", "secret", "محاسب رئيسي", false)
        };
        var (code, _) = UserLoginDiagnostics.Classify(rows, "secret", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.UserDisabled, code);
    }

    [Fact]
    public void Wrong_Password_Is_InvalidCredentials()
    {
        var rows = new[]
        {
            Row(1, "acc1", "secret", "محاسب رئيسي", true)
        };
        var (code, _) = UserLoginDiagnostics.Classify(rows, "nope", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.InvalidCredentials, code);
    }

    [Fact]
    public void SalesManager_On_Admin_Path_Is_WrongRole()
    {
        var rows = new[]
        {
            Row(1, "sm1", "secret", "مدير مبيعات", true)
        };
        var (code, _) = UserLoginDiagnostics.Classify(rows, "secret", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.WrongRole, code);
    }

    [Fact]
    public void SalesManager_On_Employee_Path_LoginOk()
    {
        var rows = new[]
        {
            Row(1, "sm1", "secret", "مدير مبيعات", true)
        };
        var (code, user) = UserLoginDiagnostics.Classify(rows, "secret", UserLoginDiagnostics.IsEmployeeLoginRole);
        Assert.Equal(LoginDiagnosticCode.LoginOk, code);
        Assert.NotNull(user);
    }

    [Fact]
    public void Two_Active_Same_Password_Is_DuplicateAmbiguous()
    {
        var rows = new[]
        {
            Row(1, "dup", "secret", "محاسب رئيسي", true),
            Row(2, "dup", "secret", "محاسب رئيسي", true),
        };
        var (code, _) = UserLoginDiagnostics.Classify(rows, "secret", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.DuplicateAmbiguous, code);
    }

    [Fact]
    public void Two_Active_Different_Passwords_Picks_Matching_Only()
    {
        var rows = new[]
        {
            Row(1, "dup", "a", "محاسب رئيسي", true),
            Row(2, "dup", "b", "محاسب رئيسي", true),
        };
        var (code, user) = UserLoginDiagnostics.Classify(rows, "b", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.LoginOk, code);
        Assert.Equal(2, user!.UserId);
    }

    [Fact]
    public void Disabled_Plus_Active_Prefers_Active()
    {
        var rows = new[]
        {
            Row(1, "acc", "old", "محاسب رئيسي", false),
            Row(2, "acc", "new", "محاسب رئيسي", true),
        };
        var (code, user) = UserLoginDiagnostics.Classify(rows, "new", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.LoginOk, code);
        Assert.Equal(2, user!.UserId);
    }

    [Fact]
    public void Empty_Rows_UserNotFound()
    {
        var (code, _) = UserLoginDiagnostics.Classify([], "x", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.UserNotFound, code);
    }

    /// <summary>
    /// Basra regression: active محاسب فرعي + active محاسب رئيسي sharing a trimmed username.
    /// Admin login must ignore the non-admin role when evaluating ambiguity/credentials.
    /// </summary>
    [Fact]
    public void Basra_Admin_Ignores_Active_SubAccountant_Same_Trimmed_Name()
    {
        var rows = new[]
        {
            Row(10, "كرار ", "shared-or-any", "محاسب فرعي", true),
            Row(11, "كرار", "main-secret", "محاسب رئيسي", true),
        };
        var (code, user) = UserLoginDiagnostics.Classify(
            rows, "main-secret", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.LoginOk, code);
        Assert.Equal(11, user!.UserId);
        Assert.Equal("محاسب رئيسي", user.UserType);
    }

    [Fact]
    public void Basra_Admin_Same_Password_Still_Succeeds_Because_SubAccountant_Not_Eligible()
    {
        var rows = new[]
        {
            Row(10, "كرار ", "same", "محاسب فرعي", true),
            Row(11, "كرار", "same", "محاسب رئيسي", true),
        };
        var (code, user) = UserLoginDiagnostics.Classify(
            rows, "same", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.LoginOk, code);
        Assert.Equal(11, user!.UserId);
    }

    [Fact]
    public void Basra_Employee_Uses_SubAccountant_Independently()
    {
        var rows = new[]
        {
            Row(10, "كرار ", "emp-secret", "محاسب فرعي", true),
            Row(11, "كرار", "main-secret", "محاسب رئيسي", true),
        };
        var (code, user) = UserLoginDiagnostics.Classify(
            rows, "emp-secret", UserLoginDiagnostics.IsEmployeeLoginRole);
        Assert.Equal(LoginDiagnosticCode.LoginOk, code);
        Assert.Equal(10, user!.UserId);
        Assert.Equal("محاسب فرعي", user.UserType);
    }

    [Fact]
    public void Two_Eligible_Active_Same_Password_Still_DuplicateAmbiguous()
    {
        var rows = new[]
        {
            Row(1, "dup", "secret", "محاسب رئيسي", true),
            Row(2, "dup", "secret", "مدير فرع", true),
            Row(3, "dup", "secret", "محاسب فرعي", true), // ignored on admin path
        };
        var (code, _) = UserLoginDiagnostics.Classify(rows, "secret", UserLoginDiagnostics.IsAdminLoginRole);
        Assert.Equal(LoginDiagnosticCode.DuplicateAmbiguous, code);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(1, true)]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData(false, false)]
    [InlineData(0, false)]
    [InlineData("false", false)]
    public void UserState_Parsing(object state, bool expected) =>
        Assert.Equal(expected, UserLoginDiagnostics.IsActiveUserState(state));
}
