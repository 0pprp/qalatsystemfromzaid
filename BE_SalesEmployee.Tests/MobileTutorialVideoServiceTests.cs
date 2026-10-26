using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class MobileTutorialVideoServiceTests
{
    [Fact]
    public async Task Create_Then_ListPublic_Returns_Enabled_Only()
    {
        var svc = new MobileTutorialVideoService(new InMemoryMobileTutorialVideoStore());

        var (ok, err, row) = await svc.CreateAsync(
            "delegate",
            "كيفية إرسال التسديدات",
            "https://youtube.com/watch?v=abc",
            true,
            "مدير",
            CancellationToken.None);
        Assert.True(ok, err);
        Assert.NotNull(row);

        await svc.CreateAsync(
            "delegate",
            "سؤال متوقف",
            "https://youtube.com/watch?v=off",
            false,
            "مدير",
            CancellationToken.None);

        var publicRows = await svc.ListPublicByAppAsync("delegate", CancellationToken.None);
        Assert.Single(publicRows);
        Assert.Equal("كيفية إرسال التسديدات", publicRows[0].QuestionAr);
    }

    [Fact]
    public async Task Create_Rejects_Missing_Question()
    {
        var svc = new MobileTutorialVideoService(new InMemoryMobileTutorialVideoStore());
        var (ok, err, _) = await svc.CreateAsync(
            "follower",
            "  ",
            "https://example.com/v",
            true,
            null,
            CancellationToken.None);
        Assert.False(ok);
        Assert.Contains("السؤال", err);
    }
}
