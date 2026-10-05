using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class WaiverApplyRulesTests
{
    [Fact]
    public void WaiverId_MustBeNonEmpty()
    {
        Assert.False(WaiverApplyRules.IsValidWaiverId(null));
        Assert.False(WaiverApplyRules.IsValidWaiverId(Guid.Empty));
        Assert.True(WaiverApplyRules.IsValidWaiverId(Guid.NewGuid()));
    }

    [Fact]
    public void ClaimSql_CreatesDurableIdempotencyTable()
    {
        Assert.Contains("SalesWaiverApplications", WaiverApplyRules.ClaimSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY", WaiverApplyRules.ClaimSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WaiverId", WaiverApplyRules.TryClaimSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UniqueKeyViolation_RejectsNonSqlExceptions()
    {
        Assert.False(WaiverApplyRules.IsUniqueKeyViolation(new InvalidOperationException("x")));
        Assert.False(WaiverApplyRules.IsUniqueKeyViolation(new Exception("outer", new ArgumentException("inner"))));
    }
}
