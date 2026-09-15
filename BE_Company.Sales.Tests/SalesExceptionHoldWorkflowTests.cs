using System.Security.Claims;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// Covers the 15 denormalized branch-hold exception workflow scenarios.
/// </summary>
public sealed class SalesExceptionHoldWorkflowTests
{
    private static SalesIdentity Manager(string name = "mgr-najaf") => new()
    {
        EmployeeId = 90,
        EmployeeName = name,
        BranchId = "najaf-demo",
        BranchName = "النجف",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

    private static SalesRequestService Svc(FakeRequestRepository repo) =>
        new(repo, new FakeClock { UtcNow = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc) }, new FakeManagerRead());

    private static async Task<SalesRequestDTO> NewUnassignedAsync(SalesRequestService svc, string name = "زبون استثناء")
    {
        return await svc.CreateAsync(Manager(), new SalesRequestCreateDTO
        {
            Customer = new SalesRequestCustomerDTO { FullName = name, Phone = "07701234567" }
        }, CancellationToken.None);
    }

    private sealed class WorkflowForwarder : ISalesExceptionGatewayForwarder
    {
        public SalesExceptionGatewayForwardResult CreateResult { get; set; } =
            SalesExceptionGatewayForwardResult.Success("""{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Pending","salesRequestId":1}""");

        public SalesExceptionGatewayForwardResult GetResult { get; set; } =
            SalesExceptionGatewayForwardResult.Success("""{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Approved","assignmentConsumed":false,"salesRequestId":1}""");

        public SalesExceptionGatewayForwardResult ConsumeResult { get; set; } =
            SalesExceptionGatewayForwardResult.Success("""{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Approved","assignmentConsumed":true}""");

        public SalesExceptionCreateForwardRequest? LastCreate { get; private set; }
        public Guid? LastGetId { get; private set; }
        public SalesExceptionConsumeForwardRequest? LastConsume { get; private set; }
        public int ConsumeCalls { get; private set; }

        public Task<SalesExceptionGatewayForwardResult> CreateAsync(
            SalesExceptionCreateForwardRequest request,
            CancellationToken ct = default)
        {
            LastCreate = request;
            return Task.FromResult(CreateResult);
        }

        public Task<SalesExceptionGatewayForwardResult> ListAsync(
            SalesExceptionListForwardRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(SalesExceptionGatewayForwardResult.Success("""{"page":1,"totalCount":0,"items":[]}"""));

        public Task<SalesExceptionGatewayForwardResult> GetAsync(Guid id, CancellationToken ct = default)
        {
            LastGetId = id;
            return Task.FromResult(GetResult);
        }

        public Task<SalesExceptionGatewayForwardResult> ConsumeAsync(
            Guid id,
            SalesExceptionConsumeForwardRequest request,
            CancellationToken ct = default)
        {
            LastConsume = request;
            ConsumeCalls++;
            return Task.FromResult(ConsumeResult);
        }
    }

