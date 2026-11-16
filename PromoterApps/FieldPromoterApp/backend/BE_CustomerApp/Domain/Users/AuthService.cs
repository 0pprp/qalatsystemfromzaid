using Dapper;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Notifications;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Domain.Users;

public sealed record DeviceInput(string? Platform, string? PushToken, string? AppVersion);

public sealed record AuthResult(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, MeDto User);

/// <summary>
/// Agents sign in with the username and password the dashboard gave them. There is no self-registration and no
/// SMS sign-in: accounts exist only when an admin creates them (AdminAgentsService).
/// </summary>
public sealed class AuthService
{
    private const string BadCredentials = "اسم المستخدم أو كلمة المرور غير صحيحة";

    private readonly Db _db;
    private readonly TokenService _tokens;
    private readonly UserService _users;
    private readonly NotificationService _notifications;

    public AuthService(Db db, TokenService tokens, UserService users, NotificationService notifications)
    {
        _db = db;
        _tokens = tokens;
        _users = users;
        _notifications = notifications;
    }

    public async Task<AuthResult> LoginAsync(string? username, string? password, DeviceInput? device, CancellationToken ct)
    {
        var name = NormalizeUsername(username);
        if (name is null || string.IsNullOrEmpty(password)) throw new ApiException(401, "invalid_credentials", BadCredentials);

        var (userId, tokens) = await _db.InTransactionAsync(async (c, tx) =>
        {
            var row = await c.QuerySingleOrDefaultAsync<(long Id, string? PasswordHash, bool IsSuspended, bool IsDeleted)>(
                "SELECT Id, PasswordHash, IsSuspended, IsDeleted FROM agents.Users WITH (UPDLOCK) WHERE Username = @name", new { name }, tx);
            // Same answer for unknown user and wrong password.
            if (row.Id == 0 || row.IsDeleted || !PasswordHasher.Verify(password, row.PasswordHash))
                throw new ApiException(401, "invalid_credentials", BadCredentials);
            if (row.IsSuspended) throw ApiException.Forbidden("account_suspended", "تم إيقاف هذا الحساب، يرجى مراجعة الإدارة");
            await c.ExecuteAsync("UPDATE agents.Users SET LastLoginAt = SYSUTCDATETIME() WHERE Id = @Id", new { row.Id }, tx);
            return (row.Id, await _tokens.IssueAsync(row.Id, c, tx));
        }, ct: ct);

        if (!string.IsNullOrWhiteSpace(device?.PushToken))
            await _notifications.RegisterDeviceAsync(userId, device.Platform ?? "android", device.PushToken, device.AppVersion, ct);

        return new AuthResult(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshToken, await _users.GetMeAsync(userId, ct));
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        var (userId, tokens) = await _db.InTransactionAsync(async (c, tx) =>
        {
            var id = await _tokens.ConsumeRefreshAsync(refreshToken ?? "", c, tx)
                     ?? throw new ApiException(401, "invalid_refresh", "انتهت الجلسة، يرجى تسجيل الدخول مجدداً");
            var user = await UserService.GetAsync(id, c, tx);
            if (user.IsSuspended) throw ApiException.Forbidden("account_suspended", "تم إيقاف هذا الحساب، يرجى مراجعة الإدارة");
            return (id, await _tokens.IssueAsync(id, c, tx));
        }, ct: ct);
        return new AuthResult(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshToken, await _users.GetMeAsync(userId, ct));
    }

    /// <summary>Usernames are case-insensitive: stored and compared in lower case.</summary>
    /// <summary>
    /// Usernames may be Arabic or English. Case (English), surrounding spaces, Arabic diacritics and tatweel (ـ) are
    /// ignored, and Arabic-Indic digits become 0-9, so «علي١» and «عَلي1» sign in as the same user.
    /// </summary>
    public static string? NormalizeUsername(string? username)
    {
        if (username is null) return null;
        var sb = new System.Text.StringBuilder(username.Length);
        foreach (var ch in username.Trim())
        {
            int code = ch;
            if (code is 0x0640 or (>= 0x064B and <= 0x065F) or 0x0670) continue; // tatweel, harakat
            if (code is >= 0x0660 and <= 0x0669) sb.Append((char)('0' + code - 0x0660)); // Arabic-Indic digits
            else if (code is >= 0x06F0 and <= 0x06F9) sb.Append((char)('0' + code - 0x06F0)); // Persian digits
            else sb.Append(char.ToLowerInvariant(ch));
        }
        var u = sb.ToString();
        return string.IsNullOrEmpty(u) ? null : u;
    }
}
