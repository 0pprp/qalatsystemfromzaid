using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class AppCommitteeTaskServiceTests
{
    [Fact]
    public async Task Create_Then_Public_Returns_Enabled_Only()
    {
        var svc = new AppCommitteeTaskService(new InMemoryAppCommitteeTaskStore());
        var (ok, err, _) = await svc.CreateAsync("مهمة ظاهرة", true, "مدير", CancellationToken.None);
        Assert.True(ok, err);
        await svc.CreateAsync("مهمة مخفية", false, "مدير", CancellationToken.None);

        var pub = await svc.ListPublicAsync(CancellationToken.None);
        Assert.Single(pub);
        Assert.Equal("مهمة ظاهرة", pub[0].Body);
    }

    [Fact]
    public async Task Create_Rejects_Empty_Body()
    {
        var svc = new AppCommitteeTaskService(new InMemoryAppCommitteeTaskStore());
        var (ok, err, _) = await svc.CreateAsync("   ", true, null, CancellationToken.None);
        Assert.False(ok);
        Assert.Contains("نص", err);
    }
}
