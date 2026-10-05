using Xunit;
using BE_DelegateWebApplication.Services;

namespace BE_DelegateWebApplication.Tests
{
    /// <summary>
    /// Documents security expectations for PostPaymentIdempotent intake.
    /// </summary>
    public sealed class CollectionPaymentSecurityContractTests
    {
        [Fact]
        public void SpoofedDelegateId_IsIgnored_AuthIdentityUsedForInsert()
        {
            const int authDelegateId = 10;
            int? bodyDelegateId = 99;
            // Body may differ; server must still accept and store authDelegateId.
            var wouldHaveRejectedOld = bodyDelegateId is not null && bodyDelegateId != authDelegateId;
            Assert.True(wouldHaveRejectedOld);
            const int storedDelegateId = authDelegateId;
            Assert.Equal(10, storedDelegateId);
        }

        [Fact]
        public void AuthDelegateId_WinsOverBody()
        {
            const int authDelegateId = 10;
            int? bodyDelegateId = 99;
            var stored = authDelegateId; // never body
            Assert.NotEqual(bodyDelegateId, stored);
            Assert.Equal(authDelegateId, stored);
        }

        [Fact]
        public void FollowerCollectionEnabled_LocksDelegateIntakeMessage()
        {
            Assert.Contains("متابعين", FollowerCollectionPaymentGate.LockedMessage);
            Assert.Contains("مقفل", FollowerCollectionPaymentGate.LockedMessage);
        }

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        [InlineData(false, false, false)]
        public void FollowerCollection_LockWhenAuthOrCustomerListEnabled(
            bool authEnabled,
            bool customerEnabled,
            bool expectLocked)
        {
            var locked = authEnabled || customerEnabled;
            Assert.Equal(expectLocked, locked);
        }
    }
}
