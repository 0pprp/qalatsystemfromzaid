using BE_Company.Sales.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// End-to-end paper archive chain against a real SQL Server scratch database.
/// Runs only when ARCHIVE_FLOW_SQL is set to a master-level connection string
/// (e.g. "Server=.\SQLEXPRESS;Integrated Security=true;TrustServerCertificate=true").
/// </summary>
public sealed class ArchiveFlowSqlIntegrationTests
{
    private const string Db = "ArchiveFlowIT";

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
    public async Task FullChain_AgentCommitteeArchive_AndFollowerRequests()
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
        var root = Path.Combine(Path.GetTempPath(), "archive-flow-it");
        Directory.CreateDirectory(root);
        var archive = new SalesArchiveService(guard, new TempWebHostEnvironment(root), new FixedClock());

        // Legacy: papers already sent to the main accountant become the archive manager's inbox.
        var board = await archive.BoardAsync(default);
        var legacy = Assert.Single(board.AtArchive);
        Assert.Equal(1, legacy.EmployeeId);
        var legacyItems = (await archive.GetForAccountantAsync(legacy.BundleId, default))!.Items;
        Assert.Equal(2, legacyItems.Count(i => i.Status == "ToArchive"));
        Assert.Equal(1, legacyItems.Count(i => i.Status == "Approved"));

        // Agent 2's three completed sales were backfilled into a Ready bundle.
        var ready = Assert.Single(board.Ready);
        Assert.Equal(2, ready.EmployeeId);
        Assert.True(ready.CanSend);

        // Sending requires a committee recipient.
        var noRecipient = await Assert.ThrowsAsync<SalesArchiveException>(() =>
            archive.SendAsync(2, ready.BundleId, null, null, null, null, [], default));
        Assert.Equal(400, noRecipient.StatusCode);

        var sent = await archive.SendAsync(2, ready.BundleId, "علي", null, null, "مع الحزمة", [], default);
        Assert.Equal(SalesArchiveRules.PhaseInTransit, sent.Phase);
        Assert.Equal("علي", sent.RecipientName);
        Assert.All(sent.Items, i => Assert.Equal("ToCommittee", i.Status));
        var ids = sent.Items.Select(i => i.ArchiveId).ToArray();

        // Only the chosen committee member sees / acts on them.
        Assert.Empty(await archive.CommitteeItemsAsync("حسن", default));
        Assert.Equal(3, (await archive.CommitteeItemsAsync("علي", default)).Count);
        var notMine = await Assert.ThrowsAsync<SalesArchiveException>(() =>
            archive.CommitteeReceiveAsync("حسن", [ids[0]], true, null, default));
        Assert.Equal(409, notMine.StatusCode);

        // علي receives two and refuses one → that one goes back to the agent.
        await archive.CommitteeReceiveAsync("علي", [ids[0], ids[1]], true, null, default);
        await Assert.ThrowsAsync<SalesArchiveException>(() =>
            archive.CommitteeReceiveAsync("علي", [ids[2]], false, "", default));
        await archive.CommitteeReceiveAsync("علي", [ids[2]], false, "الورقة ناقصة", default);
        var agentView = (await archive.GetForEmployeeAsync(2, ready.BundleId, default))!;
        Assert.Equal(SalesArchiveRules.PhaseNeedsFix, agentView.Phase);
        Assert.True(agentView.CanSend);
        Assert.Equal("الورقة ناقصة", agentView.Items.Single(i => i.ArchiveId == ids[2]).RefusalNote);

        // The agent re-sends only the refused paper, this time to حسن.
        var resent = await archive.SendAsync(2, ready.BundleId, "حسن", null, null, null, [], default);
        Assert.Equal(SalesArchiveRules.PhaseInTransit, resent.Phase);
        Assert.Equal("حسن", resent.Items.Single(i => i.ArchiveId == ids[2]).CommitteeUser);
        Assert.Equal("WithCommittee", resent.Items.Single(i => i.ArchiveId == ids[0]).Status);

        // علي hands both to the archive manager; the bundle shows as AtArchive.
        await archive.CommitteeSendToArchiveAsync("علي", [ids[0], ids[1]], default);
        board = await archive.BoardAsync(default);
        Assert.Contains(board.AtArchive, b => b.BundleId == ready.BundleId);