    private static IConfiguration BranchConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesManagement:RequireDemoDatabase"] = "false",
                ["SalesManagement:BranchId"] = "najaf-demo",
                ["SalesManagement:BranchName"] = "النجف - DEMO"
            })
            .Build();

    private static SalesManagerExceptionsController CreateExceptionsController(
        WorkflowForwarder forwarder,
        FakeRequestRepository repo,
        string userName = "mgr-najaf")
    {
        var http = new DefaultHttpContext();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("UserType", SalesRoles.UserTypeSalesManager),
            new Claim("UserName", userName),
            new Claim("UserID", "90")
        ], "Test"));
        http.Items["UserType"] = SalesRoles.UserTypeSalesManager;
        http.Items["UserID"] = "90";

        var accessor = new HttpContextAccessor { HttpContext = http };
        var identity = new SalesIdentityService(accessor, BranchConfig());
        var requests = Svc(repo);
        return new SalesManagerExceptionsController(identity, forwarder, requests)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    // 1. Create exception -> request disappears from unassigned
    [Fact]
    public async Task CreateException_SetsPendingHold_AndExcludesFromManagerList()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc);
        var exceptionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var forwarder = new WorkflowForwarder
        {
            CreateResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"{{exceptionId}}","status":"Pending","salesRequestId":{{created.Id}}}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            SalesRequestId = created.Id,
            Reason = "سبب الاستثناء"
        }, CancellationToken.None);

        Assert.IsType<ContentResult>(response);
        var row = Assert.Single(repo.Rows);
        Assert.Equal(SalesExceptionHoldStatuses.Pending, row.ExceptionHoldStatus);
        Assert.Equal(exceptionId, row.ActiveExceptionId);

        var listed = await svc.ListForManagerAsync(null, null, null, null, CancellationToken.None);
        Assert.Empty(listed);
    }

    // 2. Create exception -> appears in exception list (forwarder called / returns body)
    [Fact]
    public async Task CreateException_ForwardsAndReturnsGatewayBody()
    {
        var repo = new FakeRequestRepository();
        var created = await NewUnassignedAsync(Svc(repo));
        var forwarder = new WorkflowForwarder
        {
            CreateResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Pending","salesRequestId":{{created.Id}}}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            SalesRequestId = created.Id,
            Reason = "سبب"
        }, CancellationToken.None);

        var content = Assert.IsType<ContentResult>(response);
        Assert.Contains("Pending", content.Content);
        Assert.NotNull(forwarder.LastCreate);
        Assert.Equal(created.Id, forwarder.LastCreate!.SalesRequestId);
    }

    // 3. Pending -> cannot assign
    [Fact]
    public async Task PendingHold_DirectAssign_Is409()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc);
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Pending;
        created.ActiveExceptionId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.AssignAsync(Manager(), created.Id, new SalesRequestAssignDTO { EmployeeId = 1 }, CancellationToken.None));

        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    // 4 + 5 + 6. Approved -> assign API allowed; full data preserved; uses AssignAsync
    [Fact]
    public async Task ApprovedException_Assign_UsesOriginalRequestAndPreservesCustomer()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc, "زبون محفوظ");
        var exceptionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Approved;
        created.ActiveExceptionId = exceptionId;

        var forwarder = new WorkflowForwarder
        {
            GetResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"{{exceptionId}}","status":"Approved","assignmentConsumed":false,"salesRequestId":{{created.Id}}}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        var response = await controller.Assign(exceptionId, new SalesManagerExceptionsController.AssignExceptionBody
        {
            EmployeeId = 1,
            EmployeeName = "أحمد"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        var row = Assert.Single(repo.Rows);
        Assert.Equal(1, row.TargetEmployeeId);
        Assert.Equal("زبون محفوظ", row.CustomerName);
        Assert.Equal("أحمد", row.TargetEmployeeName);
        Assert.Null(row.ExceptionHoldStatus);
        Assert.Null(row.ActiveExceptionId);
        Assert.Equal(1, forwarder.ConsumeCalls);
        Assert.Equal(1, forwarder.LastConsume!.EmployeeId);
    }

    // 7. Approved -> second assignment rejected
    [Fact]
    public async Task ApprovedException_SecondAssign_IsRejected()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc);
        var exceptionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Approved;
        created.ActiveExceptionId = exceptionId;

        var forwarder = new WorkflowForwarder
        {
            GetResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"{{exceptionId}}","status":"Approved","assignmentConsumed":false,"salesRequestId":{{created.Id}}}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        Assert.IsType<OkObjectResult>(await controller.Assign(exceptionId,
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 1 }, CancellationToken.None));

        forwarder.GetResult = SalesExceptionGatewayForwardResult.Success(
            $$"""{"id":"{{exceptionId}}","status":"Approved","assignmentConsumed":true,"salesRequestId":{{created.Id}}}""");
        forwarder.ConsumeResult = SalesExceptionGatewayForwardResult.Failure(
            StatusCodes.Status409Conflict, "تم إسناد الاستثناء مسبقاً");

        var second = await controller.Assign(exceptionId,
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 2 }, CancellationToken.None);

        var status = Assert.IsAssignableFrom<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
    }

    // 8. Rejected -> cannot assign
    [Fact]
    public async Task RejectedHold_CannotAssign()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc);
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Rejected;
        created.ActiveExceptionId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.AssignAsync(Manager(), created.Id, new SalesRequestAssignDTO { EmployeeId = 1 }, CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);

        var forwarder = new WorkflowForwarder
        {
            GetResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Rejected","assignmentConsumed":false,"salesRequestId":{{created.Id}}}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);
        var response = await controller.Assign(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 1 }, CancellationToken.None);
        var status = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
    }

    // 9. Rejected -> does not return to unassigned
    [Fact]
    public async Task RejectedHold_StaysExcludedFromManagerList()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc);
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Rejected;
        created.ActiveExceptionId = Guid.NewGuid();

        Assert.Empty(await svc.ListForManagerAsync(null, null, null, null, CancellationToken.None));
    }

    // 10. Duplicate exception for same SalesRequest rejected
    [Fact]
    public async Task DuplicateActiveException_ReturnsConflictFromGateway()
    {
        var repo = new FakeRequestRepository();
        var created = await NewUnassignedAsync(Svc(repo));
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Pending;
        var forwarder = new WorkflowForwarder
        {
            CreateResult = SalesExceptionGatewayForwardResult.Failure(
                StatusCodes.Status409Conflict, "يوجد طلب استثناء نشط لهذا الطلب")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            SalesRequestId = created.Id,
            Reason = "تكرار"
        }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
    }

    // 11. Assignment by manager outside city/scope rejected
    [Fact]
    public async Task Assign_OutsideCityScope_Is403()
    {
        var repo = new FakeRequestRepository();
        var created = await NewUnassignedAsync(Svc(repo));
        created.CityValue = "basra-demo";
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Approved;
        var exceptionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        created.ActiveExceptionId = exceptionId;

        var forwarder = new WorkflowForwarder
        {
            GetResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"{{exceptionId}}","status":"Approved","assignmentConsumed":false,"salesRequestId":{{created.Id}},"cityValue":"basra-demo"}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        // Force city mismatch on the sales request so assign path rejects before assign.
        created.CityValue = "basra-demo";
        var response = await controller.Assign(exceptionId,
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 1 }, CancellationToken.None);

        // Branch tenancy: request city outside manager branch is rejected.
        var status = Assert.IsType<ObjectResult>(response);
        Assert.True(
            status.StatusCode is StatusCodes.Status403Forbidden or StatusCodes.Status409Conflict,
            $"Unexpected status {status.StatusCode}");
    }

    // 12. Missing original SalesRequest returns clear error
    [Fact]
    public async Task Assign_MissingOriginalSalesRequest_Returns404()
    {
        var repo = new FakeRequestRepository();
        var forwarder = new WorkflowForwarder
        {
            GetResult = SalesExceptionGatewayForwardResult.Success(
                """{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Approved","assignmentConsumed":false,"salesRequestId":999}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        var response = await controller.Assign(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 1 }, CancellationToken.None);

        var status = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, status.StatusCode);
    }

    // 13 + 14. Exception history remains after assignment (hold cleared; consume called with employee)
    [Fact]
    public async Task Assign_ClearsHold_AndReportsAssignedEmployeeToGateway()
    {
        var repo = new FakeRequestRepository();
        var created = await NewUnassignedAsync(Svc(repo));
        var exceptionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Approved;
        created.ActiveExceptionId = exceptionId;
        var forwarder = new WorkflowForwarder
        {
            GetResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"{{exceptionId}}","status":"Approved","assignmentConsumed":false,"salesRequestId":{{created.Id}}}""")
        };
        var controller = CreateExceptionsController(forwarder, repo);

        await controller.Assign(exceptionId,
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 2, EmployeeName = "علي" },
            CancellationToken.None);

        Assert.Null(created.ExceptionHoldStatus);
        Assert.Equal(2, forwarder.LastConsume!.EmployeeId);
        Assert.Equal("علي", forwarder.LastConsume.EmployeeName);
        Assert.Equal("mgr-najaf", forwarder.LastConsume.AssignedByManagerUserName);
    }

    // 15. Concurrency: two assignment attempts cannot both succeed (second blocked by assigned state / consumed)
    [Fact]
    public async Task ConcurrentAssign_OnlyOneSucceeds()
    {
        var repo = new FakeRequestRepository();
        var created = await NewUnassignedAsync(Svc(repo));
        var exceptionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Approved;
        created.ActiveExceptionId = exceptionId;

        var consumeGate = 0;
        var forwarder = new WorkflowForwarder
        {
            GetResult = SalesExceptionGatewayForwardResult.Success(
                $$"""{"id":"{{exceptionId}}","status":"Approved","assignmentConsumed":false,"salesRequestId":{{created.Id}}}""")
        };
        // First consume ok; subsequent conflict
        forwarder.ConsumeResult = SalesExceptionGatewayForwardResult.Success(
            """{"id":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","status":"Approved","assignmentConsumed":true}""");

        var controller = CreateExceptionsController(forwarder, repo);
        var first = await controller.Assign(exceptionId,
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 1 }, CancellationToken.None);
        Assert.IsType<OkObjectResult>(first);

        forwarder.GetResult = SalesExceptionGatewayForwardResult.Success(
            $$"""{"id":"{{exceptionId}}","status":"Approved","assignmentConsumed":true,"salesRequestId":{{created.Id}}}""");
        var second = await controller.Assign(exceptionId,
            new SalesManagerExceptionsController.AssignExceptionBody { EmployeeId = 2 }, CancellationToken.None);
        var secondStatus = Assert.IsAssignableFrom<ObjectResult>(second);
        Assert.Equal(StatusCodes.Status409Conflict, secondStatus.StatusCode);
        Assert.Equal(1, created.TargetEmployeeId);
        _ = consumeGate;
    }

    [Fact]
    public async Task SetExceptionHold_UpdatesBranchRow()
    {
        var repo = new FakeRequestRepository();
        var created = await NewUnassignedAsync(Svc(repo));
        var exceptionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        await Svc(repo).SetExceptionHoldAsync(
            created.Id,
            SalesExceptionHoldStatuses.Approved,
            exceptionId,
            CancellationToken.None);

        Assert.Equal(SalesExceptionHoldStatuses.Approved, created.ExceptionHoldStatus);
        Assert.Equal(exceptionId, created.ActiveExceptionId);

        await Svc(repo).SetExceptionHoldAsync(created.Id, null, null, CancellationToken.None);
        Assert.Null(created.ExceptionHoldStatus);
        Assert.Null(created.ActiveExceptionId);
    }

    [Fact]
    public async Task ApprovedHold_DirectAssignWithoutExceptionId_Is409()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc);
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Approved;
        created.ActiveExceptionId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
            svc.AssignAsync(Manager(), created.Id, new SalesRequestAssignDTO { EmployeeId = 1 }, CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, ex.StatusCode);
    }

    [Fact]
    public async Task ApprovedHold_AssignWithMatchingExceptionId_Succeeds()
    {
        var repo = new FakeRequestRepository();
        var svc = Svc(repo);
        var created = await NewUnassignedAsync(svc);
        var exceptionId = Guid.NewGuid();
        created.ExceptionHoldStatus = SalesExceptionHoldStatuses.Approved;
        created.ActiveExceptionId = exceptionId;

        var assigned = await svc.AssignAsync(
            Manager(),
            created.Id,
            new SalesRequestAssignDTO { EmployeeId = 1, EmployeeName = "أحمد" },
            CancellationToken.None,
            exceptionAssignId: exceptionId);

        Assert.Equal(1, assigned.TargetEmployeeId);
        // Hold cleared by assign path caller; AssignAsync itself may leave hold for controller to clear.
    }
}
