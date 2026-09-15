using System.Security.Claims;
using BE_SalesEmployee.Controllers;
using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Branch BE_Company has no Sales Gateway JWT, so exception create/list arrive through
/// api/internal/sales-exceptions with the shared internal key and a server-asserted actor.
/// </summary>
public sealed class InternalSalesExceptionIntakeTests
{
    private const string ValidKey = "SalesEmployee-Gateway-2026";

    private static IConfiguration Configuration(string? internalApiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalApiKey"] = internalApiKey })
            .Build();

    private static (InternalSalesExceptionsController Controller, SalesExceptionService Service) CreateController(
        string? headerKey,
        string? configuredKey = ValidKey)
    {
        var service = new SalesExceptionService(new InMemorySalesExceptionStore(), new IntentRecordingBranchNotePoster());
        var controller = new InternalSalesExceptionsController(service, Configuration(configuredKey))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (headerKey != null)
        {
            controller.Request.Headers[InternalSalesExceptionsController.GatewayKeyHeader] = headerKey;
        }

        return (controller, service);
    }

    private static InternalSalesExceptionsController.TrustedExceptionRequest ManagerRequest(
        string reason = "العميل متعاون ويستحق استثناء من المدير المفوض",
        string requesterUserName = "mgr-najaf") => new()
    {
        CityValue = "najaf-demo",
        CityName = "النجف - DEMO",
        CustomerId = 12,
        CustomerName = "عميل تجريبي",
        CustomerPhone = "07xx",
        SalesRequestId = 99,
        Reason = reason,
        TargetApproverType = TargetApproverTypes.DelegatedManager,
        RequesterUserId = "11",
        RequesterUserName = requesterUserName,
        RequesterDisplayName = "مدير مبيعات النجف",
        Role = SalesRoles.UserTypeSalesManager
    };

    [Fact]
    public async Task Post_MissingGatewayKey_Is401_AndStoresNothing()
    {
        var (controller, service) = CreateController(headerKey: null);

        var response = await controller.Create(ManagerRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(0, (await service.ListAsync(new SalesExceptionQuery())).TotalCount);
    }

    [Fact]
    public async Task Get_MissingGatewayKey_Is401()
    {
        var (controller, _) = CreateController(headerKey: null);

        var response = await controller.List(
            status: null,
            cityValue: null,
            page: null,
            pageSize: null,
            requesterUserName: "mgr-najaf",
            ct: CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response);
    }

    [Fact]
    public async Task Post_WrongGatewayKey_Is401_AndStoresNothing()
    {
        var (controller, service) = CreateController(headerKey: "not-the-key");

        var response = await controller.Create(ManagerRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(0, (await service.ListAsync(new SalesExceptionQuery())).TotalCount);
    }

    [Fact]
    public async Task ValidTrustedPost_CreatesPendingException()
    {
        var (controller, service) = CreateController(headerKey: ValidKey);

        var response = await controller.Create(ManagerRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        var stored = Assert.Single((await service.ListAsync(new SalesExceptionQuery())).Items);
        Assert.Equal(ExceptionStatuses.Pending, stored.Status);
        Assert.Equal("mgr-najaf", stored.RequestingManagerUserName);
        Assert.Equal("najaf-demo", stored.CityValue);
        Assert.Equal("العميل متعاون ويستحق استثناء من المدير المفوض", stored.Reason);
    }

    [Fact]
    public async Task ValidTrustedGet_ReturnsOnlyScopedManagerRequests()
    {
        var (controller, service) = CreateController(headerKey: ValidKey);
        Assert.True((await service.CreateAsync(
            new GatewayUser
            {
                UserID = "1",
                UserName = "mgr-najaf",
                UserType = SalesRoles.UserTypeSalesManager,
                CityValue = "najaf-demo"
            },
            new CreateSalesExceptionInput { Reason = "سبب أ لمدير النجف", CityValue = "najaf-demo" })).Ok);
        Assert.True((await service.CreateAsync(
            new GatewayUser
            {
                UserID = "2",
                UserName = "mgr-basra",
                UserType = SalesRoles.UserTypeSalesManager,
                CityValue = "basra-demo"
            },
            new CreateSalesExceptionInput { Reason = "سبب ب لمدير البصرة", CityValue = "basra-demo" })).Ok);

        var response = await controller.List(
            status: null,
            cityValue: null,
            page: 1,
            pageSize: 50,
            requesterUserName: "mgr-najaf",
            ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
        var item = doc.RootElement.GetProperty("items")[0];
        var display = item.TryGetProperty("RequestingManagerDisplayName", out var pascal)
            ? pascal.GetString()
            : item.GetProperty("requestingManagerDisplayName").GetString();
        Assert.Equal("mgr-najaf", display);
    }

    [Fact]
    public async Task TrustedPost_UsesServerActor_NotBodySpoofFieldsAlone()
    {
        // Identity fields on the trusted body are supplied by BE_Company after local auth —
        // they become the store actor. A browser never reaches this route.
        var (controller, service) = CreateController(headerKey: ValidKey);
        var request = ManagerRequest(requesterUserName: "trusted-from-branch");
        request.RequesterDisplayName = "من الفرع";

        var response = await controller.Create(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        var stored = Assert.Single((await service.ListAsync(new SalesExceptionQuery())).Items);
        Assert.Equal("trusted-from-branch", stored.RequestingManagerUserName);
    }
}
