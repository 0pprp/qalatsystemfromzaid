using System.Text.RegularExpressions;
using Xunit;

namespace BE_Company.Tests;

/// <summary>
/// Contract: CustomersPayments_Create must not resolve identity via global TOP 1 / latest id.
/// </summary>
public sealed class CustomersPaymentsCreateOutputContractTests
{
    private static string ReadSql(string relative)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relative));
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

    private static string StripSqlComments(string sql)
    {
        sql = Regex.Replace(sql, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        sql = Regex.Replace(sql, @"--.*?$", " ", RegexOptions.Multiline);
        return sql;
    }

    [Theory]
    [InlineData("DatabaseScripts/StoredProcedures/dbo.CustomersPayments_Create.sql")]
    [InlineData("DatabaseScripts/Migrations/20261004_CustomersPayments_Create_OutputInserted.sql")]
    public void CustomersPayments_Create_UsesOutputInserted_NotGlobalLatestId(string relativePath)
    {
        var sql = StripSqlComments(ReadSql(relativePath));
        Assert.Contains("OUTPUT INSERTED.CustomerPaymentID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(
            new Regex(@"TOP\s+1\s+.*ORDER\s+BY\s+CustomerPaymentID\s+DESC", RegexOptions.IgnoreCase | RegexOptions.Singleline),
            sql);
    }
}
