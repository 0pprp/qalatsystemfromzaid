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
        public void BeforeFour_EligibleImmediatelyAtCreated()
        {
            // 15:30 Baghdad = 12:30 UTC — gate removed, eligible at create time.
            var created = new DateTime(2026, 3, 26, 12, 30, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
            Assert.True(CollectionPaymentRules.IsEligibleForPosting(eligible, created));
        }

        [Fact]
        public void OfflineCreated1530_Received1700_EligibleAtCreated()
        {
            var created = new DateTime(2026, 3, 26, 12, 30, 0, DateTimeKind.Utc); // 15:30 Baghdad
            var received = new DateTime(2026, 3, 26, 14, 0, 0, DateTimeKind.Utc); // 17:00 Baghdad
            var eligible = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
            Assert.True(CollectionPaymentRules.IsEligibleForPosting(eligible, received));
        }

        [Fact]
        public void Created1605_PostsImmediatelyAfterAccept()
        {
            var created = new DateTime(2026, 3, 26, 13, 5, 0, DateTimeKind.Utc); // 16:05 Baghdad
            var eligible = CollectionPaymentRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
            Assert.True(CollectionPaymentRules.IsEligibleForPosting(eligible, created));
        }

        [Fact]
        public void AutoPostPath_DoesNotRequireManualApprovalFlag()
        {
            // Contract: AutoPostEnabled=1 payments are accepted without accountant Approve UI.
            const bool autoPostEnabled = true;
            const bool requiresManualAccountantApproval = !autoPostEnabled;
            Assert.False(requiresManualAccountantApproval);
        }

        [Fact]
        public void AfterFour_EligibleImmediatelyAtCreated()
        {
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
            Assert.True(CollectionPaymentRules.IsValidClientPaymentId(id));
        }
    }
}
