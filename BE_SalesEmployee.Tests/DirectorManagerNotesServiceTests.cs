using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class DirectorManagerNotesServiceTests
{
    [Fact]
    public async Task CreateTrusted_Rejects_Short_Body()
    {
        var svc = new DirectorManagerNotesService(new InMemoryDirectorManagerNoteStore());
        var result = await svc.CreateTrustedAsync(
            new TrustedDirectorNoteSender { SenderDisplayName = "مندوب", SenderRole = "مندوب" },
            new CreateDirectorNoteInput { Body = "قصير", SourceApp = "delegate_application" });

        Assert.False(result.Ok);
        Assert.Contains("قصير", result.Error!);
    }

    [Fact]
    public async Task CreateTrusted_Then_Inbox_And_MarkRead()
    {
        var store = new InMemoryDirectorManagerNoteStore();
        var svc = new DirectorManagerNotesService(store);

        var created = await svc.CreateTrustedAsync(
            new TrustedDirectorNoteSender
            {
                SenderUserId = "7",
                SenderDisplayName = "علي",
                SenderRole = "مندوب",
                CityValue = "baghdad",
                CityName = "بغداد"
            },
            new CreateDirectorNoteInput
            {
                Body = "ملاحظة تجريبية للمدير حيدر هنا",
                SourceApp = "delegate_application",
                SourceType = "manager_note"
            });

        Assert.True(created.Ok);
        Assert.NotNull(created.Note);

        var unread = await svc.UnreadCountAsync();
        Assert.Equal(1, unread);

        var inbox = await svc.InboxAsync(new DirectorNoteInboxQuery { UnreadOnly = true });
        Assert.Equal(1, inbox.TotalCount);

        var marked = await svc.MarkReadAsync(created.Note!.Id);
        Assert.NotNull(marked);
        Assert.Equal(0, await svc.UnreadCountAsync());
    }
}
