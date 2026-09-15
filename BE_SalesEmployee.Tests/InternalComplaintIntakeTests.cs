using BE_SalesEmployee.Controllers;
using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// The delegate mobile app has no gateway JWT, so its complaints arrive through the branch backend
/// on api/internal/complaints. Only the shared internal key may open that door.
/// </summary>
public sealed class InternalComplaintIntakeTests
{
    private const string ValidKey = "SalesEmployee-Gateway-2026";

    private static IConfiguration Configuration(string? internalApiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["InternalApiKey"] = internalApiKey })
            .Build();

    private static (InternalComplaintsController Controller, CentralComplaintsService Service) CreateController(
        string? headerKey,
        string? configuredKey = ValidKey)
    {
        var service = new CentralComplaintsService(new InMemoryCentralComplaintsStore());
        var controller = new InternalComplaintsController(service, Configuration(configuredKey))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (headerKey != null)
        {
            controller.Request.Headers[InternalComplaintsController.GatewayKeyHeader] = headerKey;
        }
        return (controller, service);
    }

    private static InternalComplaintsController.TrustedComplaintRequest DelegateRequest(
        string message = "الشكوى الخاصة بالمندوب حول تأخر التسليم") => new()
    {
        Message = message,
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
    public async Task MissingGatewayKey_Is401_AndStoresNothing()
    {
        var (controller, service) = CreateController(headerKey: null);

        var response = await controller.Create(DelegateRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(0, (await service.InboxAsync(new ComplaintInboxQuery())).TotalCount);
    }

    [Fact]
    public async Task WrongGatewayKey_Is401_AndStoresNothing()
    {
        var (controller, service) = CreateController(headerKey: "not-the-key");

        var response = await controller.Create(DelegateRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(0, (await service.InboxAsync(new ComplaintInboxQuery())).TotalCount);
    }

    [Fact]
    public async Task UnconfiguredServerKey_FailsClosed()
    {
        var (controller, service) = CreateController(headerKey: "", configuredKey: "");

        var response = await controller.Create(DelegateRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(0, (await service.InboxAsync(new ComplaintInboxQuery())).TotalCount);
    }

    [Fact]
    public async Task ValidGatewayKey_CreatesComplaintFromTrustedSender()
    {
        var (controller, service) = CreateController(headerKey: ValidKey);

        var response = await controller.Create(DelegateRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        var stored = Assert.Single((await service.InboxAsync(new ComplaintInboxQuery())).Items);
        Assert.Equal("delegate_application", stored.SourceApp);
        Assert.Equal("complaint", stored.SourceType);
        Assert.Equal("مندوب الشمالية", stored.SenderDisplayName);
        Assert.Equal("مندوب", stored.SenderRole);
        Assert.Equal("7", stored.SenderUserId);
        Assert.Equal("najaf-demo", stored.CityValue);
        Assert.Equal(CentralComplaintsService.DefaultTrustedSubject, stored.Subject);
        Assert.Equal(ComplaintStatuses.Unread, stored.Status);
    }

    [Fact]
    public async Task ValidGatewayKey_RejectsTooShortBodyWith400()
    {
        var (controller, service) = CreateController(headerKey: ValidKey);

        var response = await controller.Create(DelegateRequest("قصيرة"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(0, (await service.InboxAsync(new ComplaintInboxQuery())).TotalCount);
    }

    [Fact]
    public async Task BodyFieldIsAcceptedWhenMessageIsAbsent()
    {
        var (controller, service) = CreateController(headerKey: ValidKey);

        var response = await controller.Create(new InternalComplaintsController.TrustedComplaintRequest
        {
            Body = "شكوى مرسلة عبر الحقل البديل body",
            SenderDisplayName = "مندوب الشمالية",
            SenderRole = "مندوب"
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        Assert.Equal(1, (await service.InboxAsync(new ComplaintInboxQuery())).TotalCount);
    }

    [Fact]
    public void PublicComplaintsRouteStaysAuthenticated()
    {
        Assert.Single(typeof(ComplaintsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.Empty(typeof(ComplaintsController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    [Fact]
    public async Task CreateTrusted_TakesIdentityOnlyFromTrustedSender()
    {
        var service = new CentralComplaintsService(new InMemoryCentralComplaintsStore());

        var result = await service.CreateTrustedAsync(
            new TrustedComplaintSender
            {
                SenderUserId = " 7 ",
                SenderUserName = " delegate-42 ",
                SenderDisplayName = " مندوب الشمالية ",
                SenderRole = "مندوب",
                CityValue = " najaf-demo ",
                CityName = " النجف - DEMO "
            },
            new CreateComplaintInput
            {
                Body = "شكوى طويلة بما يكفي للتحقق",
                // A branch may not override identity through the generic input fields.
                CityValue = "basra-demo",
                CityName = "البصرة"
            });

        Assert.True(result.Ok);
        Assert.Equal("7", result.Complaint!.SenderUserId);
        Assert.Equal("delegate-42", result.Complaint.SenderUserName);
        Assert.Equal("مندوب الشمالية", result.Complaint.SenderDisplayName);
        Assert.Equal("najaf-demo", result.Complaint.CityValue);
        Assert.Equal("النجف - DEMO", result.Complaint.CityName);
    }

    [Fact]
    public async Task CreateTrusted_EnforcesBodyLengthBounds()
    {
        var service = new CentralComplaintsService(new InMemoryCentralComplaintsStore());
        var sender = new TrustedComplaintSender { SenderDisplayName = "مندوب", SenderRole = "مندوب" };

        var tooShort = await service.CreateTrustedAsync(sender, new CreateComplaintInput
        {
            Body = new string('ا', CentralComplaintsService.MinBodyLength - 1)
        });
        var tooLong = await service.CreateTrustedAsync(sender, new CreateComplaintInput
        {
            Body = new string('ا', CentralComplaintsService.MaxBodyLength + 1)
        });
        var atBounds = await service.CreateTrustedAsync(sender, new CreateComplaintInput
        {
            Body = new string('ا', CentralComplaintsService.MinBodyLength)
        });

        Assert.False(tooShort.Ok);
        Assert.False(tooLong.Ok);
        Assert.True(atBounds.Ok);
        Assert.Equal(1, (await service.InboxAsync(new ComplaintInboxQuery())).TotalCount);
    }

    [Fact]
    public async Task CreateTrusted_KeepsExplicitSubjectAndDefaultsEmptyOne()
    {
        var service = new CentralComplaintsService(new InMemoryCentralComplaintsStore());
        var sender = new TrustedComplaintSender { SenderDisplayName = "مندوب", SenderRole = "مندوب" };

        var explicitSubject = await service.CreateTrustedAsync(sender, new CreateComplaintInput
        {
            Subject = "تأخير التسليم",
            Body = "شكوى طويلة بما يكفي للتحقق"
        });
        var blankSubject = await service.CreateTrustedAsync(sender, new CreateComplaintInput
        {
            Subject = "   ",
            Body = "شكوى طويلة بما يكفي للتحقق"
        });
        var longSubject = await service.CreateTrustedAsync(sender, new CreateComplaintInput
        {
            Subject = new string('ش', CentralComplaintsService.MaxSubjectLength + 50),
            Body = "شكوى طويلة بما يكفي للتحقق"
        });

        Assert.Equal("تأخير التسليم", explicitSubject.Complaint!.Subject);
        Assert.Equal(CentralComplaintsService.DefaultTrustedSubject, blankSubject.Complaint!.Subject);
        Assert.Equal(CentralComplaintsService.MaxSubjectLength, longSubject.Complaint!.Subject.Length);
    }

    [Fact]
    public async Task CreateTrusted_FallsBackToUnknownRoleAndStripsAngleBrackets()
    {
        var service = new CentralComplaintsService(new InMemoryCentralComplaintsStore());

        var result = await service.CreateTrustedAsync(
            new TrustedComplaintSender(),
            new CreateComplaintInput { Body = "نص <script>alert(1)</script> شكوى" });

        Assert.True(result.Ok);
        Assert.Equal("غير معروف", result.Complaint!.SenderDisplayName);
        Assert.Equal("Unknown", result.Complaint.SenderRole);
        Assert.DoesNotContain('<', result.Complaint.Body);
        Assert.DoesNotContain('>', result.Complaint.Body);
    }
}
