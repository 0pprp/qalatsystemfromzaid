using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesRequestManagerCrudTests
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

    private static SalesIdentity Employee() => new()
    {
        EmployeeId = 1,
        EmployeeName = "أحمد",
        BranchId = "najaf-demo",
        Role = SalesRoles.SalesEmployee,
        UserType = SalesRoles.UserTypeSalesEmployee
    };

    private static async Task<(FakeRequestRepository Repo, SalesRequestService Svc, SalesRequestDTO Row)> SeedAsync()
    {
        var repo = new FakeRequestRepository();
        var employees = new FakeManagerRead();
        employees.ActiveEmployees =
        [
            new() { EmployeeId = 1, EmployeeName = "أحمد" },
            new() { EmployeeId = 2, EmployeeName = "علي" }
        ];
        employees.Employees.Clear();
        employees.Employees.AddRange(employees.ActiveEmployees);
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow }, employees);
        var created = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "أحمد منتظر سرحان", Phone = "7804924373", Address = "كوفة" }
        }, default);
        Assert.Equal("07804924373", created.CustomerPhone);
        return (repo, svc, created);
    }

    [Fact]
    public async Task Manager_Can_Update_Name_And_Phone()
    {
        var (_, svc, row) = await SeedAsync();
        var updated = await svc.ManagerUpdateAsync(Manager(), row.Id, new SalesRequestManagerUpdateDTO
        {
            CustomerName = "أحمد منتظر سرحان",
            Phone = "07801112233"
        }, default);
        Assert.Equal("07801112233", updated.CustomerPhone);
    }

    [Fact]
    public async Task Employee_Cannot_Use_Manager_Update()
    {
        var (_, svc, row) = await SeedAsync();
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.ManagerUpdateAsync(Employee(), row.Id, new() { CustomerName = "x" }, default));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task SoftDelete_Hides_From_List_And_Blocks_Completed()
    {
        var (repo, svc, row) = await SeedAsync();
        await svc.SoftDeleteAsync(Manager(), row.Id, default);
        Assert.True(repo.Rows.First(r => r.Id == row.Id).IsDeleted);
        Assert.Empty(await svc.ListForManagerAsync(null, null, null, null, default));

        var sold = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "زبون مباع", Phone = "07801112233" }
        }, default);
        sold.Status = SalesRequestStatuses.Completed;
        sold.ConvertedToSaleId = 55;
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.SoftDeleteAsync(Manager(), sold.Id, default));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task SoftDeleted_Excluded_From_Evaluation_Get()
    {
        var (repo, svc, row) = await SeedAsync();
        await svc.SoftDeleteAsync(Manager(), row.Id, default);
        Assert.Null(await svc.GetForManagerAsync(row.Id, default));
        Assert.Contains(repo.History, h => h.Event == SalesRequestEvents.ManagerRequestDeleted);
    }
}
