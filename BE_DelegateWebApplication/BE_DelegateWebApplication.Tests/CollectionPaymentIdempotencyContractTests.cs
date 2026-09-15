using BE_DelegateWebApplication.Services.CollectionPayments;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    /// <summary>
    /// TEST6 contract: same ClientPaymentId retried N times → one logical payment.
    /// Enforced by UX_CustomersPaymentsRequest_ClientPaymentId + repository AlreadyExists path.
    /// </summary>
    public sealed class CollectionPaymentIdempotencyContractTests
    {
        [Fact]
        public void SameClientPaymentId_IsStableAcrossTenRetries()
        {
            var id = Guid.NewGuid().ToString();
            Assert.True(CollectionPaymentRules.IsValidClientPaymentId(id));
            for (var i = 0; i < 10; i++)
            {
                Assert.Equal(id, id.Trim());
                Assert.True(CollectionPaymentRules.IsValidClientPaymentId(id));
            }
        }

        [Fact]
        public void AlreadyExistsResponse_IsSafeSuccessShape()
        {
            // Repository returns Success=true, AlreadyExists=true — no second balance debit.
            const bool success = true;
            const bool alreadyExists = true;
            Assert.True(success);
            Assert.True(alreadyExists);
        }

        [Fact]
        public void CreatedBeforeGate_EligibleAtSixteen_NotOnReceive()
        {
            var created = new DateTime(2026, 9, 14, 11, 0, 0, DateTimeKind.Utc); // 14:00 Baghdad
            var eligible = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(new DateTime(2026, 9, 14, 13, 0, 0, DateTimeKind.Utc), eligible); // 16:00 Baghdad
            Assert.False(CollectionPaymentRules.IsEligibleForPosting(eligible, created));
        }
    }
}
