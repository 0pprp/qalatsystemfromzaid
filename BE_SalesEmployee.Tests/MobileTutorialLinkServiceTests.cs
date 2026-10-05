using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class MobileTutorialLinkServiceTests
{
    [Fact]
    public async Task List_SeedsKnownApps_WhenEmpty()
    {
        var svc = new MobileTutorialLinkService(new InMemoryMobileTutorialLinkStore());
        var rows = await svc.ListForManagerAsync(CancellationToken.None);
        Assert.Contains(rows, r => r.AppKey == "delegate" && r.TitleAr.Contains("مندوب"));
        Assert.Contains(rows, r => r.AppKey == "follower");
    }

    [Fact]
    public async Task Upsert_ThenPublicGet_RequiresEnabledUrl()
    {
        var store = new InMemoryMobileTutorialLinkStore();
        var svc = new MobileTutorialLinkService(store);

        var disabled = await svc.UpsertAsync("delegate", "تطبيق المندوب", "https://youtu.be/abc", false, "mgr", CancellationToken.None);
        Assert.True(disabled.Ok);
        Assert.Null(await svc.GetPublicAsync("delegate", CancellationToken.None));

        var enabled = await svc.UpsertAsync("delegate", "تطبيق المندوب", "https://youtu.be/abc", true, "mgr", CancellationToken.None);
        Assert.True(enabled.Ok);
        var pub = await svc.GetPublicAsync("delegate", CancellationToken.None);
        Assert.NotNull(pub);
        Assert.Equal("https://youtu.be/abc", pub!.VideoUrl);
    }

    [Fact]
    public async Task Upsert_RejectsBadUrl()
    {
        var svc = new MobileTutorialLinkService(new InMemoryMobileTutorialLinkStore());
        var bad = await svc.UpsertAsync("delegate", "مندوب", "not-a-url", true, "mgr", CancellationToken.None);
        Assert.False(bad.Ok);
        Assert.Contains("http", bad.Error!);
    }
}
