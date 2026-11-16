using BE_DelegateWebApplication.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

/// <summary>
/// Follower archive requests against a real SQL Server scratch database.
/// Runs only when ARCHIVE_FLOW_SQL is set (master-level connection string).
/// </summary>
public sealed class FollowerArchiveRequestsSqlIntegrationTests
{
    private const string Db = "ArchiveFollowerIT";

    [Fact]
    public async Task AnyCustomerOnTheLists_NoteMandatoryWhenNotFinished()
    {
        var master = Environment.GetEnvironmentVariable("ARCHIVE_FLOW_SQL");
        if (string.IsNullOrWhiteSpace(master))
        {
            return;
        }

        var cs = await ResetAsync(master);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DataBaseConnection"] = cs,
        }).Build();
        var svc = new FollowerArchiveRequestsService(config);

        // Finished-only list keeps its old meaning; the full list adds the unfinished one with its remainder.
        var finished = await svc.ListFinishedAsync(7, null, default);
        Assert.Equal([1], finished.Select(c => c.CustomerId).ToArray());
        var all = await svc.ListFinishedAsync(7, null, default, onlyFinished: false);
        Assert.Equal([1, 2], all.Select(c => c.CustomerId).ToArray());
        var open = all.Single(c => c.CustomerId == 2);
        Assert.False(open.IsFinished);
        Assert.Equal(150000m, open.Remaining);
        Assert.True(all.Single(c => c.CustomerId == 1).IsFinished);

        // Customer 3 is on another follower's list.
        var foreign = await Assert.ThrowsAsync<FollowerArchiveException>(() => svc.CreateAsync(7, "متابع", 3, "سبب", default));
        Assert.Equal(403, foreign.StatusCode);

        // Unfinished needs a note; finished does not.
        var noNote = await Assert.ThrowsAsync<FollowerArchiveException>(() => svc.CreateAsync(7, "متابع", 2, " ", default));
        Assert.Equal(400, noNote.StatusCode);
        var created = await svc.CreateAsync(7, "متابع", 2, "الزبون يريد تسوية", default);
        Assert.Equal("الزبون يريد تسوية", created.FollowerNote);
        var plain = await svc.CreateAsync(7, "متابع", 1, null, default);
        Assert.Null(plain.FollowerNote);

        var dup = await Assert.ThrowsAsync<FollowerArchiveException>(() => svc.CreateAsync(7, "متابع", 1, null, default));
        Assert.Equal(409, dup.StatusCode);

        // Middle chain statuses look like "Requested" to the follower app.
        await using (var c = new SqlConnection(cs))
        {
            await c.ExecuteAsync("UPDATE dbo.FollowerArchiveRequests SET Status = N'WithCommittee' WHERE CustomerId = 2");
        }

        var mine = await svc.ListMineAsync(7, default);
        Assert.Equal(2, mine.Count);
        Assert.All(mine, r => Assert.Equal("Requested", r.Status));
        Assert.Equal("الزبون يريد تسوية", mine.Single(r => r.CustomerId == 2).FollowerNote);
    }

    private static async Task<string> ResetAsync(string master)
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

        SqlConnection.ClearAllPools();
        var cs = new SqlConnectionStringBuilder(master) { InitialCatalog = Db }.ConnectionString;
        await using var db = new SqlConnection(cs);
        await db.ExecuteAsync(@"
CREATE TABLE dbo.Delegates (DelegateID INT PRIMARY KEY, DelegateName NVARCHAR(200));
CREATE TABLE dbo.Customers (CustomerID INT PRIMARY KEY, CustomerName NVARCHAR(200), PhoneNumber NVARCHAR(50), DelegateID INT, CustomerState INT NULL);
CREATE TABLE dbo.FollowerUserLists (UserId INT, ListId INT);
CREATE TABLE dbo.View_CustomersSalesDelegate (CustomerID INT, AmountTotalSalesDenar DECIMAL(18,0));
CREATE TABLE dbo.View_CustomersPaymentsDelegate (CustomerID INT, PaymentDate DATETIME, AmountDenar DECIMAL(18,0));
INSERT INTO dbo.Delegates VALUES (10, N'قائمتي'), (20, N'قائمة غيري');
INSERT INTO dbo.FollowerUserLists VALUES (7, 10), (8, 20);
INSERT INTO dbo.Customers VALUES (1, N'منتهٍ', N'0770', 10, 1), (2, N'عليه باقي', N'0771', 10, 1), (3, N'غريب', N'0772', 20, 1);
INSERT INTO dbo.View_CustomersSalesDelegate VALUES (1, 300000), (2, 400000), (3, 100000);
INSERT INTO dbo.View_CustomersPaymentsDelegate VALUES (1, '2026-11-01', 300000), (2, '2026-11-02', 250000);");
        return cs;
    }
}
