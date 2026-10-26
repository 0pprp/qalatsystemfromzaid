using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DirectorAssignedTaskStoreTests
{
    private static InMemoryDirectorAssignedTaskStore CreateStore() => new();

    private static DirectorAssignedTask NewTask(
        string title,
        byte priority,
        DateOnly due,
        string holderKind,
        string holderKey) => new()
    {
        Title = title,
        DueDate = due,
        Priority = priority,
        Status = DirectorAssignedTaskStatus.Sent,
        CreatedByUserId = "director-1",
        CreatedByUserName = "المدير",
        AssigneeKind = holderKind,
        AssigneeKey = holderKey,
        AssigneeDisplayName = "مكلف",
        CurrentHolderKind = holderKind,
        CurrentHolderKey = holderKey,
    };

    [Fact]
    public async Task Create_then_ListAllSorted_orders_by_priority_then_due_date()
    {
        var store = CreateStore();

        await store.CreateAsync(NewTask(
            "low-late",
            DirectorAssignedTaskPriority.Low,
            new DateOnly(2026, 10, 25),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman"));
        await store.CreateAsync(NewTask(
            "high-late",
            DirectorAssignedTaskPriority.High,
            new DateOnly(2026, 10, 30),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman"));
        await store.CreateAsync(NewTask(
            "med-early",
            DirectorAssignedTaskPriority.Medium,
            new DateOnly(2026, 10, 18),
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1"));
        await store.CreateAsync(NewTask(
            "high-early",
            DirectorAssignedTaskPriority.High,
            new DateOnly(2026, 10, 22),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman"));

        var listed = await store.ListAllSortedAsync();

        Assert.Equal(
            ["high-early", "high-late", "med-early", "low-late"],
            listed.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task AddEvent_appends_Transfer_and_ListEvents_returns_it()
    {
        var store = CreateStore();
        var task = await store.CreateAsync(NewTask(
            "transfer-me",
            DirectorAssignedTaskPriority.Medium,
            new DateOnly(2026, 10, 21),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman"));

        var evt = await store.AddEventAsync(new DirectorAssignedTaskEvent
        {
            TaskId = task.Id,
            EventType = "Transferred",
            AtUtc = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc),
            ActorKind = DirectorAssignedTaskAssigneeKind.DelegatedManager,
            ActorKey = "ayman",
            ActorDisplayName = "أيمن",
            ActorRoleLabel = "المدير المفوض",
            TargetKind = DirectorAssignedTaskAssigneeKind.BranchManager,
            TargetKey = "bm-42",
            TargetDisplayName = "إدارة الفرع",
            TargetRoleLabel = "إدارة الفرع",
            Note = "نقل للمهمة",
        });

        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Equal(task.Id, evt.TaskId);
        Assert.Equal("Transferred", evt.EventType);

        var events = await store.ListEventsAsync(task.Id);
        Assert.Single(events);
        Assert.Equal("Transferred", events[0].EventType);
        Assert.Equal("ayman", events[0].ActorKey);
        Assert.Equal("bm-42", events[0].TargetKey);
        Assert.Equal("نقل للمهمة", events[0].Note);
    }

    [Fact]
    public async Task ListInboxByHolder_filters_by_CurrentHolderKey()
    {
        var store = CreateStore();

        await store.CreateAsync(NewTask(
            "for-ayman",
            DirectorAssignedTaskPriority.High,
            new DateOnly(2026, 10, 22),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman"));
        await store.CreateAsync(NewTask(
            "for-bm",
            DirectorAssignedTaskPriority.Medium,
            new DateOnly(2026, 10, 23),
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1"));

        var transferred = await store.CreateAsync(NewTask(
            "held-by-bm-after-transfer",
            DirectorAssignedTaskPriority.Low,
            new DateOnly(2026, 10, 24),
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman"));
        transferred.CurrentHolderKind = DirectorAssignedTaskAssigneeKind.BranchManager;
        transferred.CurrentHolderKey = "bm-1";
        await store.UpdateAsync(transferred);

        var aymanInbox = await store.ListInboxByHolderAsync(
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            "ayman");
        Assert.Equal(["for-ayman"], aymanInbox.Select(t => t.Title).ToArray());

        var bmInbox = await store.ListInboxByHolderAsync(
            DirectorAssignedTaskAssigneeKind.BranchManager,
            "bm-1");
        Assert.Equal(
            ["for-bm", "held-by-bm-after-transfer"],
            bmInbox.Select(t => t.Title).ToArray());
    }
}
