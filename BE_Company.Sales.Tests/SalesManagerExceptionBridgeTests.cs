using System.Net;
using System.Security.Claims;
using System.Text.Json;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// FE_Company sends the branch JWT to BE_Company; this bridge validates مدير مبيعات
/// then forwards to the Sales Gateway with X-Sales-Gateway-Key — never the browser JWT.
/// </summary>
public sealed class SalesManagerExceptionBridgeTests
{
    private const string GatewayKey = "SalesEmployee-Gateway-2026";

    private sealed class RecordingForwarder : ISalesExceptionGatewayForwarder
    {
        public SalesExceptionGatewayForwardResult Result { get; set; } =
            SalesExceptionGatewayForwardResult.Success("""{"id":"00000000-0000-0000-0000-000000000001","status":"Pending"}""");

        public SalesExceptionCreateForwardRequest? LastCreate { get; private set; }
        public SalesExceptionListForwardRequest? LastList { get; private set; }

        public Task<SalesExceptionGatewayForwardResult> CreateAsync(
            SalesExceptionCreateForwardRequest request,
            CancellationToken ct = default)
        {
            LastCreate = request;
            return Task.FromResult(Result);
        }

        public Task<SalesExceptionGatewayForwardResult> ListAsync(
            SalesExceptionListForwardRequest request,
            CancellationToken ct = default)
        {
            LastList = request;
            return Task.FromResult(Result);
        }

        public Task<SalesExceptionGatewayForwardResult> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Result);

        public Task<SalesExceptionGatewayForwardResult> ConsumeAsync(
            Guid id,
            SalesExceptionConsumeForwardRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(Result);
    }

