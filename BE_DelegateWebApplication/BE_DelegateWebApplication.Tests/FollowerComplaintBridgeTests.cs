using Xunit;
using BE_DelegateWebApplication.Controllers;
using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerIdentity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace BE_DelegateWebApplication.Tests;

public sealed class FollowerComplaintBridgeTests
{
    private sealed class StubIdentity : IFollowerIdentityService
    {
        public FollowerUserIdentity? User { get; set; }

        public Task<FollowerUserIdentity?> ResolveByAsyncIdAsync(string? asyncId, CancellationToken ct = default) =>
            Task.FromResult(User);

        public Task<FollowerAuthOutcome> ResolveSessionByAsyncIdAsync(string? asyncId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<FollowerAuthOutcome> AuthenticateByCredentialsAsync(string? userName, string? password, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<FollowerUserIdentity>> ListActiveAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class RecordingLocal : IFollowerComplaintsService
    {
        public int? LastUserId { get; private set; }
        public string? LastMessage { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<FollowerComplaintCreateResult> CreateAsync(
            int userId, string? senderDisplayName, int? cityId, string? branchLink, string messageText, CancellationToken ct = default)
        {
            if (messageText.Trim().Length < FollowerComplaintsService.MinMessageLength)
                throw new ArgumentException("short");
            LastUserId = userId;
            LastMessage = messageText;
            return Task.FromResult(new FollowerComplaintCreateResult { Id = 9, CreatedAtUtc = DateTime.UtcNow });
        }
    }

    private sealed class RecordingForwarder : ICentralComplaintForwarder
    {
        private readonly CentralComplaintForwardResult _result;
        public RecordingForwarder(CentralComplaintForwardResult? result = null) =>
            _result = result ?? CentralComplaintForwardResult.Success();

        public CentralComplaintForwardRequest? Forwarded { get; private set; }

        public Task<CentralComplaintForwardResult> ForwardAsync(CentralComplaintForwardRequest request, CancellationToken ct = default)
        {
            Forwarded = request;
            return Task.FromResult(_result);
        }
    }

    private static FollowerComplaintsController Create(
        StubIdentity identity,
        RecordingLocal local,
        RecordingForwarder forwarder,
        string? asyncIdHeader = "secret")
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Branch:CityValue"] = "DatabaseCompanyNajaf",
            ["Branch:CityName"] = "النجف",
        }).Build();
        var controller = new FollowerComplaintsController(identity, local, forwarder, config);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("branch.example");
        if (!string.IsNullOrWhiteSpace(asyncIdHeader))
            http.Request.Headers[FollowerComplaintsController.AsyncIdHeader] = asyncIdHeader;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    [Fact]
    public async Task Create_Forwards_Follower_Identity_To_Central()
    {
        var identity = new StubIdentity
        {
            User = new FollowerUserIdentity
            {
                UserId = 10,
                UserName = "احمد",
                CityId = 1,
                CityName = "النجف",
                IsActive = true,
            }
        };
        var local = new RecordingLocal();
        var forwarder = new RecordingForwarder();
        var controller = Create(identity, local, forwarder);

        var result = await controller.Create(new FollowerComplaintsController.CreateComplaintBody
        {
            Message = "شكوى اختبار من المتابع هنا"
        }, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(10, local.LastUserId);
        Assert.NotNull(forwarder.Forwarded);
        Assert.Equal(FollowerComplaintsController.SourceApp, forwarder.Forwarded!.SourceApp);
        Assert.Equal(FollowerComplaintsController.FollowerRole, forwarder.Forwarded.SenderRole);
        Assert.Equal("احمد", forwarder.Forwarded.SenderDisplayName);
        Assert.Equal("10", forwarder.Forwarded.SenderUserId);
    }

    [Fact]
    public async Task Create_Unauthorized_When_AsyncId_Invalid()
    {
        var identity = new StubIdentity { User = null };
        var controller = Create(identity, new RecordingLocal(), new RecordingForwarder());
        var result = await controller.Create(new FollowerComplaintsController.CreateComplaintBody
        {
            Message = "شكوى اختبار من المتابع هنا"
        }, default);
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Create_Propagates_Central_Failure()
    {
        var identity = new StubIdentity
        {
            User = new FollowerUserIdentity { UserId = 10, UserName = "احمد", IsActive = true }
        };
        var forwarder = new RecordingForwarder(
            CentralComplaintForwardResult.Failure(StatusCodes.Status502BadGateway, "تعذر تسليم الشكوى"));
        var controller = Create(identity, new RecordingLocal(), forwarder);
        var result = await controller.Create(new FollowerComplaintsController.CreateComplaintBody
        {
            Message = "شكوى اختبار من المتابع هنا"
        }, default);
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, obj.StatusCode);
    }
}
