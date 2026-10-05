using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.MainFollower.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class MainFollowerShiftServiceTests
{
    private static MainFollowerShiftService Sut() =>
        new(new InMemoryMainFollowerShiftStore());

    [Fact]
    public async Task Start_then_location_then_end()
    {
        var svc = Sut();
        var shift = await svc.StartAsync("mf1", "متابع 1", 32.0, 44.3, default);
        Assert.True(shift.Id > 0);
        Assert.Null(shift.EndedAtUtc);

        var point = await svc.RecordLocationAsync("mf1", 32.01, 44.31, 12, DateTime.UtcNow, default);
        Assert.Equal(shift.Id, point.ShiftId);

        var ended = await svc.EndAsync("mf1", 32.02, 44.32, default);
        Assert.NotNull(ended.EndedAtUtc);
        Assert.Null(await svc.GetCurrentAsync("mf1", default));
    }

    [Fact]
    public async Task Location_without_shift_rejected()
    {
        var svc = Sut();
        var ex = await Assert.ThrowsAsync<CommitteeException>(() =>
            svc.RecordLocationAsync("mf1", 32, 44, null, null, default));
        Assert.Contains("دوام", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_snapshot_marks_active()
    {
        var store = new InMemoryMainFollowerShiftStore();
        var svc = new MainFollowerShiftService(store);
        await svc.StartAsync("mf1", "متابع 1", 32, 44, default);
        await svc.RecordLocationAsync("mf1", 32.1, 44.1, 5, DateTime.UtcNow, default);

        var live = await svc.LiveSnapshotAsync(
            [("mf1", "متابع 1"), ("mf2", "متابع 2")],
            default);
        Assert.Equal(2, live.Count);
    }
}
