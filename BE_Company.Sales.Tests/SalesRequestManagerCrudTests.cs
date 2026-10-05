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

    private sealed class FakeCascadeVoid : ISalesCascadeVoidService
    {
        public int VoidCalls { get; private set; }
        public bool LastConfirm { get; private set; }
        public int PaymentCount { get; set; }
        public bool RequireConfirm => PaymentCount > 0;

        public Task<SalesRequestSoftDeleteImpactDTO> GetImpactAsync(SalesRequestDTO request, CancellationToken ct) =>
            Task.FromResult(new SalesRequestSoftDeleteImpactDTO
            {
                RequestId = request.Id,
                HasLinkedDraft = request.ConvertedToSaleId is > 0,
                LinkedDraftSaleId = request.ConvertedToSaleId,
                IsPosted = true,
                WillDeleteOfficialSale = true,
                WillRestoreInventory = true,
                PaymentCount = PaymentCount,
                PaymentTotal = PaymentCount * 1000m,
                RequiresPaymentConfirm = PaymentCount > 0,
                WarningAr = PaymentCount > 0
                    ? $"تسديدات: {PaymentCount}"
                    : "حذف مبيع وإرجاع مخزن"
            });

        public Task VoidLinkedAsync(
            SalesIdentity manager,
            SalesRequestDTO request,
            bool confirmCascadePayments,
            string reason,
            CancellationToken ct)
        {
            if (PaymentCount > 0 && !confirmCascadePayments)
            {
                throw new SalesCompleteException(
                    409,
                    "أكد حذف التسديدات",
                    SalesCascadeVoidService.PaymentsConfirmRequiredCode);
            }

            VoidCalls++;
            LastConfirm = confirmCascadePayments;
            return Task.CompletedTask;
        }
    }

    private static async Task<(FakeRequestRepository Repo, SalesRequestService Svc, SalesRequestDTO Row, FakeCascadeVoid Cascade)> SeedAsync(
        FakeCascadeVoid? cascade = null)
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
        cascade ??= new FakeCascadeVoid();
        var svc = new SalesRequestService(repo, new FakeClock { UtcNow = DateTime.UtcNow }, employees, cascade);
        var created = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "أحمد منتظر سرحان", Phone = "7804924373", Address = "كوفة" }
        }, default);
        Assert.Equal("07804924373", created.CustomerPhone);
        return (repo, svc, created, cascade);
    }

    [Fact]
    public async Task Manager_Can_Update_Name_And_Phone()
    {
        var (_, svc, row, _) = await SeedAsync();
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
        var (_, svc, row, _) = await SeedAsync();
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.ManagerUpdateAsync(Employee(), row.Id, new() { CustomerName = "x" }, default));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task SoftDelete_Hides_From_List_And_Cascades_Completed()
    {
        var (repo, svc, row, cascade) = await SeedAsync();
        await svc.SoftDeleteAsync(Manager(), row.Id, "طلب مكرر", default);
        Assert.True(repo.Rows.First(r => r.Id == row.Id).IsDeleted);
        Assert.Equal("طلب مكرر", repo.Rows.First(r => r.Id == row.Id).DeleteReason);
        Assert.Empty(await svc.ListForManagerAsync(null, null, null, null, default));
        Assert.Single(await svc.ListDeletedForManagerAsync(default));
        Assert.Equal(0, cascade.VoidCalls);

        var sold = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "زبون مباع", Phone = "07801112233" }
        }, default);
        sold.Status = SalesRequestStatuses.Completed;
        sold.ConvertedToSaleId = 55;
        await svc.SoftDeleteAsync(Manager(), sold.Id, "إلغاء مبيع", confirmCascadePayments: false, default);
        Assert.True(repo.Rows.First(r => r.Id == sold.Id).IsDeleted);
        Assert.Equal(1, cascade.VoidCalls);
        Assert.False(cascade.LastConfirm);
    }

    [Fact]
    public async Task SoftDelete_Sold_With_Payments_Requires_Confirm()
    {
        var cascade = new FakeCascadeVoid { PaymentCount = 2 };
        var (repo, svc, _, _) = await SeedAsync(cascade);
        var sold = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "زبون تسديد", Phone = "07801112234" }
        }, default);
        sold.Status = SalesRequestStatuses.Completed;
        sold.ConvertedToSaleId = 77;

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.SoftDeleteAsync(Manager(), sold.Id, "حذف مع تسديد", confirmCascadePayments: false, default));
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(SalesCascadeVoidService.PaymentsConfirmRequiredCode, ex.Code);
        Assert.False(repo.Rows.First(r => r.Id == sold.Id).IsDeleted);

        await svc.SoftDeleteAsync(Manager(), sold.Id, "حذف مع تسديد", confirmCascadePayments: true, default);
        Assert.True(repo.Rows.First(r => r.Id == sold.Id).IsDeleted);
        Assert.Equal(1, cascade.VoidCalls);
        Assert.True(cascade.LastConfirm);
    }

    [Fact]
    public async Task SoftDeleteImpact_Reports_Payments()
    {
        var cascade = new FakeCascadeVoid { PaymentCount = 3 };
        var (_, svc, _, _) = await SeedAsync(cascade);
        var sold = await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "زبون", Phone = "07801112235" }
        }, default);
        sold.ConvertedToSaleId = 88;
        var impact = await svc.GetSoftDeleteImpactAsync(Manager(), sold.Id, default);
        Assert.True(impact.RequiresPaymentConfirm);
        Assert.Equal(3, impact.PaymentCount);
        Assert.True(impact.WillRestoreInventory);
    }

    [Fact]
    public async Task SoftDelete_Requires_Reason()
    {
        var (_, svc, row, _) = await SeedAsync();
        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.SoftDeleteAsync(Manager(), row.Id, "  ", default));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SoftDeleted_Excluded_From_Evaluation_Get()
    {
        var (repo, svc, row, _) = await SeedAsync();
        await svc.SoftDeleteAsync(Manager(), row.Id, "غير مناسب", default);
        Assert.Null(await svc.GetForManagerAsync(row.Id, default));
        Assert.Contains(repo.History, h => h.Event == SalesRequestEvents.ManagerRequestDeleted);
    }

    [Fact]
    public async Task Manager_List_Skips_Per_Row_History_Hydration()
    {
        var (repo, svc, _, _) = await SeedAsync();
        await svc.CreateAsync(Manager(), new()
        {
            Customer = new() { FullName = "زبون ثاني علي حسن", Phone = "07802223344" }
        }, default);

        var historyBefore = repo.ListHistoryCallCount;
        var transfersBefore = repo.ListNameTransfersCallCount;

        var listed = await svc.ListForManagerAsync(null, null, null, null, default);
        Assert.Equal(2, listed.Count);
        Assert.Equal(historyBefore, repo.ListHistoryCallCount);
        Assert.Equal(transfersBefore, repo.ListNameTransfersCallCount);

        _ = await svc.GetForManagerAsync(listed[0].Id, default);
        Assert.True(repo.ListHistoryCallCount > historyBefore);
    }
}
