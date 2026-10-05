using System.Text.RegularExpressions;
using Xunit;

namespace BE_Company.Tests;

/// <summary>
/// Contract tests over the SQL source of truth for concurrent posting.
/// Guarantees we never regress to TOP 1 / MAX latest-payment identity.
/// </summary>
public sealed class CollectionPaymentAtomicPostingContractTests
{
    private static string ReadMigration()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(
            root,
            "DatabaseScripts",
            "Migrations",
            "20261004_CollectionPayment_AtomicPosting_NoLatestId.sql");
        Assert.True(File.Exists(path), $"Missing migration: {path}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BE_Company", "BE_Company.csproj")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repo root from test base directory.");
    }

    [Fact]
    public void Create_UsesOutputInserted_NotLatestGlobalId()
    {
        var sql = ReadMigration();
        Assert.Contains("OUTPUT INSERTED.CustomerPaymentID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@ClientPaymentId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@NewCustomerPaymentID", sql, StringComparison.OrdinalIgnoreCase);

        // Strip SQL comments before asserting anti-patterns are absent from executable text.
        var executable = Regex.Replace(sql, @"--.*?$", "", RegexOptions.Multiline);
        Assert.DoesNotMatch(
            new Regex(@"ORDER\s+BY\s+CustomerPaymentID\s+DESC", RegexOptions.IgnoreCase),
            executable);
        Assert.DoesNotMatch(
            new Regex(@"MAX\s*\(\s*CustomerPaymentID\s*\)", RegexOptions.IgnoreCase),
            executable);
    }

    [Fact]
    public void PostEligible_ClaimsRowAtomically_BeforeFinancialWrite()
    {
        var sql = ReadMigration();
        Assert.Contains("UPDLOCK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROWLOCK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PostedToBoxAtUtc IS NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@NewCustomerPaymentID = @NewPaymentID OUTPUT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UniqueIndexes_OnClientPaymentId_AreRequired()
    {
        var sql = ReadMigration();
        Assert.Contains("UX_CustomersPayments_ClientPaymentId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UX_CustomersPaymentsRequest_ClientPaymentId", sql, StringComparison.OrdinalIgnoreCase);
    }
}
