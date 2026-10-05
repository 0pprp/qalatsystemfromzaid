using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Tests;

public sealed class SalesExceptionHoldStatusesTests
{
    [Fact]
    public void IsHeld_Null_IsFalse()
    {
        Assert.False(SalesExceptionHoldStatuses.IsHeld(null));
        Assert.False(SalesExceptionHoldStatuses.IsHeld(""));
    }

    [Fact]
    public void IsHeldUnassigned_NullHold_IsFalse_EvenWhenUnassigned()
    {
        var row = new SalesRequestDTO
        {
            ExceptionHoldStatus = null,
            TargetEmployeeId = 0,
            Status = SalesRequestStatuses.New,
            CustomerSourceType = SalesRequestSources.EmployeeSubmitted
        };
        Assert.False(SalesExceptionHoldStatuses.IsHeldUnassigned(row));
    }

    [Fact]
    public void IsHeldUnassigned_PendingUnassigned_IsTrue()
    {
        var row = new SalesRequestDTO
        {
            ExceptionHoldStatus = SalesExceptionHoldStatuses.Pending,
            TargetEmployeeId = 0
        };
        Assert.True(SalesExceptionHoldStatuses.IsHeldUnassigned(row));
    }
}
