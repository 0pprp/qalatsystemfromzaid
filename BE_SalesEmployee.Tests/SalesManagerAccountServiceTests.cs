using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests
{
    public sealed class SalesManagerAccountServiceTests
    {
        private static SalesManagerAccountService CreateService(Dictionary<string, string?> values)
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();
            return new SalesManagerAccountService(config);
        }

        [Fact]
        public void PrimaryAccount_Succeeds_AndReturnsDisplayName()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccount:UserName"] = "salesmanager",
                ["SalesManagerAccount:Password"] = "primary-secret",
                ["SalesManagerAccount:DisplayName"] = "مدير المبيعات",
            });

            var ok = svc.TryAuthenticate("salesmanager", "primary-secret", out var identity);

            Assert.True(ok);
            Assert.NotNull(identity);
            Assert.Equal("salesmanager", identity!.UserName, ignoreCase: true);
            Assert.Equal("مدير المبيعات", identity.DisplayName);
        }

        [Fact]
        public void PrimaryAccount_WrongPassword_Fails()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccount:UserName"] = "salesmanager",
                ["SalesManagerAccount:Password"] = "primary-secret",
                ["SalesManagerAccount:DisplayName"] = "مدير المبيعات",
            });

            var ok = svc.TryAuthenticate("salesmanager", "wrong", out var identity);

            Assert.False(ok);
            Assert.Null(identity);
        }

        [Fact]
        public void AdditionalAccounts_Succeed_WithOwnDisplayName()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccount:UserName"] = "salesmanager",
                ["SalesManagerAccount:Password"] = "primary-secret",
                ["SalesManagerAccount:DisplayName"] = "مدير المبيعات",
                ["SalesManagerAccounts:0:UserName"] = "manager2",
                ["SalesManagerAccounts:0:Password"] = "pass-two",
                ["SalesManagerAccounts:0:DisplayName"] = "مدير مركزي 2",
                ["SalesManagerAccounts:1:UserName"] = "manager3",
                ["SalesManagerAccounts:1:Password"] = "pass-three",
                ["SalesManagerAccounts:1:DisplayName"] = "مدير مركزي 3",
            });

            Assert.True(svc.TryAuthenticate("manager2", "pass-two", out var a2));
            Assert.NotNull(a2);
            Assert.Equal("manager2", a2!.UserName, ignoreCase: true);
            Assert.Equal("مدير مركزي 2", a2.DisplayName);

            Assert.True(svc.TryAuthenticate("manager3", "pass-three", out var a3));
            Assert.NotNull(a3);
            Assert.Equal("manager3", a3!.UserName, ignoreCase: true);
            Assert.Equal("مدير مركزي 3", a3.DisplayName);
        }

        [Fact]
        public void AdditionalAccount_WrongPassword_Fails()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccounts:0:UserName"] = "manager2",
                ["SalesManagerAccounts:0:Password"] = "pass-two",
                ["SalesManagerAccounts:0:DisplayName"] = "مدير مركزي 2",
            });

            var ok = svc.TryAuthenticate("manager2", "nope", out var identity);

            Assert.False(ok);
            Assert.Null(identity);
        }

        [Fact]
        public void Amar1_TemporaryAccount_StillWorks()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccount:UserName"] = "salesmanager",
                ["SalesManagerAccount:Password"] = "primary-secret",
                ["SalesManagerAccount:DisplayName"] = "مدير المبيعات",
            });

            var ok = svc.TryAuthenticate("amar1", "amar1", out var identity);

            Assert.True(ok);
            Assert.NotNull(identity);
            Assert.Equal("amar1", identity!.UserName, ignoreCase: true);
            Assert.Equal("مدير المبيعات", identity.DisplayName);
        }

        [Fact]
        public void UnknownUser_Fails()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccount:UserName"] = "salesmanager",
                ["SalesManagerAccount:Password"] = "primary-secret",
            });

            Assert.False(svc.TryAuthenticate("nobody", "primary-secret", out var identity));
            Assert.Null(identity);
        }

        [Fact]
        public void EmptyCredentials_Fail()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccount:UserName"] = "salesmanager",
                ["SalesManagerAccount:Password"] = "primary-secret",
            });

            Assert.False(svc.TryAuthenticate("", "primary-secret", out _));
            Assert.False(svc.TryAuthenticate("salesmanager", "", out _));
            Assert.False(svc.TryAuthenticate(null, null, out _));
        }

        [Fact]
        public void BoolOverload_MatchesIdentityOverload()
        {
            var svc = CreateService(new Dictionary<string, string?>
            {
                ["SalesManagerAccounts:0:UserName"] = "extra",
                ["SalesManagerAccounts:0:Password"] = "extra-pass",
                ["SalesManagerAccounts:0:DisplayName"] = "Extra Mgr",
            });

            Assert.True(svc.TryAuthenticate("extra", "extra-pass"));
            Assert.False(svc.TryAuthenticate("extra", "bad"));
        }
    }
}
