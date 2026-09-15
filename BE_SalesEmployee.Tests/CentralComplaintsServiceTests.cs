using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CentralComplaintsServiceTests
{
    private static CentralComplaintsService CreateService() =>
        new(new InMemoryCentralComplaintsStore());

    private static GatewayUser Sender(
        string userName = "sales1",
        string userType = SalesRoles.UserTypeSalesEmployee,
        string cityValue = "najaf-demo",
        string cityName = "النجف - DEMO") => new()
    {
        UserID = "17",
        UserName = userName,
        UserType = userType,
        CityValue = cityValue,
        CityName = cityName
    };

    private static CreateComplaintInput Input(string subject = "شكوى", string body = "تفاصيل الشكوى") => new()
    {
        Subject = subject,
        Body = body
    };

    [Fact]
    public async Task Create_StampsIdentityFromPrincipal_NotFromBody()
    {
        var service = CreateService();

        var result = await service.CreateAsync(Sender(), new CreateComplaintInput
        {
            Subject = "  تأخير التسليم  ",
            Body = "  لم يصل الطلب  ",
            SourceType = "delivery"
        });

        Assert.True(result.Ok);
        var complaint = result.Complaint!;
        Assert.Equal("تأخير التسليم", complaint.Subject);
        Assert.Equal("لم يصل الطلب", complaint.Body);
        Assert.Equal("sales1", complaint.SenderUserName);
        Assert.Equal("17", complaint.SenderUserId);
        Assert.Equal(SalesRoles.SalesEmployee, complaint.SenderRole);
        Assert.Equal("najaf-demo", complaint.CityValue);
        Assert.Equal(CentralComplaintsService.DefaultSourceApp, complaint.SourceApp);
        Assert.Equal(ComplaintStatuses.Unread, complaint.Status);
        Assert.Null(complaint.ReadAtUtc);
    }

    [Fact]
    public async Task Create_RejectsMissingSubjectOrBody()
    {
        var service = CreateService();

        var noSubject = await service.CreateAsync(Sender(), new CreateComplaintInput { Subject = "  ", Body = "نص" });
        var noBody = await service.CreateAsync(Sender(), new CreateComplaintInput { Subject = "عنوان", Body = "" });

        Assert.False(noSubject.Ok);
        Assert.False(noBody.Ok);
        Assert.Equal(0, (await service.InboxAsync(new ComplaintInboxQuery())).TotalCount);
    }

    [Fact]
    public async Task Create_TruncatesOverlongSubjectToColumnWidth()
    {
        var service = CreateService();

        var result = await service.CreateAsync(Sender(), new CreateComplaintInput
        {
            Subject = new string('ش', CentralComplaintsService.MaxSubjectLength + 50),
            Body = "نص"
        });

        Assert.True(result.Ok);
        Assert.Equal(CentralComplaintsService.MaxSubjectLength, result.Complaint!.Subject.Length);
    }

    [Fact]
    public async Task Inbox_PagesNewestFirst()
    {
        var service = CreateService();
        for (var i = 1; i <= 25; i++)
        {
            await service.CreateAsync(Sender(), Input($"شكوى {i}"));
        }

        var firstPage = await service.InboxAsync(new ComplaintInboxQuery { Page = 1, PageSize = 10 });
        var lastPage = await service.InboxAsync(new ComplaintInboxQuery { Page = 3, PageSize = 10 });

        Assert.Equal(25, firstPage.TotalCount);
        Assert.Equal(3, firstPage.TotalPages);
        Assert.Equal(10, firstPage.Items.Count);
        Assert.Equal("شكوى 25", firstPage.Items[0].Subject);
        Assert.Equal(5, lastPage.Items.Count);
        Assert.Equal("شكوى 1", lastPage.Items[^1].Subject);
    }

    [Fact]
    public async Task Inbox_ClampsPageSizeToMaximum()
    {
        var service = CreateService();
        await service.CreateAsync(Sender(), Input());

        var page = await service.InboxAsync(new ComplaintInboxQuery { Page = 0, PageSize = 5000 });

        Assert.Equal(1, page.Page);
        Assert.Equal(PagingDefaults.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task Inbox_UnreadOnlyFilter_ExcludesReadRows()
    {
        var service = CreateService();
        var first = (await service.CreateAsync(Sender(), Input("أولى"))).Complaint!;
        await service.CreateAsync(Sender(), Input("ثانية"));

        await service.MarkReadAsync(first.Id);
        var unread = await service.InboxAsync(new ComplaintInboxQuery { UnreadOnly = true });
        var all = await service.InboxAsync(new ComplaintInboxQuery());

        Assert.Equal(1, unread.TotalCount);
        Assert.Equal("ثانية", unread.Items[0].Subject);
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task Inbox_FiltersByCityAndSourceAppAndSearch()
    {
        var service = CreateService();
        await service.CreateAsync(Sender(cityValue: "najaf-demo"), new CreateComplaintInput
        {
            Subject = "مشكلة في التوصيل",
            Body = "الطلب متأخر",
            SourceApp = "delegate-app"
        });
        await service.CreateAsync(Sender(cityValue: "basra-demo"), new CreateComplaintInput
        {
            Subject = "مشكلة في الفاتورة",
            Body = "مبلغ خاطئ",
            SourceApp = "sales-app"
        });

        var byCity = await service.InboxAsync(new ComplaintInboxQuery { CityValue = "basra-demo" });
        var bySource = await service.InboxAsync(new ComplaintInboxQuery { SourceApp = "delegate-app" });
        var bySearch = await service.InboxAsync(new ComplaintInboxQuery { Search = "الفاتورة" });

        Assert.Equal("مشكلة في الفاتورة", Assert.Single(byCity.Items).Subject);
        Assert.Equal("مشكلة في التوصيل", Assert.Single(bySource.Items).Subject);
        Assert.Equal("مشكلة في الفاتورة", Assert.Single(bySearch.Items).Subject);
    }

    [Fact]
    public async Task GetById_ReturnsNullForUnknownId()
    {
        var service = CreateService();

        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkRead_IsIdempotentAndKeepsFirstTimestamp()
    {
        var service = CreateService();
        var created = (await service.CreateAsync(Sender(), Input())).Complaint!;

        var first = await service.MarkReadAsync(created.Id);
        var second = await service.MarkReadAsync(created.Id);

        Assert.Equal(ComplaintStatuses.Read, first!.Status);
        Assert.NotNull(first.ReadAtUtc);
        Assert.Equal(first.ReadAtUtc, second!.ReadAtUtc);
        Assert.Equal(0, await service.UnreadCountAsync());
    }

    [Fact]
    public async Task MarkRead_ReturnsNullForUnknownId()
    {
        var service = CreateService();

        Assert.Null(await service.MarkReadAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task MarkAllRead_ClearsUnreadCount()
    {
        var service = CreateService();
        await service.CreateAsync(Sender(), Input("1"));
        await service.CreateAsync(Sender(), Input("2"));
        await service.CreateAsync(Sender(), Input("3"));

        Assert.Equal(3, await service.UnreadCountAsync());
        Assert.Equal(3, await service.MarkAllReadAsync());
        Assert.Equal(0, await service.UnreadCountAsync());
        Assert.Equal(0, await service.MarkAllReadAsync());
    }

    [Fact]
    public async Task SenderRole_RecordsDelegatedManagerAndFallsBackToRawUserType()
    {
        var service = CreateService();

        var dm = await service.CreateAsync(
            Sender(userName: "dm", userType: SalesRoles.UserTypeDelegatedManager), Input("من المفوض"));
        var branchManager = await service.CreateAsync(
            Sender(userName: "bm", userType: "مدير فرع"), Input("من مدير الفرع"));

        Assert.Equal(SalesRoles.DelegatedManager, dm.Complaint!.SenderRole);
        Assert.Equal("مدير فرع", branchManager.Complaint!.SenderRole);
    }

    [Fact]
    public void Intake_IsOpenToAnyAuthenticatedUser_WhileInboxIsDelegatedManagerOnly()
    {
        var intake = typeof(BE_SalesEmployee.Controllers.ComplaintsController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();
        var inbox = typeof(BE_SalesEmployee.Controllers.DelegatedManagerController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        Assert.True(string.IsNullOrEmpty(intake.Policy));
        Assert.Equal(SalesPolicies.DelegatedManager, inbox.Policy);
    }

    [Fact]
    public async Task ConcurrentCreates_AreAllPersisted()
    {
        var service = CreateService();

        await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(i => service.CreateAsync(Sender(), Input($"شكوى {i}"))));

        Assert.Equal(50, (await service.InboxAsync(new ComplaintInboxQuery { PageSize = 100 })).TotalCount);
        Assert.Equal(50, await service.UnreadCountAsync());
    }
}