    private static IConfiguration BranchConfig(
        string branchId = "najaf-demo",
        string branchName = "النجف - DEMO") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesManagement:RequireDemoDatabase"] = "false",
                ["SalesManagement:BranchId"] = branchId,
                ["SalesManagement:BranchName"] = branchName
            })
            .Build();

    private static SalesManagerExceptionsController CreateController(
        RecordingForwarder forwarder,
        string? userType,
        string userName = "mgr-najaf",
        string userId = "11",
        IConfiguration? configuration = null)
    {
        var config = configuration ?? BranchConfig();
        var http = new DefaultHttpContext();
        if (userType != null)
        {
            var claims = new List<Claim>
            {
                new("UserType", userType),
                new("UserName", userName),
                new("UserID", userId)
            };
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
            http.Items["UserType"] = userType;
            http.Items["UserID"] = userId;
        }

        var accessor = new HttpContextAccessor { HttpContext = http };
        var identity = new SalesIdentityService(accessor, config);
        var requests = new SalesRequestService(new FakeRequestRepository(), new FakeClock());
        var controller = new SalesManagerExceptionsController(identity, forwarder, requests)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        return controller;
    }

    [Fact]
    public void ExceptionsRoute_RequiresSalesManagerPolicy()
    {
        var attr = typeof(SalesManagerExceptionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        Assert.Equal(SalesPolicies.SalesManager, attr.Policy);
    }

    [Fact]
    public async Task AuthenticatedSalesManager_Create_ForwardsTrustedActorAndSucceeds()
    {
        var forwarder = new RecordingForwarder();
        var controller = CreateController(forwarder, SalesRoles.UserTypeSalesManager);

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            CityName = "النجف - DEMO",
            CustomerId = 12,
            Reason = "سبب الاستثناء من مدير المبيعات",
            // Spoof attempt — must be ignored
            RequesterUserName = "evil-spoof",
            RequesterDisplayName = "محتال"
        }, CancellationToken.None);

        Assert.IsType<ContentResult>(response);
        Assert.NotNull(forwarder.LastCreate);
        Assert.Equal("mgr-najaf", forwarder.LastCreate!.RequesterUserName);
        Assert.Equal("11", forwarder.LastCreate.RequesterUserId);
        Assert.Equal(SalesRoles.UserTypeSalesManager, forwarder.LastCreate.Role);
        Assert.NotEqual("evil-spoof", forwarder.LastCreate.RequesterUserName);
        Assert.Equal("najaf-demo", forwarder.LastCreate.CityValue);
        Assert.Equal("سبب الاستثناء من مدير المبيعات", forwarder.LastCreate.Reason);
    }

    [Fact]
    public async Task Unauthenticated_Create_Is401()
    {
        var controller = CreateController(new RecordingForwarder(), userType: null);

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            Reason = "سبب"
        }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
    }

    [Fact]
    public async Task WrongRole_AuthorizationHandler_DoesNotSucceed()
    {
        var handler = new SalesRoleHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("UserType", SalesRoles.UserTypeSalesEmployee)], "Test"));
        var authContext = new AuthorizationHandlerContext(
            [new SalesRoleRequirement(SalesRoles.SalesManager)],
            user,
            resource: null);

        await handler.HandleAsync(authContext);

        Assert.False(authContext.HasSucceeded);
    }

    [Fact]
    public async Task BodyCannotSpoofIdentity_ForwardUsesPrincipalOnly()
    {
        var forwarder = new RecordingForwarder();
        var controller = CreateController(forwarder, SalesRoles.UserTypeSalesManager, userName: "real-mgr", userId: "42");

        await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            Reason = "سبب حقيقي",
            RequesterUserId = "999",
            RequesterUserName = "spoofed",
            RequesterDisplayName = "مزيف",
            Role = "مندوب"
        }, CancellationToken.None);

        Assert.Equal("42", forwarder.LastCreate!.RequesterUserId);
        Assert.Equal("real-mgr", forwarder.LastCreate.RequesterUserName);
        Assert.Equal(SalesRoles.UserTypeSalesManager, forwarder.LastCreate.Role);
    }

    [Fact]
    public async Task InvalidCityAccess_IsRejected()
    {
        var forwarder = new RecordingForwarder();
        var controller = CreateController(
            forwarder,
            SalesRoles.UserTypeSalesManager,
            configuration: BranchConfig(branchId: "najaf-demo"));

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "basra-demo",
            Reason = "محاولة محافظة أخرى"
        }, CancellationToken.None);

        Assert.IsType<ObjectResult>(response);
        var status = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        Assert.Null(forwarder.LastCreate);
    }

    [Fact]
    public async Task GatewayUnavailable_Returns503_Not401()
    {
        var forwarder = new RecordingForwarder
        {
            Result = SalesExceptionGatewayForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "تعذر الاتصال")
        };
        var controller = CreateController(forwarder, SalesRoles.UserTypeSalesManager);

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            Reason = "سبب"
        }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, status.StatusCode);
    }

    [Fact]
    public async Task GatewayRejected_Returns502_Not401()
    {
        var forwarder = new RecordingForwarder
        {
            Result = SalesExceptionGatewayForwardResult.Failure(
                StatusCodes.Status502BadGateway,
                "تعذر تسليم الطلب")
        };
        var controller = CreateController(forwarder, SalesRoles.UserTypeSalesManager);

        var response = await controller.Create(new SalesManagerExceptionsController.CreateExceptionBody
        {
            CityValue = "najaf-demo",
            Reason = "سبب"
        }, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, status.StatusCode);
    }

    [Fact]
    public async Task List_ForwardsAuthenticatedManagerScope()
    {
        var forwarder = new RecordingForwarder
        {
            Result = SalesExceptionGatewayForwardResult.Success("""{"page":1,"totalCount":0,"items":[]}""")
        };
        var controller = CreateController(forwarder, SalesRoles.UserTypeSalesManager, userName: "mgr-najaf");

        var response = await controller.List("Pending", "najaf-demo", 1, 20, CancellationToken.None);

        Assert.IsType<ContentResult>(response);
        Assert.Equal("mgr-najaf", forwarder.LastList!.RequesterUserName);
        Assert.Equal("Pending", forwarder.LastList.Status);
        Assert.Equal("najaf-demo", forwarder.LastList.CityValue);
    }

    [Fact]
    public void FrontendExceptionHelpers_UseBranchSalesManagerBase_NotSalesGw()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "FE_Company", "src", "composables", "salesManagerApi.js"));
        Assert.True(File.Exists(path), $"Missing FE composable at {path}");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("salesExceptionBase", source);
        Assert.Contains("smPost('exceptions'", source.Replace('"', '\''));
        Assert.Contains("smGet(", source);
        Assert.Contains("exceptions", source);

        var createIdx = source.IndexOf("export async function createExceptionRequest", StringComparison.Ordinal);
        var listIdx = source.IndexOf("export async function listExceptionRequests", StringComparison.Ordinal);
        Assert.True(createIdx >= 0 && listIdx >= 0);
        var createBlock = source.Substring(createIdx, listIdx - createIdx);
        Assert.DoesNotContain("sales-gw", createBlock);
        Assert.DoesNotContain("salesGatewayBase()", createBlock);
        Assert.Contains("smPost", createBlock);

        var listBlock = source[listIdx..];
        var nextExport = listBlock.IndexOf("\nexport ", 10, StringComparison.Ordinal);
        if (nextExport > 0)
        {
            listBlock = listBlock[..nextExport];
        }

        Assert.DoesNotContain("sales-gw", listBlock);
        Assert.DoesNotContain("salesGatewayBase()", listBlock);
        Assert.Contains("smGet", listBlock);
    }
}

