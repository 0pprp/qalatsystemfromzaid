using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class GlobalAccountIdSchemaSqlTests
{
    [Fact]
    public void Migration_Script_Is_Idempotent_And_Nullable()
    {
        var sql = GlobalAccountIdSchemaSql.EnsureScript;
        Assert.Contains("COL_LENGTH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GlobalAccountId", sql, StringComparison.Ordinal);
        Assert.Contains("UNIQUEIDENTIFIER NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_Users_GlobalAccountId", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE GlobalAccountId IS NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UserState", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UserType", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("XACT_ABORT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Filtered_Index_Is_Unique_And_Allows_Multiple_Nulls()
    {
        Assert.True(GlobalAccountIdSchemaSql.UsesUniqueFilteredIndex);
        Assert.Contains("CREATE UNIQUE NONCLUSTERED INDEX", GlobalAccountIdSchemaSql.EnsureScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE GlobalAccountId IS NOT NULL", GlobalAccountIdSchemaSql.EnsureScript, StringComparison.OrdinalIgnoreCase);
    }
}
