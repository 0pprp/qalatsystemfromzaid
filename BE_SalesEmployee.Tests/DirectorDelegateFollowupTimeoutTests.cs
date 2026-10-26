using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Regression: gateway CancelAfter(8s) aborted نجف lists and surfaced as HTTP 500.
/// </summary>
public sealed class DirectorDelegateFollowupTimeoutTests
{
    [Theory]
    [InlineData("sales-manager/director-delegate-followup/lists")]
    [InlineData("sales-manager/director-delegate-followup/lists/12/summary")]
    [InlineData("sales-manager/director-delegate-followup/lists/12/customers")]
    [InlineData("sales-manager/director-delegate-followup/lists/12/board-operator-rating")]
    public void Followup_paths_use_long_branch_timeout(string path)
    {
        Assert.True(
            SalesManagerBranchAggregator.NeedsLongBranchTimeout(path),
            $"Expected long timeout for {path}");
    }

    [Fact]
    public void Short_catalog_path_stays_on_default_timeout()
    {
        Assert.False(SalesManagerBranchAggregator.NeedsLongBranchTimeout("sales-manager/stats/catalog"));
    }
}
