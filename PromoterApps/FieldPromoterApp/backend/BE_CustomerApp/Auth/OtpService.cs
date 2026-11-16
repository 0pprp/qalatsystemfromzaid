using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_CustomerApp.Domain.Users;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Sms;

namespace BE_CustomerApp.Auth;

public static class OtpPurposes
{
    public const string Login = "login";
    /// <summary>A user with the unlock creates an account for another person; the code goes to that person.</summary>
    public const string CreateForOther = "create_for";
}

public sealed record OtpIssued(Guid RequestId, int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record OtpRow(Guid Id, string Phone, string Purpose, byte[] CodeHash, DateTime ExpiresAt, int Attempts, DateTime? ConsumedAt, long? RequestedByUserId, string? PayloadJson);

/// <summary>
/// 6-digit OTP, HMAC-hashed at rest, 5-minute expiry, 5 attempts, 60s resend cooldown,
/// per-phone (5/hour, 10/day) and per-IP (30/hour) limits.
/// Otp:FixedCodes maps phone → code for app-store reviewer accounts (no SMS is sent for those).
/// </summary>
public sealed class OtpService
{
    public const int Lifetime = 300;
    public const int ResendAfter = 60;
    public const int MaxAttempts = 5;

    private readonly Db _db;
    private readonly ISmsSender _sms;
    private readonly IClock _clock;
    private readonly byte[] _secret;
    private readonly IConfiguration _config;

    public OtpService(Db db, ISmsSender sms, IClock clock, IConfiguration config)
    {
        _db = db;
        _sms = sms;
        _clock = clock;
        _config = config;
        _secret = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(config["Otp:Secret"]) ? config["Jwt:Key"]! : config["Otp:Secret"]!);
    }

    public async Task<OtpIssued> IssueAsync(string phone, string purpose, string? ip, Func<string, string> messageFor,
        long? requestedByUserId = null, string? payloadJson = null, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        await using var connection = await _db.OpenAsync(ct);
        var stats = await connection.QuerySingleAsync<(DateTime? Last, int Hour, int Day, int IpHour)>("""
            SELECT
              (SELECT MAX(CreatedAt) FROM agents.OtpRequests WHERE Phone = @phone),
              (SELECT COUNT(*) FROM agents.OtpRequests WHERE Phone = @phone AND CreatedAt > DATEADD(HOUR, -1, @now)),
              (SELECT COUNT(*) FROM agents.OtpRequests WHERE Phone = @phone AND CreatedAt > DATEADD(DAY, -1, @now)),
              (SELECT COUNT(*) FROM agents.OtpRequests WHERE Ip = @ip AND CreatedAt > DATEADD(HOUR, -1, @now))
            """, new { phone, now, ip });

        if (stats.Last is { } last && (now - last).TotalSeconds < ResendAfter)
            throw RateLimited((int)Math.Ceiling(ResendAfter - (now - last).TotalSeconds));
        if (stats.Hour >= 5 || stats.Day >= 10)
            throw RateLimited(stats.Day >= 10 ? 24 * 3600 : 3600);
        if (ip is not null && stats.IpHour >= 30)
            throw RateLimited(3600);

        var fixedCode = _config[$"Otp:FixedCodes:{phone}"];
        var code = fixedCode ?? RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var id = Guid.NewGuid();
        await connection.ExecuteAsync("""
            INSERT agents.OtpRequests(Id, Phone, Purpose, CodeHash, ExpiresAt, RequestedByUserId, PayloadJson, Ip, CreatedAt)
            VALUES (@id, @phone, @purpose, @hash, @expires, @requestedByUserId, @payloadJson, @ip, @now)
            """, new { id, phone, purpose, hash = HashCode(id, code), expires = now.AddSeconds(Lifetime), requestedByUserId, payloadJson, ip, now });

        if (fixedCode is null && !await _sms.SendOtpAsync(phone, code, messageFor(code), ct))
            throw new ApiException(503, "sms_failed", "تعذر إرسال رمز التحقق حالياً، يرجى المحاولة بعد قليل");

        return new OtpIssued(id, Lifetime, ResendAfter);
    }

    /// <summary>
    /// Checks the code (a wrong code always counts as an attempt). Does not consume it: the caller then
    /// calls <see cref="ConsumeAsync"/> inside its own transaction so consumption and account changes are atomic.
    /// </summary>
    public async Task<OtpRow> VerifyAsync(Guid requestId, string phone, string purpose, string code, CancellationToken ct = default)
    {
        await using var connection = await _db.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<OtpRow>(
            "SELECT Id, Phone, Purpose, CodeHash, ExpiresAt, Attempts, ConsumedAt, RequestedByUserId, PayloadJson FROM agents.OtpRequests WHERE Id = @requestId",
            new { requestId });
        if (row is null || row.Phone != phone || row.Purpose != purpose || row.ConsumedAt is not null)
            throw ApiException.BadRequest("otp_expired", "انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد");
        if (row.ExpiresAt <= _clock.UtcNow)
            throw ApiException.BadRequest("otp_expired", "انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد");
        if (row.Attempts >= MaxAttempts)
            throw ApiException.BadRequest("otp_too_many_attempts", "تم تجاوز عدد المحاولات، يرجى طلب رمز جديد");

        var normalized = new string((code ?? "").Where(char.IsAsciiDigit).ToArray());
        if (!CryptographicOperations.FixedTimeEquals(HashCode(requestId, normalized), row.CodeHash))
        {
            await connection.ExecuteAsync("UPDATE agents.OtpRequests SET Attempts = Attempts + 1 WHERE Id = @requestId", new { requestId });
            var left = MaxAttempts - row.Attempts - 1;
            throw ApiException.BadRequest("otp_invalid", left > 0 ? $"رمز التحقق غير صحيح، تبقى {left} محاولات" : "رمز التحقق غير صحيح", new { attemptsLeft = Math.Max(0, left) });
        }

        return row;
    }

    /// <summary>Marks the OTP used; fails if it was consumed concurrently (single use is guaranteed by the WHERE clause).</summary>
    public static async Task ConsumeAsync(Guid requestId, SqlConnection connection, SqlTransaction tx)
    {
        var updated = await connection.ExecuteAsync(
            "UPDATE agents.OtpRequests SET ConsumedAt = SYSUTCDATETIME() WHERE Id = @requestId AND ConsumedAt IS NULL",
            new { requestId }, tx);
        if (updated != 1) throw ApiException.BadRequest("otp_expired", "انتهت صلاحية رمز التحقق، يرجى طلب رمز جديد");
    }

    private byte[] HashCode(Guid requestId, string code) =>
        HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes($"{requestId:N}:{code}"));

    private static ApiException RateLimited(int retryAfterSeconds) =>
        new(429, "otp_rate_limited", "طلبت رموزاً كثيرة، يرجى الانتظار قليلاً ثم المحاولة", new { retryAfterSeconds });
}
