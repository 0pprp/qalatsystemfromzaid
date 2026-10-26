using BE_Company.Services.CollectionPayments;
using Xunit;

namespace BE_Company.Tests
{
    public sealed class CollectionPaymentPostingRulesTests
    {
        [Fact]
        public void BeforeFour_EligibleImmediatelyAtCreated()
        {
            var created = new DateTime(2026, 3, 26, 12, 0, 0, DateTimeKind.Utc); // 15:00 Baghdad
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
            Assert.True(CollectionPaymentPostingRules.IsEligible(eligible, created));
        }

        [Fact]
        public void AtFour_StillEligibleAtCreated()
        {
            var created = new DateTime(2026, 3, 26, 12, 0, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.True(CollectionPaymentPostingRules.IsEligible(
                eligible, new DateTime(2026, 3, 26, 13, 0, 0, DateTimeKind.Utc)));
        }

        [Fact]
        public void CatchUp_AfterRestart_EligibleFromCreate()
        {
            var created = new DateTime(2026, 3, 26, 10, 0, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            var restart = new DateTime(2026, 3, 26, 13, 5, 0, DateTimeKind.Utc); // 16:05 Baghdad
            Assert.True(CollectionPaymentPostingRules.IsEligible(eligible, restart));
            Assert.True(CollectionPaymentPostingRules.IsEligible(eligible, created));
        }

        [Fact]
        public void PostFour_Immediate()
        {
            var created = new DateTime(2026, 3, 26, 14, 0, 0, DateTimeKind.Utc); // 17:00 Baghdad
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
        }

        [Fact]
        public void LateReceive_EligibleAtCreated()
        {
            var created = new DateTime(2026, 3, 26, 12, 30, 0, DateTimeKind.Utc); // 15:30
            var received = new DateTime(2026, 3, 26, 14, 0, 0, DateTimeKind.Utc); // 17:00
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
            Assert.True(CollectionPaymentPostingRules.IsEligible(eligible, received));
        }

        [Fact]
        public void CreatedAfterFour_Immediate()
        {
            var created = new DateTime(2026, 3, 26, 13, 5, 0, DateTimeKind.Utc);
            var eligible = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(created, eligible);
            Assert.True(CollectionPaymentPostingRules.IsEligible(eligible, created));
        }

        [Fact]
        public void DoublePostGate_SameEligibleDoesNotChange()
        {
            var created = new DateTime(2026, 3, 26, 11, 0, 0, DateTimeKind.Utc);
            var a = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            var b = CollectionPaymentPostingRules.ComputeEligibleForPostingAtUtc(created);
            Assert.Equal(a, b);
            Assert.Equal(created, a);
        }

        [Fact]
        public void EligibilityMode_DeployMarker()
        {
            Assert.Equal("ImmediateAtCreatedUtc", CollectionPaymentPostingRules.EligibilityMode);
        }
    }
}
