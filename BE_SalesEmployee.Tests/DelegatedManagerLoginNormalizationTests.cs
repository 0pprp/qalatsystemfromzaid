using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DelegatedManagerLoginNormalizationTests
{
    private static DelegatedManagerAccountService ServiceWith(string userName, string password) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DelegatedManagerAccount:UserName"] = userName,
                ["DelegatedManagerAccount:Password"] = password,
                ["DelegatedManagerAccount:DisplayName"] = "المدير المفوض",
            })
            .Build());

    [Theory]
    [InlineData("ايمن")]
    [InlineData("أيمن")]
    [InlineData("إيمن")]
    [InlineData("آيمن")]
    [InlineData("  أيمن  ")]
    public void ArabicAlefVariants_AuthenticateAgainstPlainAlefAccount(string typed)
    {
        var svc = ServiceWith("ايمن", "pass");
        Assert.True(svc.TryAuthenticate(typed, "pass", out var identity));
        Assert.NotNull(identity);
        Assert.Equal("ايمن", identity!.UserName);
    }

    [Fact]
    public void WrongPassword_StillRejected()
    {
        var svc = ServiceWith("ايمن", "pass");
        Assert.False(svc.TryAuthenticate("أيمن", "nope", out _));
    }
}
