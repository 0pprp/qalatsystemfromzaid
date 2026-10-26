using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class LegalFollowupStatusTests
{
    [Theory]
    [InlineData("Completed", "تمت المتابعة")]
    [InlineData("InProgress", "قيد العمل")]
    [InlineData("Cancelled", "ملغاة")]
    [InlineData(null, "قيد العمل")]
    public void Arabic_MapsKnownStatuses(string? status, string expected)
    {
        Assert.Equal(expected, LegalFollowupStatuses.Arabic(status));
    }

    [Theory]
    [InlineData("InProgress", true)]
    [InlineData("Completed", false)]
    [InlineData("Cancelled", false)]
    [InlineData(null, false)]
    public void CanCancel_OnlyInProgress(string? status, bool expected)
    {
        Assert.Equal(expected, LegalFollowupService.CanCancelCase(status));
    }

    [Fact]
    public void CreateRequest_DefaultsSafe()
    {
        var req = new LegalFollowupCreateRequest();
        Assert.Equal(0, req.SalesRequestId);
        Assert.Equal(0, req.FollowerUserId);
        Assert.Null(req.Note);
    }

    [Fact]
    public void CanSendToLegal_AllowsUnassignedAndRejected_Only()
    {
        Assert.True(LegalFollowupService.CanSendSalesRequestToLegal(new SalesRequestDTO
        {
            Status = SalesRequestStatuses.New,
            TargetEmployeeId = 0
        }));
        Assert.True(LegalFollowupService.CanSendSalesRequestToLegal(new SalesRequestDTO
        {
            Status = SalesRequestStatuses.Rejected,
            TargetEmployeeId = 7
        }));
        Assert.False(LegalFollowupService.CanSendSalesRequestToLegal(new SalesRequestDTO
        {
            Status = SalesRequestStatuses.Assigned,
            TargetEmployeeId = 7
        }));
        Assert.False(LegalFollowupService.CanSendSalesRequestToLegal(new SalesRequestDTO
        {
            Status = SalesRequestStatuses.Rejected,
            TargetEmployeeId = 7,
            IsDeleted = true
        }));
        Assert.False(LegalFollowupService.CanSendSalesRequestToLegal(new SalesRequestDTO
        {
            Status = SalesRequestStatuses.Rejected,
            TargetEmployeeId = 7,
            ExceptionHoldStatus = SalesExceptionHoldStatuses.Pending
        }));
    }
}
