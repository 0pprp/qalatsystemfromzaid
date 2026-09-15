using System.Security.Claims;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DelegatedManagerRoleTests
{
    [Fact]
    public void ArabicUserType_MapsToDelegatedManagerModuleRole()
    {
        Assert.Equal(SalesRoles.DelegatedManager, SalesRoles.ToModuleRole("مدير مفوض"));
        Assert.True(SalesRoles.IsDelegatedManager("مدير مفوض"));
    }

    [Fact]
    public void DelegatedManager_IsNotTreatedAsSalesManagerOrEmployee()
    {
        Assert.False(SalesRoles.IsSalesManager("مدير مفوض"));
        Assert.False(SalesRoles.IsSalesEmployee("مدير مفوض"));
        Assert.False(SalesRoles.IsAnySales("مدير مفوض"));
    }

    [Fact]
    public void SalesManagerUserType_IsNotDelegatedManager()
    {
        Assert.False(SalesRoles.IsDelegatedManager("مدير مبيعات"));
        Assert.Equal(SalesRoles.SalesManager, SalesRoles.ToModuleRole("مدير مبيعات"));
    }

    [Fact]
    public void UnknownUserType_MapsToNull()
    {
        Assert.Null(SalesRoles.ToModuleRole("مدير فرع"));
        Assert.Null(SalesRoles.ToModuleRole(null));
        Assert.Null(SalesRoles.ToModuleRole(""));
    }

    [Fact]
    public async Task DelegatedManagerPolicy_SucceedsOnlyForDelegatedManager()
    {
        Assert.True(await SucceedsAsync("مدير مفوض", SalesRoles.DelegatedManager));
        Assert.False(await SucceedsAsync("مدير مبيعات", SalesRoles.DelegatedManager));
        Assert.False(await SucceedsAsync("موظف مبيعات", SalesRoles.DelegatedManager));
    }

    [Fact]
    public async Task SalesManagerOrDelegatedManagerPolicy_AcceptsBothRoles()
    {
        Assert.True(await SucceedsAsync("مدير مفوض", SalesRoles.SalesManager, SalesRoles.DelegatedManager));
        Assert.True(await SucceedsAsync("مدير مبيعات", SalesRoles.SalesManager, SalesRoles.DelegatedManager));
        Assert.False(await SucceedsAsync("موظف مبيعات", SalesRoles.SalesManager, SalesRoles.DelegatedManager));
    }

    [Fact]
    public void FromPrincipal_FlagsDelegatedManagerAndCentral()
    {
        var principal = Principal("مدير مفوض", central: true);

        var user = TokenService.FromPrincipal(principal);

        Assert.True(user.IsDelegatedManager);
        Assert.True(user.IsCentral);
        Assert.Equal("مدير مفوض", user.UserType);
    }

    [Fact]
    public void DelegatedManagerToken_CarriesArabicUserTypeAndNoBranchToken()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough-123456"
            })
            .Build();
        var tokens = new TokenService(config);

        var jwt = tokens.CreateDelegatedManagerToken("المدير المفوض", out var expiration);

        Assert.False(string.IsNullOrWhiteSpace(jwt));
        Assert.True(expiration > DateTime.UtcNow);

        var parsed = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(jwt);
        Assert.Equal("مدير مفوض", parsed.Claims.First(c => c.Type == "UserType").Value);
        Assert.Equal("true", parsed.Claims.First(c => c.Type == TokenService.CentralClaim).Value);
        Assert.DoesNotContain(parsed.Claims, c => c.Type == "BranchToken");
    }

    private static async Task<bool> SucceedsAsync(string userType, params string[] allowedRoles)
    {
        var handler = new SalesRoleHandler();
        var requirement = new SalesRoleRequirement(allowedRoles);
        var context = new AuthorizationHandlerContext(
            new[] { requirement },
            Principal(userType, central: true),
            resource: null);

        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Principal(string userType, bool central)
    {
        var claims = new List<Claim>
        {
            new("UserID", "0"),
            new("UserName", "tester"),
            new("UserType", userType)
        };
        if (central)
        {
            claims.Add(new Claim(TokenService.CentralClaim, "true"));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
