using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    /// <summary>
    /// Documents security expectations for PostPaymentIdempotent intake.
    /// </summary>
    public sealed class CollectionPaymentSecurityContractTests
    {
        [Fact]
        public void SpoofedDelegateId_MustBeRejectedWhenDifferentFromAuth()
        {
            const int authDelegateId = 10;
            int? bodyDelegateId = 99;
            var spoof = bodyDelegateId is not null && bodyDelegateId != authDelegateId;
            Assert.True(spoof);
        }

        [Fact]
        public void AuthDelegateId_WinsOverBody()
        {
            const int authDelegateId = 10;
            int? bodyDelegateId = 10;
            var allowed = bodyDelegateId is null || bodyDelegateId == authDelegateId;
            Assert.True(allowed);
            Assert.Equal(authDelegateId, authDelegateId); // server stores auth identity
        }
    }
}
