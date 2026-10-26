using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class MainFollowerAuthTests
{
    [Fact]
    public void Authenticates_from_MainFollowerAccounts_section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MainFollowerAccounts:0:UserName"] = "mf1",
                ["MainFollowerAccounts:0:Password"] = "pass1",
                ["MainFollowerAccounts:0:DisplayName"] = "متابع رئيسي 1",
                ["MainFollowerAccounts:1:UserName"] = "mf2",
                ["MainFollowerAccounts:1:Password"] = "pass2",
                ["MainFollowerAccounts:1:DisplayName"] = "متابع رئيسي 2",
            })
            .Build();

        var svc = new MainFollowerAccountService(config);
        Assert.True(svc.TryAuthenticate("mf1", "pass1", out var id));
        Assert.Equal("mf1", id!.UserName);
        Assert.Equal("متابع رئيسي 1", id.DisplayName);

        Assert.False(svc.TryAuthenticate("mf1", "wrong", out _));
        Assert.False(svc.TryAuthenticate("unknown", "pass1", out _));
    }

    [Fact]
    public void Token_carries_main_follower_user_type()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough-123456"
            })
            .Build();
        var tokens = new TokenService(config);
        var jwt = tokens.CreateMainFollowerToken("متابع رئيسي 1", out _);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(jwt);
        Assert.Equal("متابع رئيسي", parsed.Claims.First(c => c.Type == "UserType").Value);
        Assert.Equal("true", parsed.Claims.First(c => c.Type == TokenService.CentralClaim).Value);
    }
}
