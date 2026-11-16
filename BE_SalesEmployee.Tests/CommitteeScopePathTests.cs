using BE_SalesEmployee.Middleware;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CommitteeScopePathTests
{
    [Theory]
    [InlineData("/api/director/delegate-followup/lists")]
    [InlineData("/api/director/sales-followup/employees")]
    [InlineData("/api/director/followers/ops/summaries")]
    [InlineData("/api/director/followers/ops/12/work-bundle")]
    [InlineData("/api/director/cities")]
    public void Committee_boards_are_allowed(string path) =>
        Assert.True(CommitteeScopeMiddleware.IsAllowedPath(path));

    [Theory]
    [InlineData("/api/director/price-board")]
    [InlineData("/api/director/price-board/5")]
    [InlineData("/api/director/followers/ops/12/collection")]
    [InlineData("/api/director/followers/ops/lists/3/collection-enabled")]
    [InlineData("/api/director/oversight/accounts")]
    public void Price_board_and_collection_are_blocked(string path) =>
        Assert.False(CommitteeScopeMiddleware.IsAllowedPath(path));
}
