using System.Reflection;
using System.Security.Claims;
using BE_SalesEmployee.Controllers;
using BE_SalesEmployee.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>الإعلانات: المدير + مسؤول المراقبين only — never لجان المراقبة or other roles.</summary>
public sealed class AnnouncementsAccessTests
{
    [Fact]
    public void Announcements_controller_uses_director_or_supervisor_policy()
    {
        var attrs = typeof(SalesManagerAnnouncementsController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();
        Assert.Single(attrs);
        Assert.Equal(SalesPolicies.DirectorOrSupervisor, attrs[0].Policy);
    }

    [Theory]
    [InlineData("المدير", true)]
    [InlineData("مسؤول المراقبين", true)]
    [InlineData("المراقب", true)]
    [InlineData("مسؤول لجنة المراقبة", false)]
    [InlineData("مساعد مسؤول لجنة المراقبة", false)]
    [InlineData("مدير مبيعات", false)]
    [InlineData("محاسب رئيسي", false)]
    public async Task Director_or_supervisor_policy_roles(string userType, bool allowed)
    {
        // Same requirement Program.cs registers for SalesPolicies.DirectorOrSupervisor.
        var requirement = new SalesRoleRequirement(SalesRoles.Director, SalesRoles.Monitor);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("UserType", userType)], "test"));
        var ctx = new AuthorizationHandlerContext([requirement], user, null);
        await new SalesRoleHandler().HandleAsync(ctx);
        Assert.Equal(allowed, ctx.HasSucceeded);
    }
}
