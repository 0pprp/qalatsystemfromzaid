using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class SecurityBootstrapAuthTests
{
    [Fact]
    public void Director_bootstrap_credentials_rejected_when_configured_password_differs()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DirectorAccount:UserName"] = "حيدر",
            ["DirectorAccount:Password"] = "configured-strong-password",
            ["DirectorAccount:DisplayName"] = "المدير",
        }).Build();

        var svc = new DirectorAccountService(config);
        Assert.True(svc.HasConfiguredAccounts());
        Assert.False(svc.TryAuthenticate("حيدر", "حيدر", out var identity));
        Assert.Null(identity);
        Assert.True(svc.TryAuthenticate("حيدر", "configured-strong-password", out identity));
        Assert.NotNull(identity);
    }

    [Fact]
    public void Director_fails_closed_when_no_passwords_configured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DirectorAccount:UserName"] = "حيدر",
            ["DirectorAccount:Password"] = "",
        }).Build();

        var svc = new DirectorAccountService(config);
        Assert.False(svc.HasConfiguredAccounts());
        Assert.False(svc.TryAuthenticate("حيدر", "حيدر", out _));
    }

    [Fact]
    public void SalesManager_hardcoded_amar1_rejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SalesManagerAccount:UserName"] = "salesmanager",
            ["SalesManagerAccount:Password"] = "real-password",
        }).Build();

        var svc = new SalesManagerAccountService(config);
        Assert.False(svc.TryAuthenticate("amar1", "amar1", out _));
        Assert.True(svc.TryAuthenticate("salesmanager", "real-password", out var id));
        Assert.Equal("salesmanager", id!.UserName);
    }

    [Fact]
    public void Jwt_key_required_fails_closed()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        Assert.Throws<InvalidOperationException>(() => TokenService.RequireJwtKey(empty));

        var shortKey = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "too-short",
        }).Build();
        Assert.Throws<InvalidOperationException>(() => TokenService.RequireJwtKey(shortKey));

        var ok = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "unit-test-signing-key-that-is-long-enough-123456",
        }).Build();
        var tokens = new TokenService(ok);
        var jwt = tokens.CreateDirectorToken("tester", out var exp);
        Assert.False(string.IsNullOrWhiteSpace(jwt));
        Assert.True(exp > DateTime.UtcNow);
    }
}
