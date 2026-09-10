using BE_Company.Services.CollectionPayments;
using Xunit;

namespace BE_Company.Tests
{
    public sealed class CollectionPaymentPostingRulesTests
    {
        [Fact]
        public void BeforeFour_NotPostedEarly()
        {
            var created = new DateTime(2026, 3, 26, 12, 0, 0, DateTimeKind.Utc); // 15:00 Baghdad
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.False(CollectionPaymentPostingRules.IsEligible(eligible, created));
            Assert.False(CollectionPaymentPostingRules.IsEligible(eligible, new DateTime(2026, 3, 26, 12, 59, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void AtFour_BecomesEligible()
        {
            var created = new DateTime(2026, 3, 26, 12, 0, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.True(CollectionPaymentPostingRules.IsEligible(
                eligible, new DateTime(2026, 3, 26, 13, 0, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void CatchUp_AfterRestartAt1605()
        {
            var created = new DateTime(2026, 3, 26, 10, 0, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            var restart = new DateTime(2026, 3, 26, 13, 5, 0, DateTimeKind.Utc); // 16:05 Baghdad
            Assert.True(CollectionPaymentPostingRules.IsEligible(eligible, restart));
        }

        [Fact]
        public void PostFour_Immediate()
        {
            var created = new DateTime(2026, 3, 26, 14, 0, 0, DateTimeKind.Utc); // 17:00 Baghdad
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
        }

        [Fact]
        public void DoublePostGate_SameEligibleDoesNotChange()
        {
            var created = new DateTime(2026, 3, 26, 11, 0, 0, DateTimeKind.Utc);
            var a = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            var b = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(a, b);
        }
    }
}
