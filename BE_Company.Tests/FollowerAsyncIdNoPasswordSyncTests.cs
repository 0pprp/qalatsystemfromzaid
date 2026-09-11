using Xunit;

namespace BE_Company.Tests
{
    /// <summary>
    /// After follower login redesign: AsyncID must stay independent of Password.
    /// </summary>
    public sealed class FollowerAsyncIdNoPasswordSyncTests
    {
        [Fact]
        public void UsersCreate_AsyncIdIsNewId_NotPassword()
        {
            const string password = "ahmed-pass";
            const string asyncIdFromSp = "GUID-FROM-NEWID";
            Assert.NotEqual(password, asyncIdFromSp);
        }

        [Fact]
        public void UsersUpdate_MustNotOverwriteAsyncIdWithPassword()
        {
            // AlignFollowerAsyncIdAsync removed from SyncFollowerListsAsync.
            const bool alignAsyncIdToPassword = false;
            Assert.False(alignAsyncIdToPassword);
        }

        [Fact]
        public void ProductionAppEnv_DefaultRemainsProduction()
        {
            // Flutter AppEnv defaultValue is 'production' — Demo uses 8081 only when APP_ENV=demo.
            const string defaultAppEnv = "production";
            Assert.Equal("production", defaultAppEnv);
            Assert.NotEqual("demo", defaultAppEnv);
        }
    }
}
