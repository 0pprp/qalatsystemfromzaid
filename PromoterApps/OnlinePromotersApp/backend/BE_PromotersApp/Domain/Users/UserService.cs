using Dapper;
using Microsoft.Data.SqlClient;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Notifications;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Users;

public sealed record DeviceInput(string? Platform, string? AppVersion, string? PushToken = null);

/// <summary>
/// The signed-in promoter. A sub-account is never told who created it or that a cut is taken: it only ever sees its
/// own commission (products, orders and wallet already show its share).
/// </summary>
public sealed record MeDto(long Id, string Username, string Phone, bool IsFull, bool CanCreateAccounts, DateTime CreatedAt);

public sealed record AuthResult(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, MeDto User);

/// <summary>A sub-account in the creator's «فريقي» list, with what the creator earned from it.</summary>
public sealed record TeamMemberDto(
    long Id, string Username, string Phone, decimal CutPercent, bool IsFull, bool IsSuspended, DateTime CreatedAt,
    int OrdersCount, int SoldCount, int OpenCount, long EarnedFromMember, DateTime? LastSaleAt);

public sealed record TeamDto(IReadOnlyList<TeamMemberDto> Items, long TotalEarned, bool CanCreateAccounts);

public sealed record CreateMemberInput(string? Username, string? Password, string? Phone, decimal? CutPercent);

public sealed record UpdateMemberInput(decimal? CutPercent);

/// <summary>Sign-in with the username and password the dashboard (or the creator's app) gave.</summary>
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
        var name = Usernames.Normalize(username);
        if (name is null || string.IsNullOrEmpty(password)) throw new ApiException(401, "invalid_credentials", BadCredentials);

        var (userId, tokens) = await _db.InTransactionAsync(async (c, tx) =>
        {
            var row = await c.QuerySingleOrDefaultAsync<(long Id, string? PasswordHash, bool IsSuspended)>(
                "SELECT Id, PasswordHash, IsSuspended FROM promoters.Users WITH (UPDLOCK) WHERE Username = @name", new { name }, tx);
            if (row.Id == 0 || !PasswordHasher.Verify(password, row.PasswordHash))
                throw new ApiException(401, "invalid_credentials", BadCredentials);
            if (row.IsSuspended) throw ApiException.Forbidden("account_suspended", "تم إيقاف هذا الحساب، يرجى مراجعة الإدارة");
            await c.ExecuteAsync("UPDATE promoters.Users SET LastLoginAt = SYSUTCDATETIME() WHERE Id = @Id", new { row.Id }, tx);
            return (row.Id, await _tokens.IssueAsync(row.Id, c, tx));
        }, ct: ct);
        if (!string.IsNullOrWhiteSpace(device?.PushToken))
            await _notifications.RegisterDeviceAsync(userId, device.Platform, device.PushToken, device.AppVersion, ct);
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
}

public sealed class UserService
{
    private readonly Db _db;
    private readonly NotificationService _notifications;

