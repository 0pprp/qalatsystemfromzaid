using BE_Company.Sales.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// Deleted / voided sales leave the paper archive. Runs only when ARCHIVE_FLOW_SQL is set
/// to a master-level connection string (see <see cref="ArchiveFlowSqlIntegrationTests"/>).
/// </summary>
public sealed class ArchivePurgeDeletedSalesSqlTests
{
    private const string Db = "ArchivePurgeIT";

    private sealed class FixedClock : IIraqClock
    {
        public DateTime UtcNow { get; } = new(2026, 2, 15, 9, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    [Fact]
    public async Task VoidedAndBranchDeletedSales_LeaveTheBundle_AndAreNotReAdded()
    {
        var master = Environment.GetEnvironmentVariable("ARCHIVE_FLOW_SQL");
        if (string.IsNullOrWhiteSpace(master))
        {
            return; // integration DB not configured
        }

        var cs = await ResetDatabaseAsync(master);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DataBaseConnection"] = cs,
            ["SalesManagement:RequireDemoDatabase"] = "false",
        }).Build();
        var guard = new SalesDevelopmentGuard(config, new TestHostEnvironment(), NullLogger<SalesDevelopmentGuard>.Instance);
        var root = Path.Combine(Path.GetTempPath(), "archive-purge-it");
        Directory.CreateDirectory(root);
        var archive = new SalesArchiveService(guard, new TempWebHostEnvironment(root), new FixedClock());

        // All four completed sales are backfilled into one bundle.
        var bundle = Assert.Single(await archive.ListForEmployeeAsync(2, default));
        Assert.Equal(4, bundle.ItemCount);
        await archive.SendAsync(2, bundle.BundleId, "علي", null, null, null, [], default);
        Assert.Equal(4, (await archive.CommitteeItemsAsync("علي", default)).Count);

        await using (var c = new SqlConnection(cs))
        {
            // 301 voided in the app, 302's branch sale deleted in the branch system, 303 draft removed.
            await c.ExecuteAsync(@"
UPDATE dbo.SalesDrafts SET Status = N'Voided' WHERE SaleId = 301;
DELETE FROM dbo.CustomersSales WHERE CustomerSaleID = 9002;
DELETE FROM dbo.SalesDrafts WHERE SaleId = 303;");
        }

        // The committee list purges too, even before any board load.
        var committee = await archive.CommitteeItemsAsync("علي", default);
        Assert.Equal(304, Assert.Single(committee).SaleId);

        // Repeated loads keep them out (backfill must not re-add the branch-deleted sale).
        var after = Assert.Single(await archive.ListForEmployeeAsync(2, default));
        Assert.Equal(1, after.ItemCount);
        after = Assert.Single(await archive.ListForEmployeeAsync(2, default));
        Assert.Equal(1, after.ItemCount);
        Assert.Equal("ToCommittee", (await archive.GetForEmployeeAsync(2, after.BundleId, default))!.Items.Single().Status);

        // When every sale of a bundle is gone, the bundle disappears.
        await using (var c = new SqlConnection(cs))
        {
            await c.ExecuteAsync("UPDATE dbo.SalesDrafts SET Status = N'Voided' WHERE SaleId = 304;");
        }

        Assert.Empty(await archive.ListForEmployeeAsync(2, default));
        Assert.Empty((await archive.BoardAsync(default)).AtCommittee);
    }

    private static async Task<string> ResetDatabaseAsync(string master)
    {
        await using (var c = new SqlConnection(master))
        {
            await c.ExecuteAsync($@"
IF DB_ID(N'{Db}') IS NOT NULL
BEGIN
    ALTER DATABASE [{Db}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{Db}];
END;
CREATE DATABASE [{Db}];");
        }

        var cs = new SqlConnectionStringBuilder(master) { InitialCatalog = Db }.ConnectionString;
        SqlConnection.ClearAllPools();
        await using var db = new SqlConnection(cs);
        await db.ExecuteAsync(@"
CREATE TABLE dbo.Users (UserID INT PRIMARY KEY, UserName NVARCHAR(200));
CREATE TABLE dbo.Delegates (DelegateID INT PRIMARY KEY, DelegateName NVARCHAR(200));
CREATE TABLE dbo.CustomersSales (CustomerSaleID INT PRIMARY KEY, CustomerID INT);
INSERT INTO dbo.CustomersSales VALUES (9001, 1), (9002, 2), (9004, 4);
CREATE TABLE dbo.SalesDrafts (
    SaleId INT PRIMARY KEY, EmployeeId INT, UserName NVARCHAR(200), FullName NVARCHAR(200),
    FinalSalePrice DECIMAL(18,0), CompletedAt DATETIME NULL, CustomerListId INT NULL, Status NVARCHAR(30),
    CustomerSaleId INT NULL);
CREATE TABLE dbo.SalesDraftItems (SaleId INT, ProductName NVARCHAR(200), Quantity INT);
INSERT INTO dbo.SalesDrafts VALUES
    (301, 2, N'موظف 2', N'زبون أ', 100000, '2026-02-03T10:00:00', NULL, N'Completed', 9001),
    (302, 2, N'موظف 2', N'زبون ب', 200000, '2026-02-04T10:00:00', NULL, N'Completed', 9002),
    (303, 2, N'موظف 2', N'زبون ج', 300000, '2026-02-05T10:00:00', NULL, N'Completed', NULL),
    (304, 2, N'موظف 2', N'زبون د', 400000, '2026-02-06T10:00:00', NULL, N'Completed', 9004);");
        return cs;
    }
}
