using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DirectorAssignedTaskServiceTests : IDisposable
{
    private readonly FakeEnv _env = new();
    private readonly InMemoryDirectorAssignedTaskStore _store = new();
    private readonly DirectorAssignedTaskFileStorage _files;
    private readonly DirectorAssignedTaskService _sut;

    public DirectorAssignedTaskServiceTests()
    {
        _files = new DirectorAssignedTaskFileStorage(
            _env,
            NullLogger<DirectorAssignedTaskFileStorage>.Instance);
        _sut = new DirectorAssignedTaskService(_store, _files);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_env.ContentRootPath))
                Directory.Delete(_env.ContentRootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task Direct_BM_accept_complete_sets_completed_and_BranchCompletedAtUtc_only()
    {
        var created = await CreateDirectorTaskAsync(
            title: "bm-direct",
            priority: DirectorAssignedTaskPriority.High,
            due: new DateOnly(2026, 10, 25),
            assigneeKind: DirectorAssignedTaskAssigneeKind.BranchManager,
            assigneeKey: "bm-1",
            assigneeDisplayName: "إدارة الفرع - بغداد");

        Assert.Equal(DirectorAssignedTaskStatus.Sent, created.Status);
        Assert.Equal(DirectorAssignedTaskAssigneeKind.BranchManager, created.CurrentHolderKind);
        Assert.Equal("bm-1", created.CurrentHolderKey);

        var eventsAfterCreate = await _store.ListEventsAsync(created.Id);
        Assert.Contains(eventsAfterCreate, e => e.EventType == "Created");

        var accept = await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1",
            "إدارة الفرع - بغداد",
            CancellationToken.None);
        Assert.True(accept.Ok, accept.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Accepted, accept.Task!.Status);

        var complete = await _sut.CompleteAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1",
            "إدارة الفرع - بغداد",
            CancellationToken.None);
        Assert.True(complete.Ok, complete.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, complete.Task!.Status);
        Assert.NotNull(complete.Task.BranchCompletedAtUtc);
        Assert.Null(complete.Task.DelegatedCompletedAtUtc);
        Assert.Null(complete.Task.DelegatedConfirmedAtUtc);

        var events = await _store.ListEventsAsync(created.Id);
        Assert.Contains(events, e => e.EventType == "Accepted");
        Assert.Contains(events, e => e.EventType == "Completed");
    }

    [Fact]
    public async Task DM_self_accept_complete_sets_completed_and_DelegatedCompletedAtUtc()
    {
        var created = await CreateDirectorTaskAsync(
            title: "dm-self",
            priority: DirectorAssignedTaskPriority.Medium,
            due: new DateOnly(2026, 10, 26),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        var accept = await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None);
        Assert.True(accept.Ok, accept.Error);

        var complete = await _sut.CompleteAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None);
        Assert.True(complete.Ok, complete.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, complete.Task!.Status);
        Assert.NotNull(complete.Task.DelegatedCompletedAtUtc);
        Assert.Null(complete.Task.BranchCompletedAtUtc);
        Assert.Null(complete.Task.DelegatedConfirmedAtUtc);
    }

    [Fact]
    public async Task DM_transfer_BM_complete_then_DM_confirm_sets_both_times_and_transfer_event()
    {
        var created = await CreateDirectorTaskAsync(
            title: "dm-transfer",
            priority: DirectorAssignedTaskPriority.High,
            due: new DateOnly(2026, 10, 27),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None)).Ok);

        var transfer = await _sut.TransferAsync(
            created.Id,
            actorKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            actorKey: "ayman",
            actorDisplayName: "أيمن",
            targetKind: DirectorAssignedTaskAssigneeKind.BranchManager,
            targetKey: "bm-42",
            targetDisplayName: "إدارة الفرع - البصرة",
            note: "نقل للمتابعة الميدانية",
            CancellationToken.None);
        Assert.True(transfer.Ok, transfer.Error);
        Assert.Equal(DirectorAssignedTaskAssigneeKind.BranchManager, transfer.Task!.CurrentHolderKind);
        Assert.Equal("bm-42", transfer.Task.CurrentHolderKey);
        Assert.Equal(DirectorAssignedTaskStatus.Transferred, transfer.Task.Status);
        Assert.NotNull(transfer.Task.TransferredAtUtc);

        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-42",
            "إدارة الفرع - البصرة",
            CancellationToken.None)).Ok);

        var bmComplete = await _sut.CompleteAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-42",
            "إدارة الفرع - البصرة",
            CancellationToken.None);
        Assert.True(bmComplete.Ok, bmComplete.Error);
        Assert.Equal(DirectorAssignedTaskStatus.AwaitingDelegatedConfirm, bmComplete.Task!.Status);
        Assert.NotNull(bmComplete.Task.BranchCompletedAtUtc);
        Assert.Null(bmComplete.Task.DelegatedConfirmedAtUtc);
        Assert.Equal(DirectorAssignedTaskAssigneeKind.DelegatedManager, bmComplete.Task.CurrentHolderKind);
        Assert.Equal("ayman", bmComplete.Task.CurrentHolderKey);

        var confirm = await _sut.ConfirmAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None);
        Assert.True(confirm.Ok, confirm.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, confirm.Task!.Status);
        Assert.NotNull(confirm.Task.BranchCompletedAtUtc);
        Assert.NotNull(confirm.Task.DelegatedConfirmedAtUtc);

        var events = await _store.ListEventsAsync(created.Id);
        var transferEvt = Assert.Single(events, e => e.EventType == "Transferred");
        Assert.Equal("ayman", transferEvt.ActorKey);
        Assert.Equal("bm-42", transferEvt.TargetKey);
        Assert.Contains(events, e => e.EventType == "Confirmed");
    }

    [Fact]
    public async Task Reject_then_UpdateReturned_resends_as_Sent()
    {
        var created = await CreateDirectorTaskAsync(
            title: "reject-me",
            priority: DirectorAssignedTaskPriority.Low,
            due: new DateOnly(2026, 10, 28),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        var reject = await _sut.RejectAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            note: "غير مناسب حالياً",
            CancellationToken.None);
        Assert.True(reject.Ok, reject.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Returned, reject.Task!.Status);
        Assert.Equal("غير مناسب حالياً", reject.Task.RejectNote);

        var eventsAfterReject = await _store.ListEventsAsync(created.Id);
        Assert.Contains(eventsAfterReject, e => e.EventType == "Rejected");

        var updated = await _sut.UpdateReturnedAsync(
            created.Id,
            title: "reject-me-updated",
            description: "وصف محدّث بعد الإعادة",
            dueDate: new DateOnly(2026, 11, 1),
            priority: DirectorAssignedTaskPriority.High,
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن",
            assigneeCityValue: null,
            updatedByUserId: "director-1",
            updatedByUserName: "المدير",
            ct: CancellationToken.None);
        Assert.True(updated.Ok, updated.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Sent, updated.Task!.Status);
        Assert.Equal("reject-me-updated", updated.Task.Title);
        Assert.Equal(DirectorAssignedTaskPriority.High, updated.Task.Priority);
        Assert.Equal(new DateOnly(2026, 11, 1), updated.Task.DueDate);
        Assert.Equal(DirectorAssignedTaskAssigneeKind.DelegatedManager, updated.Task.CurrentHolderKind);
        Assert.Equal("ayman", updated.Task.CurrentHolderKey);

        var events = await _store.ListEventsAsync(created.Id);
        Assert.Contains(events, e => e.EventType == "Updated");
    }

    [Fact]
    public async Task ListForDirector_marks_overdue_while_task_stays_active()
    {
        var iraqToday = DirectorAssignedTaskService.GetIraqToday();
        var overdueDue = iraqToday.AddDays(-2);

        var created = await CreateDirectorTaskAsync(
            title: "overdue-active",
            priority: DirectorAssignedTaskPriority.Medium,
            due: overdueDue,
            assigneeKind: DirectorAssignedTaskAssigneeKind.BranchManager,
            assigneeKey: "bm-1",
            assigneeDisplayName: "إدارة الفرع");

        Assert.Equal(DirectorAssignedTaskStatus.Sent, created.Status);

        var list = await _sut.ListForDirectorAsync(CancellationToken.None);
        var item = Assert.Single(list, x => x.Task.Id == created.Id);
        Assert.True(item.IsOverdue);
        Assert.Equal(DirectorAssignedTaskStatus.Sent, item.Task.Status);
    }

    [Fact]
    public async Task ListForDirector_marks_overdue_while_awaiting_delegated_confirm()
    {
        var pastDueAt = DateTime.UtcNow.AddHours(-2);
        var (ok, error, created) = await _sut.CreateAsync(
            title: "overdue-awaiting-confirm",
            description: null,
            dueDate: DateOnly.FromDateTime(pastDueAt),
            priority: DirectorAssignedTaskPriority.High,
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن",
            assigneeCityValue: null,
            createdByUserId: "director-1",
            createdByUserName: "المدير",
            ct: CancellationToken.None,
            dueAtUtc: pastDueAt);
        Assert.True(ok, error);
        Assert.NotNull(created);

        Assert.True((await _sut.AcceptAsync(
            created!.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None)).Ok);

        Assert.True((await _sut.TransferAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1",
            "إدارة الفرع",
            note: null,
            CancellationToken.None)).Ok);

        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1",
            "إدارة الفرع",
            CancellationToken.None)).Ok);

        var complete = await _sut.CompleteAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1",
            "إدارة الفرع",
            CancellationToken.None);
        Assert.True(complete.Ok, complete.Error);
        Assert.Equal(DirectorAssignedTaskStatus.AwaitingDelegatedConfirm, complete.Task!.Status);

        var list = await _sut.ListForDirectorAsync(CancellationToken.None);
        var item = Assert.Single(list, x => x.Task.Id == created.Id);
        Assert.True(item.IsOverdue);
        Assert.Equal(DirectorAssignedTaskStatus.AwaitingDelegatedConfirm, item.Task.Status);

        Assert.True((await _sut.ConfirmAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None)).Ok);

        var afterConfirm = await _sut.ListForDirectorAsync(CancellationToken.None);
        var done = Assert.Single(afterConfirm, x => x.Task.Id == created.Id);
        Assert.False(done.IsOverdue);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, done.Task.Status);
    }

    [Fact]
    public async Task ListForDirector_sorts_high_before_medium_before_low()
    {
        await CreateDirectorTaskAsync(
            "low",
            DirectorAssignedTaskPriority.Low,
            new DateOnly(2026, 10, 25),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن");
        await CreateDirectorTaskAsync(
            "high",
            DirectorAssignedTaskPriority.High,
            new DateOnly(2026, 10, 30),
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1",
            "إدارة الفرع");
        await CreateDirectorTaskAsync(
            "medium",
            DirectorAssignedTaskPriority.Medium,
            new DateOnly(2026, 10, 20),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن");

        var list = await _sut.ListForDirectorAsync(CancellationToken.None);
        Assert.Equal(
            ["high", "medium", "low"],
            list.Select(x => x.Task.Title).ToArray());
    }

    [Fact]
    public async Task Upload_and_Download_attachment_roundtrip()
    {
        var created = await CreateDirectorTaskAsync(
            title: "with-file",
            priority: DirectorAssignedTaskPriority.Medium,
            due: new DateOnly(2026, 10, 29),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        var file = new FormFileStub(new byte[] { 1, 2, 3, 4 }, "brief.pdf", "application/pdf");
        var upload = await _sut.UploadAttachmentAsync(
            created.Id,
            file,
            uploadedBy: "director-1",
            CancellationToken.None);
        Assert.True(upload.Ok, upload.Error);
        Assert.NotNull(upload.Attachment);
        Assert.Equal("brief.pdf", upload.Attachment!.FileName);
        Assert.False(string.IsNullOrWhiteSpace(upload.Attachment.StoredFileName));

        var download = await _sut.DownloadAttachmentAsync(
            created.Id,
            upload.Attachment.Id,
            CancellationToken.None);
        Assert.True(download.Ok, download.Error);
        Assert.NotNull(download.FullPath);
        Assert.True(File.Exists(download.FullPath));
        Assert.Equal("application/pdf", download.ContentType);
        Assert.Equal("brief.pdf", download.FileName);
    }

    [Fact]
    public async Task Accept_rejects_non_holder()
    {
        var created = await CreateDirectorTaskAsync(
            title: "holder-check",
            priority: DirectorAssignedTaskPriority.High,
            due: new DateOnly(2026, 10, 24),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        var result = await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1",
            "إدارة الفرع",
            CancellationToken.None);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Null(result.Task);
    }

    [Fact]
    public async Task Transfer_keeps_task_in_DM_inbox_as_Transferred_with_attachments()
    {
        var created = await CreateDirectorTaskAsync(
            title: "dm-inbox-after-transfer",
            priority: DirectorAssignedTaskPriority.High,
            due: new DateOnly(2026, 10, 27),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        var file = new FormFileStub(new byte[] { 9, 8, 7 }, "note.pdf", "application/pdf");
        var upload = await _sut.UploadAttachmentAsync(
            created.Id, file, uploadedBy: "director-1", CancellationToken.None);
        Assert.True(upload.Ok, upload.Error);

        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None)).Ok);

        Assert.True((await _sut.TransferAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-42",
            "إدارة الفرع",
            note: null,
            CancellationToken.None)).Ok);

        var dmInbox = await _sut.InboxForDmAsync("ayman", CancellationToken.None);
        var row = Assert.Single(dmInbox, t => t.Id == created.Id);
        Assert.Equal(DirectorAssignedTaskStatus.Transferred, row.Status);
        Assert.Equal(DirectorAssignedTaskAssigneeKind.BranchManager, row.CurrentHolderKind);
        Assert.NotNull(row.TransferredAtUtc);

        var atts = await _sut.ListAttachmentsAsync(created.Id, CancellationToken.None);
        Assert.Single(atts);
        Assert.Equal("note.pdf", atts[0].FileName);

        // BM can accept منقولة
        var accept = await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-42",
            "إدارة الفرع",
            CancellationToken.None);
        Assert.True(accept.Ok, accept.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Accepted, accept.Task!.Status);
    }

    [Fact]
    public async Task Create_with_due_hour_minute_period_roundtrips_DueAtUtc()
    {
        var (ok, error, task) = await _sut.CreateAsync(
            title: "due-time",
            description: null,
            dueDate: new DateOnly(2026, 10, 20),
            priority: DirectorAssignedTaskPriority.Medium,
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن",
            assigneeCityValue: null,
            createdByUserId: "director-1",
            createdByUserName: "المدير",
            dueAtUtc: null,
            dueHour: 10,
            dueMinute: 15,
            duePeriod: "صباح",
            ct: CancellationToken.None);
        Assert.True(ok, error);
        Assert.NotNull(task!.DueAtUtc);
        Assert.Equal(new DateOnly(2026, 10, 20), task.DueDate);
        // Iraq 10:15 صباح = 07:15 UTC
        Assert.Equal(new DateTime(2026, 10, 20, 7, 15, 0, DateTimeKind.Utc), task.DueAtUtc);

        var loaded = await _store.GetAsync(task.Id);
        Assert.Equal(task.DueAtUtc, loaded!.DueAtUtc);
    }

    [Fact]
    public async Task After_BM_complete_task_reappears_as_DM_holder_for_confirm()
    {
        var created = await CreateDirectorTaskAsync(
            title: "confirm-holder",
            priority: DirectorAssignedTaskPriority.High,
            due: new DateOnly(2026, 10, 27),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None)).Ok);
        Assert.True((await _sut.TransferAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-7",
            "إدارة الفرع",
            null,
            CancellationToken.None)).Ok);
        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-7",
            "إدارة الفرع",
            CancellationToken.None)).Ok);

        var bmComplete = await _sut.CompleteAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-7",
            "إدارة الفرع",
            CancellationToken.None);
        Assert.True(bmComplete.Ok, bmComplete.Error);
        Assert.Equal(DirectorAssignedTaskStatus.AwaitingDelegatedConfirm, bmComplete.Task!.Status);
        Assert.Equal(DirectorAssignedTaskAssigneeKind.DelegatedManager, bmComplete.Task.CurrentHolderKind);
        Assert.Equal("ayman", bmComplete.Task.CurrentHolderKey);

        var dmInbox = await _sut.InboxForDmAsync("ayman", CancellationToken.None);
        Assert.Contains(dmInbox, t => t.Id == created.Id
            && t.Status == DirectorAssignedTaskStatus.AwaitingDelegatedConfirm);
    }

    [Fact]
    public async Task After_transfer_BM_reject_and_resend_DM_self_complete_is_Completed_not_awaiting_confirm()
    {
        var created = await CreateDirectorTaskAsync(
            title: "stale-transfer",
            priority: DirectorAssignedTaskPriority.High,
            due: new DateOnly(2026, 10, 27),
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن");

        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None)).Ok);

        Assert.True((await _sut.TransferAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-99",
            "إدارة الفرع",
            note: null,
            CancellationToken.None)).Ok);

        Assert.True((await _sut.RejectAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-99",
            "إدارة الفرع",
            note: "لا أستطيع",
            CancellationToken.None)).Ok);

        Assert.True((await _sut.UpdateReturnedAsync(
            created.Id,
            title: "stale-transfer",
            description: null,
            dueDate: new DateOnly(2026, 10, 27),
            priority: DirectorAssignedTaskPriority.High,
            assigneeKind: DirectorAssignedTaskAssigneeKind.DelegatedManager,
            assigneeKey: "ayman",
            assigneeDisplayName: "أيمن",
            assigneeCityValue: null,
            updatedByUserId: "director-1",
            updatedByUserName: "المدير",
            ct: CancellationToken.None)).Ok);

        Assert.True((await _sut.AcceptAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None)).Ok);

        var complete = await _sut.CompleteAsync(
            created.Id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman",
            "أيمن",
            CancellationToken.None);

        Assert.True(complete.Ok, complete.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, complete.Task!.Status);
        Assert.NotNull(complete.Task.DelegatedCompletedAtUtc);
        Assert.Null(complete.Task.BranchCompletedAtUtc);
        Assert.Null(complete.Task.DelegatedConfirmedAtUtc);
    }

    private async Task<DirectorAssignedTask> CreateDirectorTaskAsync(
        string title,
        byte priority,
        DateOnly due,
        string assigneeKind,
        string assigneeKey,
        string assigneeDisplayName)
    {
        var (ok, error, task) = await _sut.CreateAsync(
            title: title,
            description: null,
            dueDate: due,
            priority: priority,
            assigneeKind: assigneeKind,
            assigneeKey: assigneeKey,
            assigneeDisplayName: assigneeDisplayName,
            assigneeCityValue: null,
            createdByUserId: "director-1",
            createdByUserName: "المدير",
            ct: CancellationToken.None);
        Assert.True(ok, error);
        Assert.NotNull(task);
        return task!;
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } =
            Path.Combine(Path.GetTempPath(), "assigned-task-svc-" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FormFileStub : IFormFile
    {
        private readonly byte[] _bytes;

        public FormFileStub(byte[] bytes, string fileName, string contentType)
        {
            _bytes = bytes;
            FileName = fileName;
            ContentType = contentType;
            Name = "file";
            Length = bytes.Length;
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers { get; } = new HeaderDictionary();
        public long Length { get; }
        public string Name { get; }
        public string FileName { get; }

        public void CopyTo(Stream target) => target.Write(_bytes);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            target.Write(_bytes);
            return Task.CompletedTask;
        }

        public Stream OpenReadStream() => new MemoryStream(_bytes, writable: false);
    }
}
