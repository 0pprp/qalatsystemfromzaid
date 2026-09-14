using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BE_SalesEmployee.Sales.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class CompanyJwtSigningConfigTests
{
    private static readonly string CompanyKeyB64 = Convert.ToBase64String(
        Encoding.UTF8.GetBytes("company-signing-key-32-bytes-min!!"));

    private static readonly string OtherKeyB64 = Convert.ToBase64String(
        Encoding.UTF8.GetBytes("other-signing-key-32-bytes-min!!!"));

    [Fact]
    public void Missing_SigningKeys_Fails_Fast_Without_Secret_In_Message()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "gateway-should-not-be-used"
        }).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => CompanyJwtSigningConfig.LoadRequired(config));
        Assert.Contains("SigningKeys", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("gateway-should-not-be-used", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Company_Signed_MainAccountant_Token_Validates()
    {
        var config = ConfigWith(CompanyKeyB64);
        var keys = CompanyJwtSigningConfig.LoadRequired(config);
        var token = Mint(CompanyKeyB64, "محاسب رئيسي", expiresMinutes: 30);
        var principal = Validate(token, keys);
        Assert.Equal("محاسب رئيسي", principal.FindFirst("UserType")?.Value);
    }

    [Fact]
    public void Invalid_Signer_Rejected()
    {
        var keys = CompanyJwtSigningConfig.LoadRequired(ConfigWith(CompanyKeyB64));
        var token = Mint(OtherKeyB64, "محاسب رئيسي", expiresMinutes: 30);
        Assert.ThrowsAny<Exception>(() => Validate(token, keys));
    }

    [Fact]
    public void Expired_Token_Rejected()
    {
        var keys = CompanyJwtSigningConfig.LoadRequired(ConfigWith(CompanyKeyB64));
        var token = Mint(CompanyKeyB64, "محاسب رئيسي", expiresMinutes: -5);
        Assert.ThrowsAny<Exception>(() => Validate(token, keys));
    }

    [Fact]
    public void SalesManager_Role_Is_Not_MainAccountant_Policy()
    {
        var keys = CompanyJwtSigningConfig.LoadRequired(ConfigWith(CompanyKeyB64));
        var token = Mint(CompanyKeyB64, "مدير مبيعات", expiresMinutes: 30);
        var principal = Validate(token, keys);
        Assert.False(string.Equals(principal.FindFirst("UserType")?.Value, "محاسب رئيسي", StringComparison.Ordinal));
    }

    [Fact]
    public void Fingerprint12_Is_Stable_And_Short()
    {
        var keys = CompanyJwtSigningConfig.LoadRequired(ConfigWith(CompanyKeyB64));
        var fp = CompanyJwtSigningConfig.Fingerprint12(keys[0]);
        Assert.Equal(12, fp.Length);
        Assert.Equal(fp, CompanyJwtSigningConfig.Fingerprint12(keys[0]));
    }

    private static IConfiguration ConfigWith(string keyB64) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Schemes:Bearer:SigningKeys:0:Value"] = keyB64
        }).Build();

    private static string Mint(string keyB64, string userType, int expiresMinutes)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(keyB64));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: [new Claim("UserType", userType), new Claim("UserID", "42")],
            notBefore: DateTime.UtcNow.AddMinutes(Math.Min(expiresMinutes, 0) - 1),
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static ClaimsPrincipal Validate(string token, IReadOnlyList<SecurityKey> keys)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKeys = keys
        }, out _);
    }
}
