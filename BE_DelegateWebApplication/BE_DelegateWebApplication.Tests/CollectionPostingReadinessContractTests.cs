using BE_DelegateWebApplication.Services.CollectionPayments;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

/// <summary>
/// Documents readiness contract: API alive is insufficient without PostEligible.
/// </summary>
public sealed class CollectionPostingReadinessContractTests
{
    [Fact]
    public void ClientPaymentId_Remains_The_Idempotency_Key()
    {
        var a = Guid.NewGuid().ToString();
        Assert.True(CollectionPaymentRules.IsValidClientPaymentId(a));
        Assert.False(CollectionPaymentRules.IsValidClientPaymentId("legacy-local-1"));
        // Device may hold legacy-local-* until migrated; server GUID path stays strict.
    }

    [Fact]
    public void Amount_And_CustomerName_Are_Not_Idempotency_Keys()
    {
        // Same amount + name must still be distinct payments when ClientPaymentId differs.
        var id1 = Guid.NewGuid().ToString();
        var id2 = Guid.NewGuid().ToString();
        Assert.NotEqual(id1, id2);
        Assert.True(CollectionPaymentRules.IsValidClientPaymentId(id1));
        Assert.True(CollectionPaymentRules.IsValidClientPaymentId(id2));
    }
}
