using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesRequestNameTransferTests
{
    private static SalesIdentity Manager() => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = "najaf-demo",
        BranchName = "النجف",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

    private static SalesIdentity Employee(int id, string name = "موظف", string branch = "najaf-demo", bool gateway = false) => new()
    {
        EmployeeId = id,
        EmployeeName = name,
        BranchId = branch,
        BranchName = "النجف",
        Role = SalesRoles.SalesEmployee,
        UserType = SalesRoles.UserTypeSalesEmployee,
        IsGateway = gateway
    };

    private static async Task<(FakeRequestRepository Repo, FakeManagerRead Employees, SalesRequestService Svc, SalesRequestDTO Row)> ReadyAsync(
        int ownerId = 1)
    {
        var repo = new FakeRequestRepository();
        var employees = new FakeManagerRead();
        employees.Employees.Clear();
        employees.Employees.AddRange(
        [
            new() { EmployeeId = 1, EmployeeName = "أحمد" },
            new() { EmployeeId = 2, EmployeeName = "علي" },
            new() { EmployeeId = 3, EmployeeName = "غير نشط" }
        ]);
        employees.ActiveEmployees =
        [
            new() { EmployeeId = 1, EmployeeName = "أحمد" },
            new() { EmployeeId = 2, EmployeeName = "علي" }
        ];
        var svc = new SalesRequestService(repo, new FakeClock(), employees);
        var created = await svc.CreateAsync(Manager(), new() { Customer = new() { FullName = "زبون", Phone = "07701234567", Address = "كوفة" } }, default);
        var assigned = await svc.AssignAsync(Manager(), created.Id, new SalesRequestAssignDTO { EmployeeId = ownerId, EmployeeName = "أحمد" }, default);
        assigned.FilterStatus = SalesFilterStatuses.ReadyForSale;
        assigned.CityValue = "najaf-demo";
        assigned.Notes = "ملاحظة المدير الأصلية";
        assigned.CustomerName = "زبون أصلي";
        return (repo, employees, svc, assigned);
    }

    [Fact]
    public async Task Transfer_Succeeds_Same_Branch()
    {
        var (repo, _, svc, row) = await ReadyAsync(1);
        var originalNotes = row.Notes;
        var originalName = row.CustomerName;

        var transferred = await svc.TransferNameAsync(
            Employee(1, "أحمد"),
            row.Id,
            new SalesRequestTransferDTO { ToEmployeeId = 2, TransferReason = "خارج المنطقة" },
            default);

        Assert.Equal(2, transferred.TargetEmployeeId);
        Assert.Equal("علي", transferred.TargetEmployeeName);
        Assert.Equal(SalesRequestStatuses.Assigned, transferred.Status);
        Assert.Equal(SalesFilterStatuses.ReadyForSale, transferred.FilterStatus);
        Assert.Equal(originalNotes, transferred.Notes);
        Assert.Equal(originalName, transferred.CustomerName);
        Assert.Single(repo.NameTransfers);
        Assert.Equal("خارج المنطقة", transferred.LatestNameTransfer!.TransferReason);
        Assert.Equal(1, transferred.LatestNameTransfer.FromEmployeeId);
        Assert.Equal(2, transferred.LatestNameTransfer.ToEmployeeId);

        var forOld = await svc.ListForEmployeeAsync(1, default);
        var forNew = await svc.ListForEmployeeAsync(2, default);
        Assert.DoesNotContain(forOld, r => r.Id == row.Id);
        Assert.Contains(forNew, r => r.Id == row.Id);
    }

    [Fact]
    public async Task Rejects_Self_Transfer()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(1), row.Id, new() { ToEmployeeId = 1, TransferReason = "x" }, default));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Rejects_Inactive_Peer()
    {
        var (_, employees, svc, row) = await ReadyAsync(1);
        employees.ActiveEmployees = [new() { EmployeeId = 1, EmployeeName = "أحمد" }];
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(1), row.Id, new() { ToEmployeeId = 2, TransferReason = "سبب" }, default));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Rejects_Empty_Reason()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(1), row.Id, new() { ToEmployeeId = 2, TransferReason = "  " }, default));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Rejects_Non_Owner()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(2, "علي"), row.Id, new() { ToEmployeeId = 1, TransferReason = "سبب" }, default));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Rejects_Other_City_Mismatch()
    {
        // Two different gateway short keys on a direct (non-trusted-gateway) actor → real mismatch.
        var (_, _, svc, row) = await ReadyAsync(1);
        row.CityValue = "baghdad-karkh";
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(1, "أحمد", "najaf-demo"), row.Id, new() { ToEmployeeId = 2, TransferReason = "سبب" }, default));
        Assert.Equal(403, ex.StatusCode);
        Assert.Contains("محافظة", ex.Message);
    }

    /// <summary>
    /// Reproduces Demo false-403: actor.BranchId is gateway short value while SalesRequests.CityValue
    /// is a legacy Arabic/display label for the same branch DB.
    /// </summary>
    [Fact]
    public async Task Allows_ShortBranchId_With_LegacyArabic_CityValue_SameBranchDb()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        row.CityValue = "النجف";
        var transferred = await svc.TransferNameAsync(
            Employee(1, "أحمد", "najaf-demo"),
            row.Id,
            new() { ToEmployeeId = 2, TransferReason = "خارج المنطقة" },
            default);
        Assert.Equal(2, transferred.TargetEmployeeId);
    }

    [Fact]
    public async Task Allows_ShortBranchId_With_LegacyCatalog_CityValue_SameBranchDb()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        row.CityValue = "Database_Najaf_DEMO";
        var transferred = await svc.TransferNameAsync(
            Employee(1, "أحمد", "najaf-demo"),
            row.Id,
            new() { ToEmployeeId = 2, TransferReason = "سبب" },
            default);
        Assert.Equal(2, transferred.TargetEmployeeId);
    }

    [Fact]
    public async Task TrustedGateway_Allows_LegacyCityValue_Mismatch()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        row.CityValue = "baghdad-karkh"; // even a different key: gateway already routed to this branch DB
        var transferred = await svc.TransferNameAsync(
            Employee(1, "أحمد", "najaf-demo", gateway: true),
            row.Id,
            new() { ToEmployeeId = 2, TransferReason = "سبب" },
            default);
        Assert.Equal(2, transferred.TargetEmployeeId);
    }

    [Fact]
    public async Task Rejects_Transfer_To_Peer_Outside_This_Branch_Employee_List()
    {
        // Cross-branch boundary for this host: peers come only from this branch DB Users table.
        var (_, employees, svc, row) = await ReadyAsync(1);
        employees.ActiveEmployees =
        [
            new() { EmployeeId = 1, EmployeeName = "أحمد" },
            new() { EmployeeId = 2, EmployeeName = "علي" }
        ];
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(1), row.Id, new() { ToEmployeeId = 999, TransferReason = "سبب" }, default));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Rejects_Completed_Status()
    {
        var (repo, _, svc, row) = await ReadyAsync(1);
        row.Status = SalesRequestStatuses.Completed;
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(1), row.Id, new() { ToEmployeeId = 2, TransferReason = "سبب" }, default));
        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(repo.NameTransfers);
    }

    [Fact]
    public async Task Rejects_Rejected_Status()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        row.Status = SalesRequestStatuses.Rejected;
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Employee(1), row.Id, new() { ToEmployeeId = 2, TransferReason = "سبب" }, default));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Allows_ConvertedToSale_And_Inspected()
    {
        var (repo, _, svc, row) = await ReadyAsync(1);
        row.Status = SalesRequestStatuses.ConvertedToSale;
        var first = await svc.TransferNameAsync(Employee(1), row.Id, new() { ToEmployeeId = 2, TransferReason = "حول" }, default);
        Assert.Equal(2, first.TargetEmployeeId);

        first.Status = SalesRequestStatuses.Inspected;
        var second = await svc.TransferNameAsync(Employee(2, "علي"), row.Id, new() { ToEmployeeId = 1, TransferReason = "كشف" }, default);
        Assert.Equal(1, second.TargetEmployeeId);
        Assert.Equal(2, repo.NameTransfers.Count);
    }

    [Fact]
    public async Task AtomicTransfer_Rejects_Terminal_Status()
    {
        var (repo, _, _, row) = await ReadyAsync(1);
        row.Status = SalesRequestStatuses.Completed;
        var ok = await repo.TryTransferTargetAsync(
            row.Id,
            1,
            2,
            "علي",
            SalesRequestStatuses.Assigned,
            DateTime.UtcNow,
            SalesFilterStatuses.ReadyForSale,
            default);
        Assert.False(ok);
        Assert.Equal(1, row.TargetEmployeeId);
        Assert.Equal(SalesRequestStatuses.Completed, row.Status);
    }

    [Fact]
    public async Task Peers_Exclude_Self()
    {
        var (_, _, svc, _) = await ReadyAsync(1);
        var peers = await svc.ListTransferPeersAsync(Employee(1, "أحمد"), default);
        Assert.DoesNotContain(peers, p => p.EmployeeId == 1);
        Assert.Contains(peers, p => p.EmployeeId == 2);
    }

    [Fact]
    public async Task Repeated_Transfer_Keeps_History()
    {
        var (repo, employees, svc, row) = await ReadyAsync(1);
        employees.ActiveEmployees =
        [
            new() { EmployeeId = 1, EmployeeName = "أحمد" },
            new() { EmployeeId = 2, EmployeeName = "علي" },
            new() { EmployeeId = 4, EmployeeName = "حسن" }
        ];
        employees.Employees.Clear();
        employees.Employees.AddRange(employees.ActiveEmployees);

        await svc.TransferNameAsync(Employee(1, "أحمد"), row.Id, new() { ToEmployeeId = 2, TransferReason = "أول" }, default);
        var second = await svc.TransferNameAsync(Employee(2, "علي"), row.Id, new() { ToEmployeeId = 4, TransferReason = "ثاني" }, default);

        Assert.Equal(2, repo.NameTransfers.Count);
        Assert.Equal(2, second.NameTransfers.Count);
        Assert.Equal("ثاني", second.LatestNameTransfer!.TransferReason);
        Assert.Equal(4, second.TargetEmployeeId);
        Assert.Contains(repo.History, h => h.Event == SalesRequestEvents.NameTransferred);
    }

    [Fact]
    public async Task Rejects_Non_Sales_Employee_Actor()
    {
        var (_, _, svc, row) = await ReadyAsync(1);
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.TransferNameAsync(Manager(), row.Id, new() { ToEmployeeId = 2, TransferReason = "سبب" }, default));
        Assert.Equal(403, ex.StatusCode);
    }
}