        // Archive manager accepts one, refuses one → refused one returns to علي (not the agent).
        await archive.ArchiveDecideAsync("ارشيف", [ids[0]], true, null, default);
        await archive.ArchiveDecideAsync("ارشيف", [ids[1]], false, "غير مختوم", default);
        var mine = await archive.CommitteeItemsAsync("علي", default);
        var bounced = mine.Single(i => i.ArchiveId == ids[1]);
        Assert.Equal("ArchiveRefused", bounced.Status);
        Assert.Equal("غير مختوم", bounced.RefusalNote);
        Assert.Equal(SalesArchiveRules.PhaseInTransit, (await archive.GetForEmployeeAsync(2, ready.BundleId, default))!.Phase);

        // Deciding twice is a conflict.
        var twice = await Assert.ThrowsAsync<SalesArchiveException>(() =>
            archive.ArchiveDecideAsync("ارشيف", [ids[0]], true, null, default));
        Assert.Equal(409, twice.StatusCode);

        // علي returns it to the agent with a reason.
        await archive.CommitteeReturnToAgentAsync("علي", [ids[1]], "أعد الختم", default);
        agentView = (await archive.GetForEmployeeAsync(2, ready.BundleId, default))!;
        Assert.Equal("Refused", agentView.Items.Single(i => i.ArchiveId == ids[1]).Status);
        Assert.Equal(SalesArchiveRules.PhaseNeedsFix, agentView.Phase);

        // Legacy papers (no committee member) refused by the archive go straight back to the agent.
        var legacyPending = legacyItems.Where(i => i.Status == "ToArchive").Select(i => i.ArchiveId).ToArray();
        await archive.ArchiveDecideAsync("ارشيف", [legacyPending[0]], true, null, default);
        await archive.ArchiveDecideAsync("ارشيف", [legacyPending[1]], false, "ناقص", default);
        var legacyAfter = (await archive.GetForEmployeeAsync(1, legacy.BundleId, default))!;
        Assert.Equal("Refused", legacyAfter.Items.Single(i => i.ArchiveId == legacyPending[1]).Status);
        Assert.Equal(SalesArchiveRules.PhaseNeedsFix, legacyAfter.Phase);

