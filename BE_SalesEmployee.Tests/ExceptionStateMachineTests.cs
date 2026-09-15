using BE_SalesEmployee.DelegatedManager.Domain;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class ExceptionStateMachineTests
{
    [Theory]
    [InlineData(ExceptionStatuses.Approved)]
    [InlineData(ExceptionStatuses.Rejected)]
    [InlineData(ExceptionStatuses.Cancelled)]
    public void Pending_CanMoveToAnyTerminalStatus(string target)
    {
        Assert.True(ExceptionStateMachine.CanTransition(ExceptionStatuses.Pending, target));
    }

    [Theory]
    [InlineData(ExceptionStatuses.Approved, ExceptionStatuses.Rejected)]
    [InlineData(ExceptionStatuses.Approved, ExceptionStatuses.Cancelled)]
    [InlineData(ExceptionStatuses.Rejected, ExceptionStatuses.Approved)]
    [InlineData(ExceptionStatuses.Cancelled, ExceptionStatuses.Approved)]
    [InlineData(ExceptionStatuses.Approved, ExceptionStatuses.Pending)]
    [InlineData(ExceptionStatuses.Rejected, ExceptionStatuses.Pending)]
    [InlineData(ExceptionStatuses.Cancelled, ExceptionStatuses.Pending)]
    public void TerminalStatuses_AreFinal(string from, string to)
    {
        Assert.False(ExceptionStateMachine.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ExceptionStatuses.Pending)]
    [InlineData(ExceptionStatuses.Approved)]
    [InlineData(ExceptionStatuses.Rejected)]
    [InlineData(ExceptionStatuses.Cancelled)]
    public void SameStatus_IsNotATransition(string status)
    {
        Assert.False(ExceptionStateMachine.CanTransition(status, status));
    }

    [Theory]
    [InlineData(null, ExceptionStatuses.Approved)]
    [InlineData("", ExceptionStatuses.Approved)]
    [InlineData("   ", ExceptionStatuses.Approved)]
    [InlineData(ExceptionStatuses.Pending, null)]
    [InlineData(ExceptionStatuses.Pending, "")]
    public void MissingStatus_IsRejected(string? from, string? to)
    {
        Assert.False(ExceptionStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void UnknownStatuses_AreRejected()
    {
        Assert.False(ExceptionStateMachine.CanTransition("Archived", ExceptionStatuses.Approved));
        Assert.False(ExceptionStateMachine.CanTransition(ExceptionStatuses.Pending, "Archived"));
    }

    [Fact]
    public void StatusComparison_IsCaseSensitive()
    {
        Assert.False(ExceptionStateMachine.CanTransition("pending", ExceptionStatuses.Approved));
    }
}
