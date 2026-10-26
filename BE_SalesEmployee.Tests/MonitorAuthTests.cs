using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class MonitorAuthTests
{
    [Fact]
    public void Authenticates_from_MonitorAccount_section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MonitorAccount:UserName"] = "مراقب1",
                ["MonitorAccount:Password"] = "pass1",
                ["MonitorAccount:DisplayName"] = "المراقب",
            })
            .Build();

        var svc = new MonitorAccountService(config);
        Assert.True(svc.TryAuthenticate("مراقب1", "pass1", out var id));
        Assert.Equal("مراقب1", id!.UserName);
        Assert.Equal("المراقب", id.DisplayName);
        Assert.False(svc.TryAuthenticate("مراقب1", "wrong", out _));
    }

    [Fact]
    public void Token_carries_monitor_user_type()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough-123456"
            })
            .Build();
        var tokens = new TokenService(config);
        var jwt = tokens.CreateMonitorToken("المراقب", out _);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(jwt);
        Assert.Equal("المراقب", parsed.Claims.First(c => c.Type == "UserType").Value);
        Assert.Equal("true", parsed.Claims.First(c => c.Type == TokenService.CentralClaim).Value);
    }

    [Fact]
    public void Module_role_maps_monitor()
    {
        Assert.Equal(
            BE_SalesEmployee.Sales.Authorization.SalesRoles.Monitor,
            BE_SalesEmployee.Sales.Authorization.SalesRoles.ToModuleRole("المراقب"));
    }

    [Fact]
    public void SalesManagerOrDirector_policy_roles_include_monitor()
    {
        var requirement = new BE_SalesEmployee.Sales.Authorization.SalesRoleRequirement(
            BE_SalesEmployee.Sales.Authorization.SalesRoles.SalesManager,
            BE_SalesEmployee.Sales.Authorization.SalesRoles.Director,
            BE_SalesEmployee.Sales.Authorization.SalesRoles.Monitor);

        Assert.Contains(
            BE_SalesEmployee.Sales.Authorization.SalesRoles.Monitor,
            requirement.ModuleRoles);
        Assert.Equal(
            BE_SalesEmployee.Sales.Authorization.SalesRoles.Monitor,
            BE_SalesEmployee.Sales.Authorization.SalesRoles.ToModuleRole("المراقب"));
    }
}
