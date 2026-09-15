using BE_SalesEmployee.Controllers;
using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SalesExceptionConsumeWorkflowTests
{
    private sealed class RecordingHoldPoster : IBranchExceptionHoldPoster
    {
        public List<(int SalesRequestId, string? Status, Guid ExceptionId)> Posts { get; } = new();

        public Task<bool> SyncHoldAsync(
            SalesExceptionRequest request,
            string? holdStatus,
            CancellationToken ct = default)
        {
            if (request.SalesRequestId is int id and > 0)
            {
                Posts.Add((id, holdStatus, request.Id));
            }

            return Task.FromResult(true);
        }
    }

    private static (SalesExceptionService Service, InMemorySalesExceptionStore Store, RecordingHoldPoster Holds)
        CreateService()
    {
        var store = new InMemorySalesExceptionStore();
        var holds = new RecordingHoldPoster();
        var service = new SalesExceptionService(store, new IntentRecordingBranchNotePoster(), holds);
        return (service, store, holds);
    }

    private static GatewayUser Manager(string userName = "mgr-najaf") => new()
    {
        UserID = "11",
        UserName = userName,
        UserType = SalesRoles.UserTypeSalesManager,
        CityValue = "najaf-demo",
        CityName = "النجف"
    };

    private static GatewayUser DelegatedManager() => new()
    {
        UserID = "0",
        UserName = "المدير المفوض",
        UserType = SalesRoles.UserTypeDelegatedManager,
        IsCentral = true
    };

    private static CreateSalesExceptionInput Input(int salesRequestId = 900) => new()
    {
        Reason = "سبب الاستثناء",
        CustomerId = 501,
        CustomerName = "عميل",
        SalesRequestId = salesRequestId,
        CityValue = "najaf-demo"
    };

    [Fact]
    public async Task Create_DuplicateActiveForSameSalesRequest_IsConflict()
    {
        var (service, _, _) = CreateService();
        Assert.True((await service.CreateAsync(Manager(), Input(42))).Ok);

        var second = await service.CreateAsync(Manager(), Input(42));

        Assert.Equal(SalesExceptionOutcome.Conflict, second.Outcome);
    }

    [Fact]
    public async Task Create_AllowsNewException_AfterPreviousConsumed()
    {
        var (service, _, _) = CreateService();
        var first = await service.CreateAsync(Manager(), Input(55));
        Assert.True(first.Ok);
        await service.DecideAsync(DelegatedManager(), first.Request!.Id, "approve", null);
        Assert.True((await service.TryConsumeAssignmentAsync(
            Manager(), first.Request.Id, 1, "أحمد")).Ok);

        var second = await service.CreateAsync(Manager(), Input(55));
        Assert.True(second.Ok);
    }

    [Fact]
    public async Task Approve_SyncsApprovedHoldToBranch()
    {
        var (service, _, holds) = CreateService();
        var created = await service.CreateAsync(Manager(), Input(77));
        await service.DecideAsync(DelegatedManager(), created.Request!.Id, "approve", "موافق");

        Assert.Contains(holds.Posts, p =>
            p.SalesRequestId == 77
            && p.Status == ExceptionStatuses.Approved
            && p.ExceptionId == created.Request.Id);
    }

    [Fact]
    public async Task Reject_SyncsRejectedHoldToBranch()
    {
        var (service, _, holds) = CreateService();
        var created = await service.CreateAsync(Manager(), Input(78));
        await service.DecideAsync(DelegatedManager(), created.Request!.Id, "reject", "مرفوض");

        Assert.Contains(holds.Posts, p =>
            p.SalesRequestId == 78 && p.Status == ExceptionStatuses.Rejected);
    }

    [Fact]
    public async Task Cancel_ClearsHoldOnBranch()
    {
        var (service, _, holds) = CreateService();
        var created = await service.CreateAsync(Manager(), Input(79));
        await service.DecideAsync(Manager(), created.Request!.Id, "cancel", null);

        Assert.Contains(holds.Posts, p =>
            p.SalesRequestId == 79 && p.Status is null);
    }

    [Fact]
    public async Task Consume_OnlyWhenApprovedAndNotConsumed()
    {
        var (service, store, _) = CreateService();
        var pending = await service.CreateAsync(Manager(), Input(80));
        var pendingConsume = await service.TryConsumeAssignmentAsync(
            Manager(), pending.Request!.Id, 1, "أ");
        Assert.Equal(SalesExceptionOutcome.Conflict, pendingConsume.Outcome);

        await service.DecideAsync(DelegatedManager(), pending.Request.Id, "approve", null);
        var first = await service.TryConsumeAssignmentAsync(Manager(), pending.Request.Id, 2, "ب");
        Assert.True(first.Ok);
        Assert.True(first.Request!.AssignmentConsumed);
        Assert.Equal(2, first.Request.AssignedEmployeeId);
        Assert.Equal("ب", first.Request.AssignedEmployeeName);
        Assert.NotNull(first.Request.AssignedAtUtc);
        Assert.Equal("mgr-najaf", first.Request.AssignedByManagerUserName);

        var second = await service.TryConsumeAssignmentAsync(Manager(), pending.Request.Id, 3, "ج");
        Assert.Equal(SalesExceptionOutcome.Conflict, second.Outcome);

        var stored = await store.GetAsync(pending.Request.Id);
        Assert.Equal(ExceptionStatuses.Approved, stored!.Status);
        Assert.True(stored.AssignmentConsumed);
        Assert.Equal(2, stored.AssignedEmployeeId);
    }

    [Fact]
    public async Task ConcurrentConsume_OnlyOneWins()
    {
        var (service, _, _) = CreateService();
        var created = await service.CreateAsync(Manager(), Input(81));
        await service.DecideAsync(DelegatedManager(), created.Request!.Id, "approve", null);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(i => service.TryConsumeAssignmentAsync(Manager(), created.Request.Id, i + 1, $"E{i}")));

        Assert.Equal(1, results.Count(r => r.Ok));
        Assert.Equal(7, results.Count(r => r.Outcome == SalesExceptionOutcome.Conflict));
    }

    [Fact]
    public async Task FindActive_SeesPendingAndApprovedUnconsumed_Only()
    {
        var (service, store, _) = CreateService();
        var pending = await service.CreateAsync(Manager(), Input(90));
        Assert.NotNull(await store.FindActiveBySalesRequestIdAsync(90));

        await service.DecideAsync(DelegatedManager(), pending.Request!.Id, "approve", null);
        Assert.NotNull(await store.FindActiveBySalesRequestIdAsync(90));

        await service.TryConsumeAssignmentAsync(Manager(), pending.Request.Id, 1, "أ");
        Assert.Null(await store.FindActiveBySalesRequestIdAsync(90));
    }

    [Fact]
    public async Task InternalGetAndConsume_RequireGatewayKey()
    {
        var (service, _, _) = CreateService();
        var created = await service.CreateAsync(Manager(), Input(91));
        await service.DecideAsync(DelegatedManager(), created.Request!.Id, "approve", null);

        var controller = new InternalSalesExceptionsController(
            service,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalApiKey"] = "secret" })
                .Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        Assert.IsType<UnauthorizedObjectResult>(await controller.Get(created.Request.Id, CancellationToken.None));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Consume(
            created.Request.Id,
            new InternalSalesExceptionsController.ConsumeBody { EmployeeId = 1, EmployeeName = "أ" },
            CancellationToken.None));

        controller.Request.Headers[InternalSalesExceptionsController.GatewayKeyHeader] = "secret";
        var get = await controller.Get(created.Request.Id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(get);

        var consume = await controller.Consume(
            created.Request.Id,
            new InternalSalesExceptionsController.ConsumeBody
            {
                EmployeeId = 5,
                EmployeeName = "خالد",
                AssignedByManagerUserName = "mgr-najaf"
            },
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(consume);
    }

    [Fact]
    public async Task InternalCreate_Duplicate_Returns409()
    {
        var (service, _, _) = CreateService();
        await service.CreateAsync(Manager(), Input(92));

        var controller = new InternalSalesExceptionsController(
            service,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalApiKey"] = "secret" })
                .Build())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers[InternalSalesExceptionsController.GatewayKeyHeader] = "secret";

        var response = await controller.Create(new InternalSalesExceptionsController.TrustedExceptionRequest
        {
            CityValue = "najaf-demo",
            Reason = "تكرار",
            SalesRequestId = 92,
            RequesterUserName = "mgr-najaf",
            Role = SalesRoles.UserTypeSalesManager
        }, CancellationToken.None);

        var status = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
    }
}