public sealed class SalesExceptionGatewayForwarderTests
{
    private const string GatewayKey = "SalesEmployee-Gateway-2026";

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly Exception? _throw;

        public CapturingHandler(HttpStatusCode status = HttpStatusCode.OK, Exception? toThrow = null)
        {
            _status = status;
            _throw = toThrow;
        }

        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (_throw != null)
            {
                throw _throw;
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent("""{"id":"00000000-0000-0000-0000-000000000001","status":"Pending"}""")
            };
        }
    }

    private static SalesExceptionGatewayForwarder CreateForwarder(
        CapturingHandler handler,
        string? baseUrl = "http://127.0.0.1:5403/api/",
        string? internalApiKey = GatewayKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesGateway:BaseUrl"] = baseUrl,
                ["InternalApiKey"] = internalApiKey,
                ["SalesEmployee:GatewayKey"] = null
            })
            .Build();

        return new SalesExceptionGatewayForwarder(
            new HttpClient(handler),
            configuration,
            NullLogger<SalesExceptionGatewayForwarder>.Instance);
    }

    private static SalesExceptionCreateForwardRequest SampleCreate() => new()
    {
        CityValue = "najaf-demo",
        CityName = "النجف - DEMO",
        Reason = "سبب",
        RequesterUserId = "11",
        RequesterUserName = "mgr-najaf",
        RequesterDisplayName = "مدير",
        Role = SalesRoles.UserTypeSalesManager
    };

    [Fact]
    public async Task Forward_PostsInternalPathWithGatewayKey()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler);

        var result = await forwarder.CreateAsync(SampleCreate());

        Assert.True(result.Ok);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(
            "http://127.0.0.1:5403/api/internal/sales-exceptions",
            handler.Request.RequestUri!.ToString());
        Assert.Equal(
            GatewayKey,
            Assert.Single(handler.Request.Headers.GetValues(SalesExceptionGatewayForwarder.GatewayKeyHeader)));
    }

    [Fact]
    public async Task Forward_SendsTrustedActorCamelCase()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler);

        await forwarder.CreateAsync(SampleCreate());

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var root = payload.RootElement;
        Assert.Equal("mgr-najaf", root.GetProperty("requesterUserName").GetString());
        Assert.Equal("11", root.GetProperty("requesterUserId").GetString());
        Assert.Equal(SalesRoles.UserTypeSalesManager, root.GetProperty("role").GetString());
        Assert.Equal("najaf-demo", root.GetProperty("cityValue").GetString());
    }

    [Fact]
    public async Task Forward_GatewayUnauthorized_MapsTo502()
    {
        var handler = new CapturingHandler(HttpStatusCode.Unauthorized);
        var forwarder = CreateForwarder(handler);

        var result = await forwarder.CreateAsync(SampleCreate());

        Assert.False(result.Ok);
        Assert.Equal(StatusCodes.Status502BadGateway, result.FailureStatusCode);
        Assert.NotEqual(StatusCodes.Status401Unauthorized, result.FailureStatusCode);
    }

    [Fact]
    public async Task Forward_Unreachable_MapsTo503()
    {
        var handler = new CapturingHandler(toThrow: new HttpRequestException("down"));
        var forwarder = CreateForwarder(handler);

        var result = await forwarder.CreateAsync(SampleCreate());

        Assert.False(result.Ok);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.FailureStatusCode);
    }

    [Fact]
    public async Task Forward_MissingConfig_Is503AndSendsNothing()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler, baseUrl: null, internalApiKey: null);

        var result = await forwarder.CreateAsync(SampleCreate());

        Assert.False(result.Ok);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.FailureStatusCode);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task List_GetsInternalPathWithRequesterScope()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler);

        var result = await forwarder.ListAsync(new SalesExceptionListForwardRequest
        {
            Status = "Pending",
            CityValue = "najaf-demo",
            Page = 1,
            PageSize = 20,
            RequesterUserName = "mgr-najaf"
        });

        Assert.True(result.Ok);
        Assert.Equal(HttpMethod.Get, handler.Request!.Method);
        Assert.Contains("internal/sales-exceptions", handler.Request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Contains("requesterUserName=mgr-najaf", handler.Request.RequestUri.Query, StringComparison.Ordinal);
        Assert.Equal(
            GatewayKey,
            Assert.Single(handler.Request.Headers.GetValues(SalesExceptionGatewayForwarder.GatewayKeyHeader)));
    }
}