    public UserService(Db db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public static async Task<UserRow> GetAsync(long id, SqlConnection c, SqlTransaction? tx = null) =>
        await c.QuerySingleOrDefaultAsync<UserRow>($"SELECT {UserRow.Columns} FROM promoters.Users WHERE Id = @id", new { id }, tx)
        ?? throw new ApiException(401, "unauthorized", "انتهت الجلسة، يرجى تسجيل الدخول مجدداً");

    /// <summary>App endpoints call this first: suspended accounts are rejected.</summary>
    public async Task<UserRow> GetActiveAsync(long id, CancellationToken ct = default)
    {
        await using var c = await _db.OpenAsync(ct);
        var user = await GetAsync(id, c);
        if (user.IsSuspended) throw ApiException.Forbidden("account_suspended", "تم إيقاف هذا الحساب، يرجى مراجعة الإدارة");
        return user;
    }

    public async Task<MeDto> GetMeAsync(long userId, CancellationToken ct = default)
    {
        await using var c = await _db.OpenAsync(ct);
        var u = await GetAsync(userId, c);
        return new MeDto(u.Id, u.Username, u.Phone, u.IsFull, u.CanCreateAccounts, u.CreatedAt);
    }

    // ===================== «فريقي»: accounts the promoter created =====================

    public async Task<TeamDto> GetTeamAsync(long userId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var me = await GetAsync(userId, c);
        var items = (await c.QueryAsync<TeamMemberDto>("""
            SELECT u.Id, u.Username, u.Phone, CASE WHEN u.IsFull = 1 THEN CAST(0 AS DECIMAL(5,2)) ELSE u.ParentCutPercent END AS CutPercent,
                   u.IsFull, u.IsSuspended, u.CreatedAt,
                   (SELECT COUNT(*) FROM promoters.Orders o WHERE o.UserId = u.Id) AS OrdersCount,
                   (SELECT COUNT(*) FROM promoters.Orders o WHERE o.UserId = u.Id AND o.Status = 'Sold') AS SoldCount,
                   (SELECT COUNT(*) FROM promoters.Orders o WHERE o.UserId = u.Id AND o.Status IN ('New','Transferred','FieldInspection')) AS OpenCount,
                   ISNULL((SELECT SUM(w.Amount) FROM promoters.WalletEntries w WHERE w.UserId = @userId AND w.FromUserId = u.Id), 0) AS EarnedFromMember,
                   (SELECT MAX(o.SoldAt) FROM promoters.Orders o WHERE o.UserId = u.Id AND o.Status = 'Sold') AS LastSaleAt
            FROM promoters.Users u WHERE u.ParentUserId = @userId
            ORDER BY u.Id DESC
            """, new { userId })).ToList();
        return new TeamDto(items, items.Sum(i => i.EarnedFromMember), me.CanCreateAccounts);
    }

    public async Task<TeamMemberDto> CreateMemberAsync(long userId, CreateMemberInput input, CancellationToken ct)
    {
        await using (var c0 = await _db.OpenAsync(ct))
        {
            var me = await GetAsync(userId, c0);
            if (!me.CanCreateAccounts)
                throw ApiException.Forbidden("cannot_create_accounts", "لا يمكن لهذا الحساب إنشاء حسابات فرعية. تواصل مع الإدارة");
        }
        var (account, errors) = AccountRules.Validate(input.Username, input.Password, input.Phone);
        if (AccountRules.ValidateCut(input.CutPercent) is { } ce) errors["cutPercent"] = ce;
        if (errors.Count > 0) throw ApiException.Validation(errors);

        var id = await _db.InTransactionAsync((c, tx) => AccountRules.InsertAsync(c, tx, account!, userId, input.CutPercent!.Value, false, null), ct: ct);
        return (await GetTeamAsync(userId, ct)).Items.Single(m => m.Id == id);
    }

    /// <summary>The creator changes their cut on a sub-account. Applies to orders made from now on.</summary>
    public async Task<TeamMemberDto> UpdateMemberAsync(long userId, long memberId, UpdateMemberInput input, CancellationToken ct)
    {
        if (AccountRules.ValidateCut(input.CutPercent) is { } ce) throw ApiException.Validation(new() { ["cutPercent"] = ce });
        await using (var c = await _db.OpenAsync(ct))
        {
            var member = await c.QuerySingleOrDefaultAsync<UserRow>(
                $"SELECT {UserRow.Columns} FROM promoters.Users WHERE Id = @memberId AND ParentUserId = @userId", new { memberId, userId })
                ?? throw ApiException.NotFound("الحساب غير موجود");
            if (member.IsFull) throw ApiException.Conflict("member_is_full", "هذا حساب كامل، يأخذ عمولته كاملة");
            var old = member.ParentCutPercent;
            await c.ExecuteAsync("UPDATE promoters.Users SET ParentCutPercent = @cut, UpdatedAt = SYSUTCDATETIME() WHERE Id = @memberId",
                new { cut = input.CutPercent, memberId });
            // The sub-account is told its commission changed, never who changed it or the cut.
            if (old != input.CutPercent)
                await _notifications.NotifyAsync(memberId, NotificationTypes.Account, "تغيّرت عمولتك",
                    "تغيّرت عمولتك على المنتجات. اطّلع على العمولات الجديدة في الرئيسية. تُطبق على الطلبات الجديدة فقط");
        }
        return (await GetTeamAsync(userId, ct)).Items.Single(m => m.Id == memberId);
    }
}