        // —— Follower archive requests ——
        var requests = new FollowerArchiveRequestsService(config);
        await using (var c = new SqlConnection(cs))
        {
            await requests.BoardAsync(default); // creates/extends the table
            await c.ExecuteAsync(@"
INSERT INTO dbo.FollowerArchiveRequests (CustomerId, CustomerName, FollowerUserId, FollowerName, Status, FollowerNote)
VALUES (500, N'زبون عليه باقي', 7, N'متابع', N'Requested', N'يريد تسوية');");
        }

        var reqBoard = await requests.BoardAsync(default);
        var request = Assert.Single(reqBoard.Requested);
        Assert.Equal("يريد تسوية", request.FollowerNote);
        Assert.Equal(150000m, request.Remaining);
        var assigned = await requests.AssignAsync(request.Id, "علي", "ارشيف", null, default);
        Assert.Equal("ToCommittee", assigned.Status);
        Assert.Equal("علي", assigned.CommitteeUser);
        reqBoard = await requests.BoardAsync(default);
        Assert.Empty(reqBoard.Requested);
        Assert.Single(reqBoard.AtCommittee);

        await Assert.ThrowsAsync<ArchiveRequestException>(() => requests.CommitteeAcceptAsync("حسن", request.Id, default));
        await Assert.ThrowsAsync<ArchiveRequestException>(() => requests.CommitteeGiveAsync("علي", request.Id, default));
        Assert.Equal("WithCommittee", (await requests.CommitteeAcceptAsync("علي", request.Id, default)).Status);
        await Assert.ThrowsAsync<ArchiveRequestException>(() => requests.AssignAsync(request.Id, "حسن", "ارشيف", null, default));
        var given = await requests.CommitteeGiveAsync("علي", request.Id, default);
        Assert.Equal("Sent", given.Status);
        Assert.Equal("علي", given.SentByName);
        Assert.Single((await requests.BoardAsync(default)).Sent);
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
INSERT INTO dbo.Delegates VALUES (10, N'قائمة 10');
CREATE TABLE dbo.SalesDrafts (
    SaleId INT PRIMARY KEY, EmployeeId INT, UserName NVARCHAR(200), FullName NVARCHAR(200),
    FinalSalePrice DECIMAL(18,0), CompletedAt DATETIME NULL, CustomerListId INT NULL, Status NVARCHAR(30));
CREATE TABLE dbo.SalesDraftItems (SaleId INT, ProductName NVARCHAR(200), Quantity INT);
INSERT INTO dbo.SalesDrafts VALUES
    (201, 2, N'موظف 2', N'زبون أ', 100000, '2026-02-03T10:00:00', 10, N'Completed'),
    (202, 2, N'موظف 2', N'زبون ب', 200000, '2026-02-04T10:00:00', 10, N'Completed'),
    (203, 2, N'موظف 2', N'زبون ج', 300000, '2026-02-05T10:00:00', NULL, N'Completed');
CREATE TABLE dbo.View_CustomersSalesDelegate (CustomerID INT, AmountTotalSalesDenar DECIMAL(18,0));
CREATE TABLE dbo.View_CustomersPaymentsDelegate (CustomerID INT, PaymentDate DATETIME, AmountDenar DECIMAL(18,0));
INSERT INTO dbo.View_CustomersSalesDelegate VALUES (500, 400000);
INSERT INTO dbo.View_CustomersPaymentsDelegate VALUES (500, '2026-02-01', 250400);
INSERT INTO dbo.SalesDraftItems VALUES (201, N'ثلاجة', 1), (202, N'مكيف', 2), (203, N'غسالة', 1);

-- Pre-change archive schema with a bundle already sent to the main accountant.
CREATE TABLE dbo.SalesArchiveBundles (
    BundleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchiveBundles PRIMARY KEY,
    EmployeeId INT NOT NULL, EmployeeName NVARCHAR(200) NULL, BundleNumber INT NOT NULL,
    PeriodStart DATE NOT NULL, PeriodEnd DATE NOT NULL, IntermediateName NVARCHAR(200) NULL,
    IntermediatePhone NVARCHAR(20) NULL, Note NVARCHAR(1000) NULL, PhotoPath NVARCHAR(500) NULL,
    SentAt DATETIME NULL, ReviewedAt DATETIME NULL);
CREATE TABLE dbo.SalesArchiveItems (
    ArchiveId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchiveItems PRIMARY KEY,
    SaleId INT NOT NULL, BundleId INT NOT NULL, EmployeeId INT NOT NULL, CustomerName NVARCHAR(255) NOT NULL,
    SaleDate DATE NOT NULL, SalePrice DECIMAL(18, 0) NOT NULL, ItemName NVARCHAR(500) NOT NULL,
    ListId INT NULL, ListName NVARCHAR(255) NULL, Status NVARCHAR(20) NOT NULL, RefusalNote NVARCHAR(1000) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT (GETDATE()));
INSERT INTO dbo.SalesArchiveBundles (EmployeeId, EmployeeName, BundleNumber, PeriodStart, PeriodEnd, IntermediateName, IntermediatePhone, SentAt)
VALUES (1, N'موظف 1', 1, '2026-01-21', '2026-01-31', N'وسيط', N'07700000000', '2026-02-02');
INSERT INTO dbo.SalesArchiveItems (SaleId, BundleId, EmployeeId, CustomerName, SaleDate, SalePrice, ItemName, Status)
VALUES (101, 1, 1, N'قديم 1', '2026-01-22', 1000, N'صنف', N'Pending'),
       (102, 1, 1, N'قديم 2', '2026-01-23', 1000, N'صنف', N'Pending'),
       (103, 1, 1, N'قديم 3', '2026-01-24', 1000, N'صنف', N'Approved');
INSERT INTO dbo.SalesDrafts VALUES
    (101, 1, N'موظف 1', N'قديم 1', 1000, '2026-01-22', NULL, N'Completed'),
    (102, 1, N'موظف 1', N'قديم 2', 1000, '2026-01-23', NULL, N'Completed'),
    (103, 1, N'موظف 1', N'قديم 3', 1000, '2026-01-24', NULL, N'Completed');");
        return cs;
    }
}
