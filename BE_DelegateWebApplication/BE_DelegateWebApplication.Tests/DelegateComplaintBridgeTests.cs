using BE_DelegateWebApplication.Controllers;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

/// <summary>
/// End-to-end shape of the bridge: authenticate the delegate, keep the local audit row,
/// then hand a server-resolved identity to the central inbox.
/// </summary>
public sealed class DelegateComplaintBridgeTests
{
    private const string AsyncId = "secret-north";

    private sealed class StubDelegates : IDelegateRepository
    {
        public Task<DelegateGetDTO?> GetDelegateLogin(string? asyncID) =>
            Task.FromResult<DelegateGetDTO?>(
                string.Equals(asyncID, AsyncId, StringComparison.Ordinal)
                    ? new DelegateGetDTO
                    {
                        DelegateId = 42,
                        UserId = 7,
                        CityId = 3,
                        DelegateName = "مندوب الشمالية",
                        AsyncId = AsyncId
                    }
                    : null);

        public Task<DelegateGetDTO?> GetDelegateCheckLogout(string? asyncID) => GetDelegateLogin(asyncID);

        public Task<DelegateInfoGetDTO?> GetDelegateTitle(int? delegateId) =>
            Task.FromResult<DelegateInfoGetDTO?>(null);

        public Task<IEnumerable<SelectDelegateGetDTO>?> GetDelegateSelect(int? delegateId) =>
            Task.FromResult<IEnumerable<SelectDelegateGetDTO>?>(null);

        public Task<IEnumerable<SelectDelegateGetDTO>?> GetFollowerCityLists(int followerUserId) =>
            Task.FromResult<IEnumerable<SelectDelegateGetDTO>?>(null);

        public Task<bool> IsFollowerListLinked(int fatherId, int childId) => Task.FromResult(false);
    }

    private sealed class RecordingLocalComplaints : IDelegateComplaintsService
    {
        public int Calls { get; private set; }
        public string? MessageText { get; private set; }
        public int DelegateId { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<DelegateComplaintCreateResult> CreateAsync(
            int delegateId,
            int? userId,
            string? senderDisplayName,
            int? cityId,
            string? branchLink,
            string messageText,
            CancellationToken ct = default)
        {
            Calls++;
            DelegateId = delegateId;
            MessageText = messageText;
            if (messageText.Trim().Length < DelegateComplaintsService.MinMessageLength)
            {
                throw new ArgumentException("نص الشكوى قصير جدًا");
            }

            return Task.FromResult(new DelegateComplaintCreateResult
            {
                Id = 501,
                CreatedAtUtc = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc)
            });
        }
    }

    private sealed class RecordingForwarder : ICentralComplaintForwarder
    {
        private readonly CentralComplaintForwardResult _result;

        public RecordingForwarder(CentralComplaintForwardResult? result = null)
        {
            _result = result ?? CentralComplaintForwardResult.Success();
        }

        public CentralComplaintForwardRequest? Forwarded { get; private set; }

        public Task<CentralComplaintForwardResult> ForwardAsync(
            CentralComplaintForwardRequest request,
            CancellationToken ct = default)
        {
            Forwarded = request;
            return Task.FromResult(_result);
        }
    }

    private static DelegateComplaintsController CreateController(
        RecordingLocalComplaints local,
        RecordingForwarder forwarder,
        string? asyncIdHeader = AsyncId)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Branch:CityValue"] = "najaf-demo",
                ["Branch:CityName"] = "النجف - DEMO"
            })
            .Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("najaf-demo.example.com");
        if (asyncIdHeader != null)
        {
            httpContext.Request.Headers[DelegateComplaintAuth.AsyncIdHeader] = asyncIdHeader;
        }

        return new DelegateComplaintsController(new StubDelegates(), local, forwarder, configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static DelegateComplaintsController.CreateComplaintBody ValidBody() =>
        new() { Message = "شكوى المندوب حول تأخر التسليم" };

    [Fact]
    public async Task Create_PersistsLocallyThenForwardsToCentralInbox()
    {
        var local = new RecordingLocalComplaints();
        var forwarder = new RecordingForwarder();
        var controller = CreateController(local, forwarder);

        var response = await controller.Create(ValidBody(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(1, local.Calls);
        Assert.Equal(42, local.DelegateId);
        Assert.NotNull(forwarder.Forwarded);
    }

    [Fact]
    public async Task Create_SendsServerResolvedIdentityNotClientClaims()
    {
        var forwarder = new RecordingForwarder();
        var controller = CreateController(new RecordingLocalComplaints(), forwarder);

        await controller.Create(ValidBody(), CancellationToken.None);

        var sent = forwarder.Forwarded!;
        Assert.Equal("مندوب الشمالية", sent.SenderDisplayName);
        Assert.Equal(DelegateComplaintsController.DelegateRole, sent.SenderRole);
        Assert.Equal("delegate-42", sent.SenderUserName);
        Assert.Equal("7", sent.SenderUserId);
        Assert.Equal("najaf-demo", sent.CityValue);
        Assert.Equal("النجف - DEMO", sent.CityName);
        Assert.Equal(DelegateComplaintsController.SourceApp, sent.SourceApp);
        Assert.Equal(DelegateComplaintsController.SourceType, sent.SourceType);
    }

    [Fact]
    public async Task Create_ReportsForwardFailureInsteadOfClaimingSuccess()
    {
        var forwarder = new RecordingForwarder(
            CentralComplaintForwardResult.Failure(StatusCodes.Status502BadGateway, "تعذر تسليم الشكوى"));
        var controller = CreateController(new RecordingLocalComplaints(), forwarder);

        var response = await controller.Create(ValidBody(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    [Fact]
    public async Task Create_GatewayUnavailable_Is503()
    {
        var forwarder = new RecordingForwarder(
            CentralComplaintForwardResult.Failure(StatusCodes.Status503ServiceUnavailable, "تعذر الاتصال"));
        var controller = CreateController(new RecordingLocalComplaints(), forwarder);

        var response = await controller.Create(ValidBody(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task Create_UnauthenticatedDelegate_NeverReachesCentralInbox()
    {
        var local = new RecordingLocalComplaints();
        var forwarder = new RecordingForwarder();
        var controller = CreateController(local, forwarder, asyncIdHeader: "wrong-credential");

        var response = await controller.Create(ValidBody(), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        Assert.Equal(0, local.Calls);
        Assert.Null(forwarder.Forwarded);
    }

    [Fact]
    public async Task Create_InvalidMessage_Is400AndNeverForwards()
    {
        var forwarder = new RecordingForwarder();
        var controller = CreateController(new RecordingLocalComplaints(), forwarder);

        var response = await controller.Create(
            new DelegateComplaintsController.CreateComplaintBody { Message = "قصير" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Null(forwarder.Forwarded);
    }
}
