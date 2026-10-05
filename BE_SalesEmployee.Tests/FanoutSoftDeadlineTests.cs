using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Guards ~1s first-paint for sales-manager list fan-out across many branches.
/// Slow/dead branches must not hold the response for the full BranchTimeout (8s).
/// </summary>
public sealed class FanoutSoftDeadlineTests
{
    [Fact]
    public void Fanout_soft_deadline_is_at_most_one_second()
    {
        Assert.True(
            FanoutSoftDeadline.SoftWait <= TimeSpan.FromSeconds(1),
            $"SoftWait {FanoutSoftDeadline.SoftWait.TotalMilliseconds}ms must be <= 1000ms for ~1s first paint.");
    }

    [Fact]
    public void Fanout_per_branch_timeout_is_stricter_than_single_branch()
    {
        Assert.True(
            FanoutSoftDeadline.MultiBranchTimeout < SalesManagerBranchAggregator.BranchTimeout,
            "Multi-branch list must use a tighter per-branch timeout than single-branch BranchTimeout.");
        Assert.True(
            FanoutSoftDeadline.MultiBranchTimeout >= FanoutSoftDeadline.SoftWait,
            "Per-branch timeout must outlive the soft wait so early finishers are collected.");
    }

    [Fact]
    public async Task Collect_returns_completed_chunks_at_soft_deadline_without_waiting_for_stragglers()
    {
        var fast = Task.FromResult(new[] { "a", "b" }.AsEnumerable());
        var slow = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            return new[] { "slow" }.AsEnumerable();
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var merged = await FanoutSoftDeadline.CollectAsync(
            [fast, slow],
            softWait: TimeSpan.FromMilliseconds(200),
            extendIfEmpty: TimeSpan.FromMilliseconds(50),
            hasData: chunk => chunk.Any(),
            ct: CancellationToken.None);
        sw.Stop();

        Assert.Contains("a", merged);
        Assert.Contains("b", merged);
        Assert.DoesNotContain("slow", merged);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"Collect took {sw.Elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Collect_extends_once_when_soft_deadline_yields_empty()
    {
        var late = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            return new[] { "late" }.AsEnumerable();
        });

        var merged = await FanoutSoftDeadline.CollectAsync(
            [late],
            softWait: TimeSpan.FromMilliseconds(50),
            extendIfEmpty: TimeSpan.FromMilliseconds(400),
            hasData: chunk => chunk.Any(),
            ct: CancellationToken.None);

        Assert.Contains("late", merged);
    }
}
