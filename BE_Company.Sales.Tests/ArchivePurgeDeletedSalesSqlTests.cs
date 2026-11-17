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

        // Agent 3's unlinked sales: only confident "branch sale deleted" matches are dropped,
        // and the backfill never adds them in the first place.
        for (var load = 0; load < 2; load++)
        {
            var agent3 = Assert.Single(await archive.ListForEmployeeAsync(3, default));
            var kept = (await archive.GetForEmployeeAsync(3, agent3.BundleId, default))!.Items
                .Select(i => i.SaleId).OrderBy(id => id).ToArray();
            Assert.Equal(new[] { 402, 403, 404, 405, 406 }, kept); // 404: CustomerId rule is on hold
        }

        // A paper already in a bundle is removed once its matched branch sale is deleted.
        await using (var c = new SqlConnection(cs))
        {
            await c.ExecuteAsync("DELETE FROM dbo.CustomersSales WHERE CustomerSaleID = 9402;");
        }

        var agent3After = Assert.Single(await archive.ListForEmployeeAsync(3, default));
        Assert.Equal(4, agent3After.ItemCount);
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
    (304, 2, N'موظف 2', N'زبون د', 400000, '2026-02-06T10:00:00', NULL, N'Completed', 9004);
ALTER TABLE dbo.SalesDrafts ADD Phone NVARCHAR(50) NULL, CustomerId INT NULL, PostingStatus NVARCHAR(30) NULL;
CREATE TABLE dbo.Customers (
    CustomerID INT PRIMARY KEY, CustomerName NVARCHAR(200), PhoneNumber NVARCHAR(100),
    SaleName NVARCHAR(100), CreatedDate DATETIME NULL);");
        await db.ExecuteAsync(@"
UPDATE dbo.SalesDrafts SET PostingStatus = N'Posted';
-- Agent 3: early sales posted before CustomerSaleId / CustomerId were linked on the draft.
INSERT INTO dbo.SalesDrafts (SaleId, EmployeeId, UserName, FullName, FinalSalePrice, CompletedAt, Status, Phone, CustomerId, PostingStatus) VALUES
    (401, 3, N'dhiaa', N'حذف في الفرع', 5450000, '2026-02-04T14:14:52.080', N'Completed', N'07700000401', NULL, N'Posted'),
    (402, 3, N'dhiaa', N'ما زال موجود', 1000000, '2026-02-05T10:00:00', N'Completed', N'07700000402', NULL, N'Posted'),
    (403, 3, N'dhiaa', N'تطابق مزدوج', 1000000, '2026-02-06T10:00:00', N'Completed', N'07700000403', NULL, N'Posted'),
    (404, 3, N'dhiaa', N'رقم العميل فقط', 1000000, '2026-02-07T10:00:00', N'Completed', N'07700000404', 7404, N'Posted'),
    (405, 3, N'dhiaa', N'لم يرحّل بعد', 1000000, '2026-02-08T10:00:00', N'Completed', N'07700000405', NULL, NULL),
    (406, 3, N'dhiaa', N'عميل قديم بعيد', 1000000, '2026-02-09T10:00:00', N'Completed', N'07700000406', NULL, N'Posted');
INSERT INTO dbo.Customers VALUES
    (7401, N'حذف في الفرع', N'07700000401', N'dhiaa', '2026-02-04T14:14:51.967'),  -- sale deleted
    (7402, N'ما زال موجود', N'07700000402', N'dhiaa', '2026-02-05T10:00:00'),       -- still sold
    (7403, N'تطابق 1', N'07700000403', N'dhiaa', '2026-02-06T10:00:01'),             -- two matches, no sales
    (7413, N'تطابق 2', N'07700000403', N'dhiaa', '2026-02-06T10:00:30'),
    (7404, N'رقم العميل فقط', N'07700000404', N'dhiaa', '2026-02-07T10:00:00'),     -- sale deleted
    (7406, N'عميل قديم بعيد', N'07700000406', N'dhiaa', '2026-01-01T10:00:00');     -- outside window
INSERT INTO dbo.CustomersSales VALUES (9402, 7402);");
        return cs;
    }
}
