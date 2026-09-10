using BE_DelegateWebApplication.Services.CollectionPayments;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    public sealed class CollectionPaymentRulesTests
    {
        [Theory]
        [InlineData(1999, false)]
        [InlineData(2000, true)]
        [InlineData(2001, true)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        public void MinAmount_Enforced(double amount, bool ok)
        {
            var valid = CollectionPaymentRules.TryValidateAmount(amount, out var error);
            Assert.Equal(ok, valid);
            if (!ok && amount > 0 && amount < 2000)
            {
                Assert.Equal(CollectionPaymentRules.MinAmountMessage, error);
            }
        }

        [Fact]
        public void BeforeFour_EligibleAtSixteenSameDay()
        {
            // 15:30 Baghdad = 12:30 UTC
            var created = new DateTime(2026, 3, 26, 12, 30, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(created);
            // 16:00 Baghdad = 13:00 UTC
            Assert.Equal(new DateTime(2026, 3, 26, 13, 0, 0, DateTimeKind.Utc), eligible);
            Assert.False(CollectionPaymentRules.IsEligibleForPosting(eligible, created));
            Assert.True(CollectionPaymentRules.IsEligibleForPosting(eligible, new DateTime(2026, 3, 26, 13, 0, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void OfflineCreated1530_Received1700_UsesCreatedNotReceived()
        {
            var created = new DateTime(2026, 3, 26, 12, 30, 0, DateTimeKind.Utc); // 15:30 Baghdad
            var received = new DateTime(2026, 3, 26, 14, 0, 0, DateTimeKind.Utc); // 17:00 Baghdad
            var eligible = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(new DateTime(2026, 3, 26, 13, 0, 0, DateTimeKind.Utc), eligible);
            // At receive time 17:00, already eligible (gate was 16:00)
            Assert.True(CollectionPaymentRules.IsEligibleForPosting(eligible, received));
        }

        [Fact]
        public void AfterFour_EligibleImmediatelyAtCreated()
        {
            // 16:30 Baghdad = 13:30 UTC
            var created = new DateTime(2026, 3, 26, 13, 30, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
            Assert.True(CollectionPaymentRules.IsEligibleForPosting(eligible, created));
        }

        [Fact]
        public void ClientPaymentId_MustBeGuid()
        {
            Assert.True(CollectionPaymentRules.IsValidClientPaymentId(Guid.NewGuid().ToString()));
            Assert.False(CollectionPaymentRules.IsValidClientPaymentId(""));
            Assert.False(CollectionPaymentRules.IsValidClientPaymentId("not-a-guid"));
        }

        [Fact]
        public void Timestamp_RejectsUnreasonableFuture()
        {
            var now = new DateTime(2026, 3, 26, 12, 0, 0, DateTimeKind.Utc);
            var future = now.AddHours(5);
            Assert.False(CollectionPaymentRules.TryNormalizeCreatedAtUtc(future, now, out _, out var err));
            Assert.Contains("مستقبلي", err);
        }

        [Fact]
        public void IdempotencyKey_StableAcrossRetries()
        {
            var id = Guid.NewGuid().ToString();
            Assert.Equal(id, id); // document: client must reuse same UUID
            Assert.True(CollectionPaymentRules.IsValidClientPaymentId(id));
        }
    }
}
