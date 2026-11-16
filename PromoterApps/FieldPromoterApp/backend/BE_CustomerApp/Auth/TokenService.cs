using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Auth;

public sealed record IssuedTokens(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken);

/// <summary>Short-lived access JWT + rotating opaque refresh token (only its SHA-256 is stored).</summary>
public sealed class TokenService
{
    private static readonly TimeSpan AccessLifetime = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(90);
    private readonly SymmetricSecurityKey _key;
    private readonly Db _db;
    private readonly IClock _clock;

    public TokenService(IConfiguration config, Db db, IClock clock)
    {
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!.Trim()));
        _db = db;
        _clock = clock;
    }

    public async Task<IssuedTokens> IssueAsync(long userId, SqlConnection connection, SqlTransaction? tx = null)
    {
        var now = _clock.UtcNow;
        var expires = now.Add(AccessLifetime);
        var jwt = new JwtSecurityToken(
            issuer: AuthSetup.CustomerIssuer,
            audience: AuthSetup.CustomerIssuer,
            claims: [new Claim(AuthSetup.CustomerIdClaim, userId.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))],
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        var access = new JwtSecurityTokenHandler().WriteToken(jwt);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        await connection.ExecuteAsync(
            "INSERT agents.RefreshTokens(UserId, TokenHash, ExpiresAt) VALUES (@userId, @hash, @exp)",
            new { userId, hash = Hash(refresh), exp = now.Add(RefreshLifetime) }, tx);
        return new IssuedTokens(access, expires, refresh);
    }

    /// <summary>Validates and revokes the refresh token (rotation). Returns the user id or null.</summary>
    public async Task<long?> ConsumeRefreshAsync(string refreshToken, SqlConnection connection, SqlTransaction tx)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        return await connection.QuerySingleOrDefaultAsync<long?>("""
            UPDATE agents.RefreshTokens SET RevokedAt = SYSUTCDATETIME()
            OUTPUT inserted.UserId
            WHERE TokenHash = @hash AND RevokedAt IS NULL AND ExpiresAt > SYSUTCDATETIME()
            """, new { hash = Hash(refreshToken) }, tx);
    }

    public async Task RevokeAsync(string refreshToken, long userId)
    {
        await using var connection = await _db.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE agents.RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE TokenHash = @hash AND UserId = @userId AND RevokedAt IS NULL",
            new { hash = Hash(refreshToken), userId });
    }

    public static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
