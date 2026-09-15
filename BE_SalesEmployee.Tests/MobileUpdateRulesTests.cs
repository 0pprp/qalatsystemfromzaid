using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class MobileUpdateRulesTests
{
    [Fact]
    public void UpToDate_NeedsNoUpdate()
    {
        Assert.Equal(MobileUpdateKind.None, MobileUpdateRules.Evaluate(5, 5, 3, forceUpdate: false));
    }

    [Fact]
    public void AheadOfPublishedRelease_NeedsNoUpdate()
    {
        Assert.Equal(MobileUpdateKind.None, MobileUpdateRules.Evaluate(7, 5, 3, forceUpdate: true));
    }

    [Fact]
    public void BehindLatestButSupported_IsOptional()
    {
        Assert.Equal(MobileUpdateKind.Optional, MobileUpdateRules.Evaluate(4, 5, 3, forceUpdate: false));
    }

    [Fact]
    public void BelowMinimumSupported_IsMandatory()
    {
        Assert.Equal(MobileUpdateKind.Mandatory, MobileUpdateRules.Evaluate(2, 5, 3, forceUpdate: false));
    }

    [Fact]
    public void ForceUpdateFlag_MakesOtherwiseOptionalUpdateMandatory()
    {
        Assert.Equal(MobileUpdateKind.Mandatory, MobileUpdateRules.Evaluate(4, 5, 3, forceUpdate: true));
    }

    [Fact]
    public void ExactlyAtMinimum_IsOptionalNotMandatory()
    {
        Assert.Equal(MobileUpdateKind.Optional, MobileUpdateRules.Evaluate(3, 5, 3, forceUpdate: false));
    }

    [Fact]
    public async Task Service_ReturnsNullForUnknownAppKey()
    {
        var service = new MobileUpdateService(new InMemoryMobileUpdateStore());

        Assert.Null(await service.EvaluateAsync("no-such-app", 1));
        Assert.Null(await service.GetAsync("no-such-app"));
    }

    [Fact]
    public async Task Service_SeedsDelegatedManagerRelease()
    {
        var service = new MobileUpdateService(new InMemoryMobileUpdateStore());

        var release = await service.GetAsync(InMemoryMobileUpdateStore.DelegatedManagerAppKey);

        Assert.NotNull(release);
        Assert.Equal("1.0.0", release!.LatestVersionName);
        Assert.Equal(64, release.Sha256.Length);
    }

    [Fact]
    public async Task Service_SeedsDelegateRelease()
    {
        var service = new MobileUpdateService(new InMemoryMobileUpdateStore());

        var release = await service.GetAsync(InMemoryMobileUpdateStore.DelegateAppKey);

        Assert.NotNull(release);
        Assert.Equal("1.0.0", release!.LatestVersionName);
        Assert.Equal(1, release.LatestVersionCode);
        Assert.Equal(MobileUpdateKind.None, (await service.EvaluateAsync("delegate", 1))!.Kind);
    }

    [Fact]
    public async Task Service_EvaluatesAgainstStoredRelease()
    {
        var store = new InMemoryMobileUpdateStore();
        await store.UpsertAsync(new MobileAppRelease
        {
            AppKey = "delegated-manager",
            LatestVersionName = "1.4.0",
            LatestVersionCode = 14,
            MinimumSupportedVersionCode = 10,
            ForceUpdate = false,
            ApkUrl = "https://example.invalid/dm-1.4.0.apk",
            Sha256 = new string('a', 64)
        });
        var service = new MobileUpdateService(store);

        var stale = await service.EvaluateAsync("delegated-manager", 9);
        var behind = await service.EvaluateAsync("delegated-manager", 12);
        var current = await service.EvaluateAsync("delegated-manager", 14);

        Assert.Equal(MobileUpdateKind.Mandatory, stale!.Kind);
        Assert.True(stale.Mandatory);
        Assert.Equal(MobileUpdateKind.Optional, behind!.Kind);
        Assert.True(behind.UpdateAvailable);
        Assert.False(behind.Mandatory);
        Assert.Equal(MobileUpdateKind.None, current!.Kind);
        Assert.False(current.UpdateAvailable);
    }

    [Fact]
    public async Task Store_LooksUpAppKeyCaseInsensitivelyAndTrimsWhitespace()
    {
        var service = new MobileUpdateService(new InMemoryMobileUpdateStore());

        Assert.NotNull(await service.GetAsync(" Delegated-Manager "));
        Assert.Null(await service.GetAsync("   "));
    }
}
