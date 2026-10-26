using BE_Company.Services;
using BE_Company.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_Company.Tests;

public class PortalTokenSecurityTests
{
    private static string Key32() => Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray());

    [Fact]
    public void AesGcm_token_roundtrip_succeeds()
    {
        var key = Key32();
        var token = PortalTokenCrypto.Create(42, 5, PortalTokenCrypto.PurposeCustomerPortal, TimeSpan.FromHours(1), key);
        var payload = PortalTokenCrypto.TryValidate(token, key);
        Assert.NotNull(payload);
        Assert.Equal(42, payload!.CustomerId);
        Assert.Equal(5, payload.BranchId);
    }

    [Fact]
    public void Tampered_token_rejected()
    {
        var key = Key32();
        var token = PortalTokenCrypto.Create(42, 5, PortalTokenCrypto.PurposeCustomerPortal, TimeSpan.FromHours(1), key);
        var chars = token.ToCharArray();
        chars[^2] = chars[^2] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);
        Assert.Null(PortalTokenCrypto.TryValidate(tampered, key));
    }

    [Fact]
    public void Expired_token_rejected()
    {
        var key = Key32();
        var token = PortalTokenCrypto.Create(42, 5, PortalTokenCrypto.PurposeCustomerPortal, TimeSpan.FromHours(1), key);
        var future = DateTimeOffset.UtcNow.AddHours(2);
        Assert.Null(PortalTokenCrypto.TryValidate(token, key, future));
    }

    [Fact]
    public void Wrong_purpose_rejected()
    {
        var key = Key32();
        var token = PortalTokenCrypto.Create(42, 5, "admin-debug", TimeSpan.FromHours(1), key);
        Assert.Null(PortalTokenCrypto.TryValidate(token, key));
    }

    [Fact]
    public void Branch_mismatch_and_revocation()
    {
        var key = Key32();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PortalSettings:EncryptionKey"] = key,
            ["PortalSettings:BranchId"] = "5",
            ["PortalSettings:TokenLifetimeDays"] = "1",
        }).Build();
        var store = new MemoryPortalTokenRevocationStore();
        var access = new PortalAccessService(config, store);

        var otherBranch = PortalTokenCrypto.Create(10, 9, PortalTokenCrypto.PurposeCustomerPortal, TimeSpan.FromHours(1), key);
        Assert.False(access.TryAuthorize(otherBranch, out _, out var err));
        Assert.Contains("فرع", err);

        var good = access.CreateCustomerPortalToken(10, 5);
        Assert.True(access.TryAuthorize(good, out var customerId, out _));
        Assert.Equal(10, customerId);

        access.RevokeToken(good);
        Assert.False(access.TryAuthorize(good, out _, out _));
    }

    [Fact]
    public void Legacy_cbc_token_has_no_fallback()
    {
        var key = Key32();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PortalSettings:EncryptionKey"] = key,
            ["PortalSettings:BranchId"] = "5",
        }).Build();
        var access = new PortalAccessService(config, new MemoryPortalTokenRevocationStore());

#pragma warning disable CS0618
        var legacy = EncryptionHelper.CreateCustomerToken(10, key, branchId: 5);
#pragma warning restore CS0618
        Assert.False(access.TryAuthorize(legacy, out _, out _));
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_verify_and_legacy_upgrade()
    {
        var hashed = PasswordHasher.Hash("Secret#1");
        Assert.StartsWith(PasswordHasher.Prefix, hashed);
        Assert.NotEqual("Secret#1", hashed);
        Assert.True(PasswordHasher.Verify("Secret#1", hashed, out var needsUpgrade));
        Assert.False(needsUpgrade);
        Assert.False(PasswordHasher.Verify("wrong", hashed, out _));

        Assert.True(PasswordHasher.Verify("legacy-plain", "legacy-plain", out var upgrade));
        Assert.True(upgrade);
    }
}

public class UserLevelMiddlewareTrustTests
{
    [Fact]
    public async Task Unauthenticated_forgery_does_not_populate_items()
    {
        // Unsigned JWT-looking payload claiming UserID=999
        var forged = "eyJhbGciOiJub25lIn0.eyJVc2VySUQiOiI5OTkiLCJVc2VyVHlwZSI6IuivremgqCJ9.";
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer " + forged;
        // Not authenticated
        var called = false;
        RequestDelegate next = ctx =>
        {
            called = true;
            Assert.False(ctx.Items.ContainsKey("UserID"));
            return Task.CompletedTask;
        };
        var mw = new UserLevelMiddleware(next);
        await mw.InvokeAsync(context);
        Assert.True(called);
    }

    [Fact]
    public async Task Authenticated_principal_copies_claims()
    {
        var context = new DefaultHttpContext();
        var identity = new System.Security.Claims.ClaimsIdentity(
            new[]
            {
                new System.Security.Claims.Claim("UserID", "7"),
                new System.Security.Claims.Claim("UserType", "مدير فرع"),
            },
            authenticationType: "Test");
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);

        var mw = new UserLevelMiddleware(ctx =>
        {
            Assert.Equal("7", ctx.Items["UserID"]);
            Assert.Equal("مدير فرع", ctx.Items["UserType"]);
            return Task.CompletedTask;
        });
        await mw.InvokeAsync(context);
    }
}
