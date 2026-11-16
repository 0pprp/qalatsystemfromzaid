using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>api/branch-oversight — gateway key only; decisions attributed to a stable per-actor branch user.</summary>
public sealed class BranchOversightRulesTests
{
    [Theory]
    [InlineData(SalesGatewayKeyHandler.AuthSourceGateway, "مدير مبيعات", true)]
    [InlineData(SalesGatewayKeyHandler.AuthSourceGateway, "المراقب", true)]
    [InlineData(SalesGatewayKeyHandler.AuthSourceGateway, "موظف فلترة المبيعات", false)]
    [InlineData(null, "مدير فرع", false)]
    [InlineData(null, "محاسب رئيسي", false)]
    [InlineData("Jwt", "مدير مبيعات", false)]
    public void Only_trusted_gateway_callers(string? authSource, string userType, bool expected) =>
        Assert.Equal(expected, BranchOversightController.IsTrustedGatewayCaller(authSource, userType));

    [Fact]
    public void Oversight_email_is_stable_per_gateway_user()
    {
        var a = BranchOversightController.OversightEmail("42", "حيدر", "المدير");
        Assert.Equal("oversight-42@oversight.local", a);
        Assert.Equal(a, BranchOversightController.OversightEmail("42", "اسم آخر", "المدير"));
    }

    [Fact]
    public void Oversight_email_falls_back_to_name_hash_without_id()
    {
        var a = BranchOversightController.OversightEmail("0", "حيدر", "المدير");
        var b = BranchOversightController.OversightEmail(null, "حيدر", "المدير");
        var c = BranchOversightController.OversightEmail(null, "علي", "مسؤول لجنة المراقبة");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.StartsWith("oversight-n", a);
        Assert.EndsWith(BranchOversightController.OversightEmailDomain, a);
    }
}
