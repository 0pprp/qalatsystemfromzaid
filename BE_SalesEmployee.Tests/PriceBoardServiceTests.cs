using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class PriceBoardServiceTests
{
    [Fact]
    public async Task Create_and_list_by_city()
    {
        var svc = new PriceBoardService(new InMemoryPriceBoardStore());
        var (ok, err, row) = await svc.CreateAsync(
            "ايفون", 1_000_000, 25_000, 100_000, "najaf", "النجف", "المراقب", default);
        Assert.True(ok);
        Assert.Null(err);
        Assert.NotNull(row);

        await svc.CreateAsync("سامسونج", 800_000, 20_000, 50_000, "basra", "البصرة", "المراقب", default);

        var najaf = await svc.ListAsync("najaf", default);
        Assert.Single(najaf);
        Assert.Equal("ايفون", najaf[0].ProductName);

        var all = await svc.ListAsync(null, default);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Validate_requires_product_and_city()
    {
        Assert.Equal("اسم المنتج مطلوب", PriceBoardService.Validate(" ", 1, 1, 1, "najaf"));
        Assert.Equal("محافظة الهدف مطلوبة", PriceBoardService.Validate("x", 1, 1, 1, " "));
        Assert.Null(PriceBoardService.Validate("x", 1, 1, 1, "najaf"));
    }

    [Fact]
    public async Task Update_and_delete()
    {
        var svc = new PriceBoardService(new InMemoryPriceBoardStore());
        var (_, _, row) = await svc.CreateAsync("A", 10, 1, 2, "c1", "C1", "m", default);
        var (ok, _, updated) = await svc.UpdateAsync(row!.Id, "B", 20, 2, 3, "c1", "C1", "m2", default);
        Assert.True(ok);
        Assert.Equal("B", updated!.ProductName);

        var (delOk, _) = await svc.DeleteAsync(row.Id, default);
        Assert.True(delOk);
        Assert.Empty(await svc.ListAsync(null, default));
    }
}
