using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class SalesRequestAssignBatchTests
{
    private static SalesIdentity Manager() => new()
    {
        EmployeeId = 90,
        EmployeeName = "mgr",
        BranchId = "najaf-demo",
        BranchName = "النجف",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

    private static SalesRequestService Svc(FakeRequestRepository repo) =>
        new(repo, new FakeClock { UtcNow = new DateTime(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc) }, new FakeManagerRead());

    private static async Task<SalesRequestDTO> NewUnassignedAsync(SalesRequestService svc, string name)
    {
        return await svc.CreateAsync(Manager(), new SalesRequestCreateDTO
        {
            Customer = new SalesRequestCustomerDTO { FullName = name, Phone = "07701234567" }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task AssignBatch_AssignsEachRequestSeparately_ToSameEmployee()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var a = await NewUnassignedAsync(svc, "زبون أ");
        var b = await NewUnassignedAsync(svc, "زبون ب");

        var result = await svc.AssignBatchAsync(Manager(), new SalesRequestAssignBatchDTO
        {
            EmployeeId = 1,
            EmployeeName = "أحمد",
            RequestIds = [a.Id, b.Id]
        }, CancellationToken.None);

        Assert.Equal(2, result.Succeeded.Count);
        Assert.Empty(result.Failed);
        Assert.Equal(SalesRequestStatuses.Assigned, (await repo.GetByIdAsync(a.Id, default))!.Status);
        Assert.Equal(1, (await repo.GetByIdAsync(a.Id, default))!.TargetEmployeeId);
        Assert.Equal(1, (await repo.GetByIdAsync(b.Id, default))!.TargetEmployeeId);
        Assert.Equal("زبون أ", (await repo.GetByIdAsync(a.Id, default))!.CustomerName);
        Assert.Equal("زبون ب", (await repo.GetByIdAsync(b.Id, default))!.CustomerName);
    }

    [Fact]
    public async Task AssignBatch_ContinuesOnFailure_AndReportsFailed()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var ok = await NewUnassignedAsync(svc, "صالح");
        var held = await NewUnassignedAsync(svc, "محجوز");
        held.ExceptionHoldStatus = SalesExceptionHoldStatuses.Pending;
        held.ActiveExceptionId = Guid.NewGuid();

        var result = await svc.AssignBatchAsync(Manager(), new SalesRequestAssignBatchDTO
        {
            EmployeeId = 1,
            RequestIds = [ok.Id, held.Id, 99999]
        }, CancellationToken.None);

        Assert.Equal(new[] { ok.Id }, result.Succeeded);
        Assert.Equal(2, result.Failed.Count);
        Assert.Contains(result.Failed, f => f.Id == held.Id && f.Message.Contains("استثناء"));
        Assert.Contains(result.Failed, f => f.Id == 99999);
    }

    [Fact]
    public async Task AssignBatch_RejectsEmptyOrOverCap()
    {
        var svc = Svc(new FakeRequestRepository());
        await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.AssignBatchAsync(Manager(), new SalesRequestAssignBatchDTO
            {
                EmployeeId = 1,
                RequestIds = []
            }, CancellationToken.None));

        var tooMany = Enumerable.Range(1, SalesRequestService.AssignBatchMaxIds + 1).ToList();
        await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.AssignBatchAsync(Manager(), new SalesRequestAssignBatchDTO
            {
                EmployeeId = 1,
                RequestIds = tooMany
            }, CancellationToken.None));
    }
}
