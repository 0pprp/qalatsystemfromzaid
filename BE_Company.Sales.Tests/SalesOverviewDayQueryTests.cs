using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class SalesOverviewDayQueryTests
{
    [Fact]
    public void TryNormalize_RejectsADayTheMonthDoesNotHave()
    {
        var now = new DateTime(2026, 11, 1);
        Assert.False(SalesOverviewDayQuery.TryNormalize(2026, 2, 30, now, out _));
        Assert.True(SalesOverviewDayQuery.TryNormalize(2026, 11, 1, now, out var date));
        Assert.Equal(new DateTime(2026, 11, 1), date);
    }
}
