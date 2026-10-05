using BE_DelegateWebApplication.DTO;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class DelegateListDedupeRulesTests
{
    [Fact]
    public void DedupeSelectDelegates_By_DelegateId()
    {
        var rows = new[]
        {
            new SelectDelegateGetDTO { DelegateId = 10, DelegateName = "أ" },
            new SelectDelegateGetDTO { DelegateId = 10, DelegateName = "أ" },
            new SelectDelegateGetDTO { DelegateId = 20, DelegateName = "ب" },
        };
        var deduped = DelegateListDedupe.DistinctByDelegateId(rows).ToList();
        Assert.Equal(2, deduped.Count);
        Assert.Contains(deduped, r => r.DelegateId == 10);
        Assert.Contains(deduped, r => r.DelegateId == 20);
    }

    [Fact]
    public void DedupeCustomers_By_CustomerId()
    {
        var rows = new[]
        {
            new CustomerGetDTO { CustomerId = 1, CustomerName = "س" },
            new CustomerGetDTO { CustomerId = 1, CustomerName = "س" },
            new CustomerGetDTO { CustomerId = 2, CustomerName = "ص" },
            new CustomerGetDTO { CustomerId = 1, CustomerName = "س" },
        };
        var deduped = DelegateListDedupe.DistinctByCustomerId(rows).ToList();
        Assert.Equal(2, deduped.Count);
        Assert.Equal(1, deduped.Count(r => r.CustomerId == 1));
    }
}
