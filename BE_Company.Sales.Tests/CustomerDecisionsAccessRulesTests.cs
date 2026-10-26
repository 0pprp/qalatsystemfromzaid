using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class CustomerDecisionsAccessRulesTests
{
    [Theory]
    [InlineData("محاسب رئيسي", null, true)]
    [InlineData("مدير فرع", null, true)]
    [InlineData("مدير مبيعات", SalesGatewayKeyHandler.AuthSourceGateway, true)]
    [InlineData("مدير مبيعات", null, false)]
    [InlineData("المراقب", null, false)]
    [InlineData("المراقب", SalesGatewayKeyHandler.AuthSourceGateway, true)]
    [InlineData("", SalesGatewayKeyHandler.AuthSourceGateway, true)]
    public void Can_list_accountant_queues(string userType, string? authSource, bool expected)
    {
        Assert.Equal(expected, CustomerDecisionsAccess.CanListAccountantQueues(userType, authSource));
    }

    [Theory]
    [InlineData("المراقب", true)]
    [InlineData("محاسب رئيسي", false)]
    [InlineData("مدير فرع", false)]
    [InlineData("مدير مبيعات", false)]
    public void Can_review_legal_is_monitor_only(string userType, bool expected)
    {
        Assert.Equal(expected, CustomerDecisionsAccess.CanReviewLegalConversion(userType));
    }

    [Theory]
    [InlineData("محاسب رئيسي", true)]
    [InlineData("المراقب", false)]
    [InlineData("مدير فرع", false)]
    [InlineData("مدير مبيعات", false)]
    public void Can_approve_waiver_is_accountant_only(string userType, bool expected)
    {
        Assert.Equal(expected, CustomerDecisionsAccess.CanApproveWaiver(userType));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("سبب الرفض", true)]
    public void Reject_note_required(string? note, bool expected)
    {
        Assert.Equal(expected, CustomerDecisionsAccess.HasRequiredReviewNote(note));
    }
}
