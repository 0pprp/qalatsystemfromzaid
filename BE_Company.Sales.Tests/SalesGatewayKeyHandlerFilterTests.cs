using System.Text;
using System.Text.Encodings.Web;
using BE_Company.Sales.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public sealed class SalesGatewayKeyHandlerFilterTests
    {
        private sealed class TestOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
        {
            private readonly AuthenticationSchemeOptions _value = new();
            public AuthenticationSchemeOptions CurrentValue => _value;
            public AuthenticationSchemeOptions Get(string? name) => _value;
            public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
        }

        private static async Task<AuthenticateResult> AuthenticateAsync(
            string? key,
            Action<IHeaderDictionary>? headers = null)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalApiKey"] = "test-key"
            }).Build();

            var handler = new SalesGatewayKeyHandler(
                new TestOptionsMonitor(),
                NullLoggerFactory.Instance,
                UrlEncoder.Default,
                config);

            var context = new DefaultHttpContext();
            if (!string.IsNullOrWhiteSpace(key))
            {
                context.Request.Headers[SalesGatewayKeyHandler.HeaderName] = key;
            }
            headers?.Invoke(context.Request.Headers);

            await handler.InitializeAsync(
                new AuthenticationScheme(SalesGatewayKeyHandler.SchemeName, null, typeof(SalesGatewayKeyHandler)),
                context);
            return await handler.AuthenticateAsync();
        }

        [Fact]
        public async Task ValidGatewayKey_FilterHeaders_AcceptedAsFilterEmployee()
        {
            var nameB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("فلتر1"));
            var result = await AuthenticateAsync("test-key", h =>
            {
                h[SalesGatewayKeyHandler.FilterUserNameB64Header] = nameB64;
                h[SalesGatewayKeyHandler.FilterExternalUserIdHeader] = "88";
                h[SalesGatewayKeyHandler.FilterRoleHeader] = "SalesFilterEmployee";
            });

            Assert.True(result.Succeeded);
            Assert.Equal(SalesRoles.UserTypeSalesFilterEmployee, result.Principal!.FindFirst("UserType")?.Value);
            Assert.Equal("فلتر1", result.Principal.FindFirst("UserName")?.Value);
            Assert.Equal("Gateway", result.Principal.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value);
            Assert.Equal("88", result.Principal.FindFirst(SalesGatewayKeyHandler.ExternalUserIdClaim)?.Value);
        }

        [Fact]
        public async Task InvalidGatewayKey_Rejected()
        {
            var result = await AuthenticateAsync("wrong", h =>
            {
                h[SalesGatewayKeyHandler.FilterUserNameB64Header] =
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("x"));
            });
            Assert.False(result.Succeeded);
            Assert.NotNull(result.Failure);
        }

        [Fact]
        public async Task ValidKey_WithoutFilterHeaders_RemainsManager()
        {
            var result = await AuthenticateAsync("test-key");
            Assert.True(result.Succeeded);
            Assert.Equal(SalesRoles.UserTypeSalesManager, result.Principal!.FindFirst("UserType")?.Value);
        }
    }
}
