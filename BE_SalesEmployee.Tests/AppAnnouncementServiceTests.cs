using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class AppAnnouncementServiceTests
{
    private static AppAnnouncementService CreateSvc()
    {
        var env = new FakeEnv();
        var images = new AppAnnouncementImageStorage(env, NullLogger<AppAnnouncementImageStorage>.Instance);
        return new AppAnnouncementService(new InMemoryAppAnnouncementStore(), images);
    }

    [Fact]
    public async Task Create_Text_Then_Public_List()
    {
        var svc = CreateSvc();
        var (ok, err, row) = await svc.CreateAsync(
            "delegate", "text", "عرض خاص اليوم", null, null,
            false, null, true, "مدير", CancellationToken.None);
        Assert.True(ok, err);
        Assert.NotNull(row);

        await svc.CreateAsync(
            "delegate", "text", "مخفي", null, null,
            false, null, false, "مدير", CancellationToken.None);

        var pub = await svc.ListPublicByAppAsync("delegate", CancellationToken.None);
        Assert.Single(pub);
        Assert.Equal("عرض خاص اليوم", pub[0].TextBody);
    }

    [Fact]
    public async Task Create_Rejects_Text_Without_Body()
    {
        var svc = CreateSvc();
        var (ok, err, _) = await svc.CreateAsync(
            "follower", "text", "  ", null, null,
            false, null, true, null, CancellationToken.None);
        Assert.False(ok);
        Assert.Contains("نص", err);
    }

    [Fact]
    public async Task Create_Requires_Valid_Link_When_HasLink()
    {
        var svc = CreateSvc();
        var (ok, err, _) = await svc.CreateAsync(
            "delegate", "text", "إعلان", null, null,
            true, "not-a-url", true, null, CancellationToken.None);
        Assert.False(ok);
        Assert.Contains("http", err);
    }

    [Fact]
    public async Task Follower_Does_Not_See_Delegate_Ads()
    {
        var svc = CreateSvc();
        await svc.CreateAsync(
            "delegate", "text", "للمندوب", null, null,
            false, null, true, null, CancellationToken.None);
        var pub = await svc.ListPublicByAppAsync("follower", CancellationToken.None);
        Assert.Empty(pub);
    }

    [Fact]
    public async Task Create_Image_Requires_Banner_And_Zoom()
    {
        var svc = CreateSvc();
        var (okMissingZoom, errZoom, _) = await svc.CreateAsync(
            "delegate", "image", null, "banner.png", null,
            false, null, true, "مدير", CancellationToken.None);
        Assert.False(okMissingZoom);
        Assert.Contains("تكبير", errZoom);

        var (ok, err, row) = await svc.CreateAsync(
            "delegate", "image", null, "banner.png", "zoom.png",
            false, null, true, "مدير", CancellationToken.None);
        Assert.True(ok, err);
        Assert.Equal("banner.png", row!.ImageFileName);
        Assert.Equal("zoom.png", row.ZoomImageFileName);

        var pub = await svc.ListPublicByAppAsync("delegate", CancellationToken.None);
        Assert.Single(pub);
        Assert.Equal("zoom.png", pub[0].ZoomImageFileName);
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "ann-tests-" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
