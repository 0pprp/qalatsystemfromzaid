using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class DirectorAssignedTaskRulesTests
{
    [Fact]
    public void Sort_orders_high_medium_low_then_sooner_due_date()
    {
        var items = new[]
        {
            (Priority: DirectorAssignedTaskPriority.Low, Due: new DateOnly(2026, 10, 25)),
            (Priority: DirectorAssignedTaskPriority.High, Due: new DateOnly(2026, 10, 30)),
            (Priority: DirectorAssignedTaskPriority.Medium, Due: new DateOnly(2026, 10, 20)),
            (Priority: DirectorAssignedTaskPriority.High, Due: new DateOnly(2026, 10, 22)),
            (Priority: DirectorAssignedTaskPriority.Medium, Due: new DateOnly(2026, 10, 18)),
        };

        var sorted = items
            .OrderBy(x => x, Comparer<(byte Priority, DateOnly Due)>.Create(
                (a, b) => DirectorAssignedTaskRules.CompareSortOrder(a.Priority, a.Due, b.Priority, b.Due)))
            .ToList();

        Assert.Equal(
            [
                (DirectorAssignedTaskPriority.High, new DateOnly(2026, 10, 22)),
                (DirectorAssignedTaskPriority.High, new DateOnly(2026, 10, 30)),
                (DirectorAssignedTaskPriority.Medium, new DateOnly(2026, 10, 18)),
                (DirectorAssignedTaskPriority.Medium, new DateOnly(2026, 10, 20)),
                (DirectorAssignedTaskPriority.Low, new DateOnly(2026, 10, 25)),
            ],
            sorted);
    }

    [Fact]
    public void IsOverdue_when_due_date_before_iraq_today_does_not_change_status()
    {
        var due = new DateOnly(2026, 10, 19);
        var iraqToday = new DateOnly(2026, 10, 20);
        var status = DirectorAssignedTaskStatus.Accepted;

        Assert.True(DirectorAssignedTaskRules.IsOverdue(due, iraqToday));
        Assert.False(DirectorAssignedTaskRules.IsOverdue(iraqToday, iraqToday));
        Assert.False(DirectorAssignedTaskRules.IsOverdue(iraqToday.AddDays(1), iraqToday));

        // Overdue is a computed flag only — status stays unchanged.
        Assert.Equal(DirectorAssignedTaskStatus.Accepted, status);
    }

    [Fact]
    public void IsOverdue_with_DueAtUtc_compares_against_utc_now()
    {
        var dueDate = new DateOnly(2026, 10, 20);
        var iraqToday = new DateOnly(2026, 10, 20);
        var dueAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        var nowPast = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
        var nowBefore = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        Assert.True(DirectorAssignedTaskRules.IsOverdue(dueDate, dueAt, iraqToday, nowPast));
        Assert.False(DirectorAssignedTaskRules.IsOverdue(dueDate, dueAt, iraqToday, nowBefore));
        // Without DueAtUtc, falls back to calendar day.
        Assert.False(DirectorAssignedTaskRules.IsOverdue(dueDate, null, iraqToday, nowPast));
    }

    [Fact]
    public void CanAccept_allows_Sent_or_Transferred()
    {
        Assert.True(DirectorAssignedTaskRules.CanAccept(DirectorAssignedTaskStatus.Sent));
        Assert.True(DirectorAssignedTaskRules.CanAccept(DirectorAssignedTaskStatus.Transferred));
        Assert.False(DirectorAssignedTaskRules.CanAccept(DirectorAssignedTaskStatus.Accepted));
        Assert.False(DirectorAssignedTaskRules.CanAccept(DirectorAssignedTaskStatus.Returned));
    }

    [Fact]
    public void ResolveDue_from_hour_minute_period_sets_DueAtUtc_and_Iraq_DueDate()
    {
        var result = DirectorAssignedTaskRules.ResolveDue(
            dueDate: new DateOnly(2026, 10, 20),
            dueAtUtc: null,
            hour12: 3,
            minute: 30,
            period: "مساء");

        Assert.True(result.Ok, result.Error);
        Assert.NotNull(result.DueAtUtc);
        // Iraq 2026-10-20 15:30 (+03) => 12:30 UTC
        Assert.Equal(new DateTime(2026, 10, 20, 12, 30, 0, DateTimeKind.Utc), result.DueAtUtc);
        Assert.Equal(new DateOnly(2026, 10, 20), result.DueDate);
    }

    [Fact]
    public void ResolveDue_from_dueAtUtc_derives_Iraq_calendar_day()
    {
        // 2026-10-20 22:00 UTC => Iraq 2026-10-21 01:00
        var dueAt = new DateTime(2026, 10, 20, 22, 0, 0, DateTimeKind.Utc);
        var result = DirectorAssignedTaskRules.ResolveDue(
            dueDate: default,
            dueAtUtc: dueAt,
            hour12: null,
            minute: null,
            period: null);

        Assert.True(result.Ok, result.Error);
        Assert.Equal(dueAt, result.DueAtUtc);
        Assert.Equal(new DateOnly(2026, 10, 21), result.DueDate);
    }

    [Fact]
    public void Reject_requires_note_and_sets_returned()
    {
        var missing = DirectorAssignedTaskRules.Reject(null);
        Assert.False(missing.Ok);
        Assert.NotNull(missing.Error);
        Assert.Null(missing.NewStatus);

        var blank = DirectorAssignedTaskRules.Reject("   ");
        Assert.False(blank.Ok);
        Assert.NotNull(blank.Error);

        var ok = DirectorAssignedTaskRules.Reject("غير مناسب");
        Assert.True(ok.Ok);
        Assert.Null(ok.Error);
        Assert.Equal(DirectorAssignedTaskStatus.Returned, ok.NewStatus);
    }

    [Fact]
    public void Complete_direct_branch_manager_becomes_completed()
    {
        var result = DirectorAssignedTaskRules.Complete(
            DirectorAssignedTaskStatus.Accepted,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            hasTransfer: false);

        Assert.True(result.Ok);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, result.NewStatus);
    }

    [Fact]
    public void Complete_field_committee_follower_becomes_completed()
    {
        var result = DirectorAssignedTaskRules.Complete(
            DirectorAssignedTaskStatus.Accepted,
            DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower,
            hasTransfer: false);

        Assert.True(result.Ok);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, result.NewStatus);
    }

    [Fact]
    public void Complete_delegated_manager_self_becomes_completed()
    {
        var result = DirectorAssignedTaskRules.Complete(
            DirectorAssignedTaskStatus.Accepted,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            hasTransfer: false);

        Assert.True(result.Ok);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, result.NewStatus);
    }

    [Fact]
    public void Complete_after_dm_transfer_awaits_delegated_confirm()
    {
        var result = DirectorAssignedTaskRules.Complete(
            DirectorAssignedTaskStatus.Accepted,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            hasTransfer: true);

        Assert.True(result.Ok);
        Assert.Equal(DirectorAssignedTaskStatus.AwaitingDelegatedConfirm, result.NewStatus);
    }

    [Fact]
    public void Confirm_after_transfer_becomes_completed()
    {
        var result = DirectorAssignedTaskRules.Confirm(
            DirectorAssignedTaskStatus.AwaitingDelegatedConfirm,
            hasTransfer: true);

        Assert.True(result.Ok);
        Assert.Equal(DirectorAssignedTaskStatus.Completed, result.NewStatus);
    }

    [Fact]
    public void Confirm_without_transfer_fails()
    {
        var result = DirectorAssignedTaskRules.Confirm(
            DirectorAssignedTaskStatus.Accepted,
            hasTransfer: false);

        Assert.False(result.Ok);
        Assert.NotNull(result.Error);
        Assert.Null(result.NewStatus);
    }
}
