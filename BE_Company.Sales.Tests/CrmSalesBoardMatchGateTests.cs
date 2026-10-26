using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class CrmSalesBoardMatchGateTests
{
    [Fact]
    public void ShouldBlockAutoAssign_WhenProgramHasMatch()
    {
        Assert.True(CrmSalesBoardMatchGate.ShouldBlockAutoAssign(programHasMatch: true, boardHasMatch: false));
    }

    [Fact]
    public void ShouldBlockAutoAssign_WhenBoardHasMatch()
    {
        Assert.True(CrmSalesBoardMatchGate.ShouldBlockAutoAssign(programHasMatch: false, boardHasMatch: true));
    }

    [Fact]
    public void ShouldAllowAutoAssign_WhenNoProgramOrBoardMatch()
    {
        Assert.False(CrmSalesBoardMatchGate.ShouldBlockAutoAssign(programHasMatch: false, boardHasMatch: false));
    }

    [Fact]
    public void HasBoardDuplicate_DetectsPhoneMatchAmongPeers()
    {
        var self = new SalesRequestDTO
        {
            Id = 100,
            CustomerName = "علي محمد حسن",
            CustomerPhone = "07509998877"
        };
        var peers = new[]
        {
            new SalesRequestDTO { Id = 1, CustomerName = "شخص آخر", CustomerPhone = "07509998877" },
            new SalesRequestDTO { Id = 2, CustomerName = "لا تطابق", CustomerPhone = "07701112233" }
        };

        Assert.True(CrmSalesBoardMatchGate.HasBoardDuplicate(self, peers));
    }

    [Fact]
    public void HasBoardDuplicate_DetectsTripleNameMatch()
    {
        var self = new SalesRequestDTO
        {
            Id = 100,
            CustomerName = "علي محمد حسن",
            CustomerPhone = "07501110000"
        };
        var peers = new[]
        {
            new SalesRequestDTO { Id = 1, CustomerName = "علي محمد حسن", CustomerPhone = "07709999999" }
        };

        Assert.True(CrmSalesBoardMatchGate.HasBoardDuplicate(self, peers));
    }

    [Fact]
    public void HasBoardDuplicate_IgnoresSelf()
    {
        var self = new SalesRequestDTO
        {
            Id = 100,
            CustomerName = "علي محمد حسن",
            CustomerPhone = "07509998877"
        };
        var peers = new[] { self };

        Assert.False(CrmSalesBoardMatchGate.HasBoardDuplicate(self, peers));
    }

    [Fact]
    public void HasBoardDuplicate_DetectsSonFatherMatch()
    {
        var self = new SalesRequestDTO
        {
            Id = 100,
            CustomerName = "حسين أحمد محمد",
            CustomerPhone = "07501110000"
        };
        var peers = new[]
        {
            new SalesRequestDTO { Id = 1, CustomerName = "أحمد محمد علي", CustomerPhone = "07709999999" }
        };

        Assert.True(CrmSalesBoardMatchGate.HasBoardDuplicate(self, peers));
    }
}
