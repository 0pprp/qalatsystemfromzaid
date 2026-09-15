using System.Net;
using System.Text.Json;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

/// <summary>
/// The delegate app holds no gateway credential, so this branch is the only thing that may
/// present the shared internal key to the central inbox.
/// </summary>
public sealed class CentralComplaintForwarderTests
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
                Content = new StringContent("{\"id\":\"00000000-0000-0000-0000-000000000001\"}")
            };
        }
    }

    private static CentralComplaintForwarder CreateForwarder(
        CapturingHandler handler,
        string? baseUrl = "http://127.0.0.1:5280/api/",
        string? internalApiKey = GatewayKey,
        string? salesGatewayApiKey = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SalesGateway:BaseUrl"] = baseUrl,
                ["InternalApiKey"] = internalApiKey,
                ["SalesGateway:ApiKey"] = salesGatewayApiKey
            })
            .Build();

        return new CentralComplaintForwarder(
            new HttpClient(handler),
            configuration,
            NullLogger<CentralComplaintForwarder>.Instance);
    }

    private static CentralComplaintForwardRequest DelegateComplaint() => new()
    {
        Message = "شكوى المندوب حول تأخر التسليم",
        SourceApp = "delegate_application",
        SourceType = "complaint",
        CityValue = "najaf-demo",
        CityName = "النجف - DEMO",
        SenderUserId = "7",
        SenderUserName = "delegate-42",
        SenderDisplayName = "مندوب الشمالية",
        SenderRole = "مندوب"
    };

    [Fact]
    public async Task Forward_PostsToInternalComplaintsWithGatewayKeyHeader()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler);

        var result = await forwarder.ForwardAsync(DelegateComplaint());

        Assert.True(result.Ok);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://127.0.0.1:5280/api/internal/complaints", handler.Request.RequestUri!.ToString());
        Assert.Equal(
            GatewayKey,
            Assert.Single(handler.Request.Headers.GetValues(CentralComplaintForwarder.GatewayKeyHeader)));
    }

    [Fact]
    public async Task Forward_SendsCamelCaseTrustedSenderFields()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler);

        await forwarder.ForwardAsync(DelegateComplaint());

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var root = payload.RootElement;
        Assert.Equal("شكوى المندوب حول تأخر التسليم", root.GetProperty("message").GetString());
        Assert.Equal("delegate_application", root.GetProperty("sourceApp").GetString());
        Assert.Equal("complaint", root.GetProperty("sourceType").GetString());
        Assert.Equal("najaf-demo", root.GetProperty("cityValue").GetString());
        Assert.Equal("مندوب الشمالية", root.GetProperty("senderDisplayName").GetString());
        Assert.Equal("مندوب", root.GetProperty("senderRole").GetString());
        Assert.Equal("delegate-42", root.GetProperty("senderUserName").GetString());
    }

    [Fact]
    public async Task Forward_NeverLeaksTheAsyncIdCredential()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler);

        await forwarder.ForwardAsync(DelegateComplaint());

        Assert.DoesNotContain("asyncId", handler.RequestBody!, StringComparison.OrdinalIgnoreCase);
        Assert.False(handler.Request!.Headers.Contains(DelegateComplaintAuth.AsyncIdHeader));
    }

    [Fact]
    public async Task Forward_FallsBackToSalesGatewayApiKey()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler, internalApiKey: null, salesGatewayApiKey: "fallback-key");

        await forwarder.ForwardAsync(DelegateComplaint());

        Assert.Equal(
            "fallback-key",
            Assert.Single(handler.Request!.Headers.GetValues(CentralComplaintForwarder.GatewayKeyHeader)));
    }

    [Fact]
    public async Task Forward_AppendsMissingTrailingSlashOnBaseUrl()
    {
        var handler = new CapturingHandler();
        var forwarder = CreateForwarder(handler, baseUrl: "http://169.58.236.52:8080/sales-gw/api");

        await forwarder.ForwardAsync(DelegateComplaint());

        Assert.Equal(
            "http://169.58.236.52:8080/sales-gw/api/internal/complaints",
            handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Forward_WithoutConfiguration_Is503AndSendsNothing()
    {
        var handler = new CapturingHandler();
        var missingKey = CreateForwarder(handler, internalApiKey: null);
        var missingBaseUrl = CreateForwarder(handler, baseUrl: null);

        var noKey = await missingKey.ForwardAsync(DelegateComplaint());
        var noBaseUrl = await missingBaseUrl.ForwardAsync(DelegateComplaint());

        Assert.False(noKey.Ok);
        Assert.False(noBaseUrl.Ok);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, noKey.FailureStatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, noBaseUrl.FailureStatusCode);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Forward_RejectedByCentralInbox_Is502()
    {
        var forwarder = CreateForwarder(new CapturingHandler(HttpStatusCode.Unauthorized));

        var result = await forwarder.ForwardAsync(DelegateComplaint());

        Assert.False(result.Ok);
        Assert.Equal(StatusCodes.Status502BadGateway, result.FailureStatusCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Forward_TransportFailure_Is503()
    {
        var unreachable = CreateForwarder(new CapturingHandler(toThrow: new HttpRequestException("no route")));
        var timedOut = CreateForwarder(new CapturingHandler(toThrow: new TaskCanceledException("timeout")));

        var unreachableResult = await unreachable.ForwardAsync(DelegateComplaint());
        var timeoutResult = await timedOut.ForwardAsync(DelegateComplaint());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unreachableResult.FailureStatusCode);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, timeoutResult.FailureStatusCode);
    }

    [Fact]
    public void ForwardRequest_CarriesNoClientSuppliedIdentityFields()
    {
        var props = typeof(CentralComplaintForwardRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("SenderDisplayName", props);
        Assert.Contains("SenderRole", props);
        Assert.DoesNotContain("AsyncId", props);
        Assert.DoesNotContain("LinkDelegate", props);
    }
}
