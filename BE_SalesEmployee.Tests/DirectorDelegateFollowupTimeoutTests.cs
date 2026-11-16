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

    [Theory]
    [InlineData("sales-manager/sales-requests?page=1&from=2026-11-01")]
    [InlineData("sales-manager/sales-requests?page=1&to=2026-11-06")]
    [InlineData("sales-manager/sales-requests?page=1&sources=delegate")]
    [InlineData("sales-manager/sales-requests?page=1&sourceFilter=1&people=delegate%7C9%7C15%7C%D8%AD%D9%8A%D8%AF%D8%B1")]
    public void Director_board_filter_is_detected(string path)
    {
        Assert.True(SalesManagerBranchAggregator.HasDirectorBoardFilter(path));
    }

    [Fact]
    public void Unfiltered_board_is_not_a_director_filter()
    {
        Assert.False(SalesManagerBranchAggregator.HasDirectorBoardFilter("sales-manager/sales-requests?page=1&pageSize=40&tab=all"));
    }

    [Fact]
    public void Source_directory_waits_for_every_branch_on_the_short_timeout()
    {
        const string path = "sales-manager/source-directory";
        Assert.True(SalesManagerBranchAggregator.IsSourceDirectory(path));
        Assert.True(SalesManagerBranchAggregator.FanoutWaitsForEveryBranch(path));
        Assert.Equal(SalesManagerBranchAggregator.BranchTimeout, SalesManagerBranchAggregator.FanoutPerBranchTimeout(path));
        Assert.False(SalesManagerBranchAggregator.NeedsLongBranchTimeout(path));
    }

    [Fact]
    public void Short_catalog_path_stays_on_default_timeout()
    {
        Assert.False(SalesManagerBranchAggregator.NeedsLongBranchTimeout("sales-manager/stats/catalog"));
    }
}
