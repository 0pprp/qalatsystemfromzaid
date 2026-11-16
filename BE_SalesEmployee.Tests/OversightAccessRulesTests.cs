using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class OversightAccessRulesTests
{
    [Theory]
    [InlineData("المدير", "المدير", true)]
    [InlineData("المدير", "مسؤول المراقبين", true)]
    [InlineData("المدير", "مسؤول لجنة المراقبة", true)]
    [InlineData("المدير", "مساعد مسؤول لجنة المراقبة", true)]
    [InlineData("مسؤول المراقبين", "مسؤول لجنة المراقبة", true)]
    [InlineData("مسؤول المراقبين", "مساعد مسؤول لجنة المراقبة", true)]
    [InlineData("المراقب", "مسؤول لجنة المراقبة", true)]
    [InlineData("مسؤول المراقبين", "المدير", false)]
    [InlineData("مسؤول المراقبين", "مسؤول المراقبين", false)]
    [InlineData("مسؤول لجنة المراقبة", "مساعد مسؤول لجنة المراقبة", false)]
    public void Create_rights(string actor, string target, bool allowed) =>
        Assert.Equal(allowed, OversightAccessRules.CanCreate(actor, target));

    [Fact]
    public void Arabic_username_and_short_password_are_accepted()
    {
        Assert.Null(OversightAccessRules.ValidateUserName("مهدي الساعدي"));
        Assert.Null(OversightAccessRules.ValidatePassword("1"));
        Assert.Null(OversightAccessRules.ValidatePassword("ا"));
        Assert.NotNull(OversightAccessRules.ValidatePassword("   "));
        Assert.NotNull(OversightAccessRules.ValidateUserName("  "));
    }

    [Fact]
    public async Task Store_keeps_arabic_username_and_committee_branches()
    {
        var store = new InMemoryOversightAccountStore();
        var committee = await store.CreateCommitteeAsync("لجنة النجف", ["najaf", "karbala"], "حيدر", default);
        var account = await store.CreateAccountAsync(
            "مساعد النجف", "مساعد النجف", "ا", OversightAccessRules.RoleCommitteeAssistant, committee.Id, "حيدر", default);
        var found = await store.FindByUserNameAsync("مساعد النجف", default);
        Assert.Equal(account.Id, found!.Id);
        Assert.Equal("ا", found.Password);
        Assert.Contains("najaf", found.CityValues);
        Assert.True(await store.UserNameExistsAsync("مساعد النجف", default));
    }
}
