using System.Text;
using Dapper;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Notifications;
using BE_PromotersApp.Domain.Users;
using BE_PromotersApp.Domain.Wallet;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Admin;

public sealed record AdminUserInput(string? Username, string? Password, string? Phone, bool? IsFull);

public sealed record AdminUserRow(
    long Id, string Username, string Phone, long? ParentUserId, string? ParentUsername, decimal ParentCutPercent, bool IsFull,
    bool IsSuspended, string? SuspendReason, string? CreatedByAdmin, DateTime CreatedAt, DateTime? LastLoginAt,
    int TeamCount, int OrdersCount, int SoldCount, int OpenCount, long Balance, long TotalEarned, long TotalWithdrawn);

public sealed record AdminUserDetailDto(AdminUserRow User, IReadOnlyList<AdminUserRow> Team);

public sealed record SuspendInput(bool Suspended, string? Reason);
public sealed record PasswordInput(string? Password);
public sealed record FullInput(bool IsFull);
public sealed record CutInput(decimal? CutPercent);

/// <summary>Promoter accounts in the dashboard («المروّجون»).</summary>
public sealed class AdminUsersService
{
    private const string RowSelect = """
        u.Id, u.Username, u.Phone, u.ParentUserId, p.Username AS ParentUsername, u.ParentCutPercent, u.IsFull, u.IsSuspended, u.SuspendReason,
        u.CreatedByAdmin, u.CreatedAt, u.LastLoginAt,
        (SELECT COUNT(*) FROM promoters.Users t WHERE t.ParentUserId = u.Id) AS TeamCount,
        (SELECT COUNT(*) FROM promoters.Orders o WHERE o.UserId = u.Id) AS OrdersCount,
        (SELECT COUNT(*) FROM promoters.Orders o WHERE o.UserId = u.Id AND o.Status = 'Sold') AS SoldCount,
        (SELECT COUNT(*) FROM promoters.Orders o WHERE o.UserId = u.Id AND o.Status IN ('New','Transferred','FieldInspection')) AS OpenCount,
        ISNULL((SELECT SUM(w.Amount) FROM promoters.WalletEntries w WHERE w.UserId = u.Id), 0) AS Balance,
        ISNULL((SELECT SUM(w.Amount) FROM promoters.WalletEntries w WHERE w.UserId = u.Id AND w.Type IN ('Sale','Team','SaleReversal','TeamReversal')), 0) AS TotalEarned,
        ISNULL((SELECT SUM(x.Amount) FROM promoters.Payouts x WHERE x.UserId = u.Id AND x.Status = 'Paid'), 0) AS TotalWithdrawn
        FROM promoters.Users u LEFT JOIN promoters.Users p ON p.Id = u.ParentUserId
        """;

    private readonly Db _db;
    private readonly AuditService _audit;
    private readonly NotificationService _notifications;

    public AdminUsersService(Db db, AuditService audit, NotificationService notifications)
    {
        _db = db;
        _audit = audit;
        _notifications = notifications;
    }

    /// <param name="kind">"main" (made in the dashboard), "sub" (made by another promoter), or all.</param>
    public async Task<PagedResult<AdminUserRow>> SearchAsync(string? q, string? status, string? kind, long? parentId, Paging paging, CancellationToken ct)
    {
        var where = new StringBuilder("1 = 1");
        var args = new DynamicParameters(new { paging.Offset, paging.PageSize });
        if (!string.IsNullOrWhiteSpace(q))
        {
            where.Append(" AND (u.Username LIKE '%' + @q + '%' OR u.Phone = @phone OR p.Username LIKE '%' + @q + '%')");
            args.Add("q", Usernames.Normalize(q) ?? q.Trim());
            args.Add("phone", IraqiPhone.Normalize(q) ?? q.Trim());
        }
        where.Append(status switch { "suspended" => " AND u.IsSuspended = 1", "active" => " AND u.IsSuspended = 0", _ => "" });
        where.Append(kind switch { "main" => " AND u.ParentUserId IS NULL", "sub" => " AND u.ParentUserId IS NOT NULL", "full" => " AND u.IsFull = 1", _ => "" });
        if (parentId is not null) { where.Append(" AND u.ParentUserId = @parentId"); args.Add("parentId", parentId); }

        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM promoters.Users u LEFT JOIN promoters.Users p ON p.Id = u.ParentUserId WHERE {where};
            SELECT {RowSelect} WHERE {where} ORDER BY u.Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, args);
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AdminUserRow>((await multi.ReadAsync<AdminUserRow>()).ToList(), total, paging.Page, paging.PageSize);
    }

    public async Task<AdminUserDetailDto> GetAsync(long id, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<AdminUserRow>($"SELECT {RowSelect} WHERE u.Id = @id", new { id })
                  ?? throw ApiException.NotFound("المروّج غير موجود");
        var team = (await c.QueryAsync<AdminUserRow>($"SELECT {RowSelect} WHERE u.ParentUserId = @id ORDER BY u.Id DESC", new { id })).ToList();
        return new AdminUserDetailDto(row, team);
    }

    /// <summary>Accounts made in the dashboard have no parent; they are full accounts unless told otherwise.</summary>
    public async Task<long> CreateAsync(AdminUserInput input, AdminActor actor, CancellationToken ct)
    {
        var (account, errors) = AccountRules.Validate(input.Username, input.Password, input.Phone);
        if (errors.Count > 0) throw ApiException.Validation(errors);
        return await _db.InTransactionAsync(async (c, tx) =>
        {
            var id = await AccountRules.InsertAsync(c, tx, account!, null, 0, input.IsFull ?? true, actor.Name);
            await _audit.WriteAsync(actor, "user.create", "User", id, null, new { account!.Username, account.Phone, IsFull = input.IsFull ?? true }, c, tx);
            return id;
        }, ct: ct);
    }

    public async Task UpdateAsync(long id, AdminUserInput input, AdminActor actor, CancellationToken ct)
    {
        var (account, errors) = AccountRules.Validate(input.Username, null, input.Phone, requirePassword: false);
        if (errors.Count > 0) throw ApiException.Validation(errors);
        await _db.InTransactionAsync(async (c, tx) =>
        {
            var taken = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM promoters.Users WHERE Username = @Username AND Id <> @id", new { account!.Username, id }, tx);
            if (taken > 0) throw ApiException.Validation(new() { ["username"] = "اسم المستخدم مستخدم مسبقاً" });
            var n = await c.ExecuteAsync("UPDATE promoters.Users SET Username = @Username, Phone = @Phone, UpdatedAt = SYSUTCDATETIME() WHERE Id = @id",
                new { account.Username, account.Phone, id }, tx);
            if (n == 0) throw ApiException.NotFound("المروّج غير موجود");
            await _audit.WriteAsync(actor, "user.update", "User", id, null, new { account.Username, account.Phone }, c, tx);
            return 0;
        }, ct: ct);
    }

    /// <summary>A new password signs the promoter out of every device.</summary>
    public async Task SetPasswordAsync(long id, string? password, AdminActor actor, CancellationToken ct)
    {
        if (PasswordHasher.Validate(password) is { } pe) throw ApiException.Validation(new() { ["password"] = pe });
        await _db.InTransactionAsync(async (c, tx) =>
        {
            var n = await c.ExecuteAsync("UPDATE promoters.Users SET PasswordHash = @hash, PasswordChangedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id = @id",
                new { id, hash = PasswordHasher.Hash(password!) }, tx);
            if (n == 0) throw ApiException.NotFound("المروّج غير موجود");
            await c.ExecuteAsync("UPDATE promoters.RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE UserId = @id AND RevokedAt IS NULL", new { id }, tx);
            await _audit.WriteAsync(actor, "user.password", "User", id, null, null, c, tx);
            return 0;
        }, ct: ct);
    }

    public async Task SetSuspendedAsync(long id, SuspendInput input, AdminActor actor, CancellationToken ct)
    {
        if ((input.Reason?.Length ?? 0) > 300) throw ApiException.Validation(new() { ["reason"] = "السبب طويل جداً" });
        await _db.InTransactionAsync(async (c, tx) =>
        {
            var n = await c.ExecuteAsync("UPDATE promoters.Users SET IsSuspended = @Suspended, SuspendReason = @reason, UpdatedAt = SYSUTCDATETIME() WHERE Id = @id",
                new { id, input.Suspended, reason = input.Suspended ? Text.NullIfBlank(input.Reason) : null }, tx);
            if (n == 0) throw ApiException.NotFound("المروّج غير موجود");
            if (input.Suspended)
                await c.ExecuteAsync("UPDATE promoters.RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE UserId = @id AND RevokedAt IS NULL", new { id }, tx);
            await _audit.WriteAsync(actor, input.Suspended ? "user.suspend" : "user.activate", "User", id, null, new { input.Reason }, c, tx);
            return 0;
        }, ct: ct);
    }

    /// <summary>
    /// Full account: keeps 100% of its commission (its creator's cut stops for new orders) and may create sub-accounts.
    /// Turning it off brings the creator's cut back and stops creating new sub-accounts (existing ones stay).
    /// </summary>
    public async Task SetFullAsync(long id, bool isFull, AdminActor actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var n = await c.ExecuteAsync("UPDATE promoters.Users SET IsFull = @isFull, UpdatedAt = SYSUTCDATETIME() WHERE Id = @id AND IsFull <> @isFull", new { id, isFull });
        if (n == 0)
        {
            if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM promoters.Users WHERE Id = @id", new { id }) == 0) throw ApiException.NotFound("المروّج غير موجود");
            return;
        }
        await _audit.WriteAsync(actor, isFull ? "user.full" : "user.not_full", "User", id, null, new { isFull }, c);
        await _notifications.NotifyAsync(id, NotificationTypes.Account,
            isFull ? "أصبح حسابك حساباً كاملاً" : "تغيّر نوع حسابك",
            isFull ? "يمكنك الآن إنشاء حسابات لفريقك. اطّلع على عمولاتك في الرئيسية" : "لم يعد بإمكانك إنشاء حسابات جديدة");
    }

    /// <summary>The dashboard changes the creator's cut on a sub-account (new orders only).</summary>
    public async Task SetCutAsync(long id, decimal? cut, AdminActor actor, CancellationToken ct)
    {
        if (AccountRules.ValidateCut(cut) is { } ce) throw ApiException.Validation(new() { ["cutPercent"] = ce });
        await using var c = await _db.OpenAsync(ct);
        var n = await c.ExecuteAsync("UPDATE promoters.Users SET ParentCutPercent = @cut, UpdatedAt = SYSUTCDATETIME() WHERE Id = @id AND ParentUserId IS NOT NULL", new { id, cut });
        if (n == 0) throw ApiException.Conflict("not_sub_account", "النسبة تخص الحسابات التي أنشأها مروّج آخر فقط");
        await _audit.WriteAsync(actor, "user.cut", "User", id, null, new { cut }, c);
    }

    /// <summary>Numbers for the dashboard's overview cards.</summary>
    public async Task<object> OverviewAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var args = new { from, to, earn = EntryTypes.Earnings };
        var n = await c.QuerySingleAsync<OverviewNumbers>("""
            SELECT (SELECT COUNT(*) FROM promoters.Users) AS Users,
                   (SELECT COUNT(*) FROM promoters.Users WHERE IsSuspended = 0) AS Active,
                   (SELECT COUNT(*) FROM promoters.Users WHERE ParentUserId IS NOT NULL) AS Sub,
                   (SELECT COUNT(*) FROM promoters.Orders WHERE (@from IS NULL OR CreatedAt >= @from) AND (@to IS NULL OR CreatedAt < @to)) AS Orders,
                   (SELECT COUNT(*) FROM promoters.Orders WHERE Status = 'Sold' AND (@from IS NULL OR SoldAt >= @from) AND (@to IS NULL OR SoldAt < @to)) AS Sold,
                   (SELECT COUNT(*) FROM promoters.Orders WHERE Status IN ('New','Transferred','FieldInspection')) AS [Open],
                   (SELECT COUNT(*) FROM promoters.Orders WHERE Status = 'NotSold' AND (@from IS NULL OR UpdatedAt >= @from) AND (@to IS NULL OR UpdatedAt < @to)) AS NotSold,
                   ISNULL((SELECT SUM(Amount) FROM promoters.WalletEntries WHERE Type IN @earn AND (@from IS NULL OR CreatedAt >= @from) AND (@to IS NULL OR CreatedAt < @to)), 0) AS Commissions,
                   ISNULL((SELECT SUM(Amount) FROM promoters.Payouts WHERE Status = 'Paid' AND (@from IS NULL OR DecidedAt >= @from) AND (@to IS NULL OR DecidedAt < @to)), 0) AS Paid,
                   ISNULL((SELECT SUM(Amount) FROM promoters.Payouts WHERE Status = 'Pending'), 0) AS PendingAmount,
                   (SELECT COUNT(*) FROM promoters.Payouts WHERE Status = 'Pending') AS PendingCount,
                   ISNULL((SELECT SUM(Amount) FROM promoters.WalletEntries), 0) AS Balances
            """, args);
        var top = await c.QueryAsync<(long Id, string Username, int Sold, long Earned)>("""
            SELECT TOP 10 u.Id, u.Username,
                   (SELECT COUNT(*) FROM promoters.Orders o WHERE o.UserId = u.Id AND o.Status = 'Sold' AND (@from IS NULL OR o.SoldAt >= @from) AND (@to IS NULL OR o.SoldAt < @to)) AS Sold,
                   ISNULL((SELECT SUM(w.Amount) FROM promoters.WalletEntries w WHERE w.UserId = u.Id AND w.Type IN @earn AND (@from IS NULL OR w.CreatedAt >= @from) AND (@to IS NULL OR w.CreatedAt < @to)), 0) AS Earned
            FROM promoters.Users u ORDER BY Earned DESC, Sold DESC
            """, args);
        var products = await c.QueryAsync<(string ProductName, int Sold, long Commission)>("""
            SELECT TOP 10 ProductName, SUM(Quantity) AS Sold, SUM(CommissionTotal) AS Commission FROM promoters.Orders
            WHERE Status = 'Sold' AND (@from IS NULL OR SoldAt >= @from) AND (@to IS NULL OR SoldAt < @to)
            GROUP BY ProductName ORDER BY SUM(Quantity) DESC
            """, args);
        return new
        {
            users = n.Users, activeUsers = n.Active, subAccounts = n.Sub, orders = n.Orders, sold = n.Sold, open = n.Open, notSold = n.NotSold,
            commissions = n.Commissions, paidOut = n.Paid, pendingPayoutAmount = n.PendingAmount, pendingPayouts = n.PendingCount, balances = n.Balances,
            topPromoters = top.Where(t => t.Sold > 0 || t.Earned != 0).Select(t => new { t.Id, t.Username, t.Sold, t.Earned }),
            topProducts = products.Select(p => new { p.ProductName, p.Sold, p.Commission }),
        };
    }

    private sealed class OverviewNumbers
    {
        public int Users { get; set; }
        public int Active { get; set; }
        public int Sub { get; set; }
        public int Orders { get; set; }
        public int Sold { get; set; }
        public int Open { get; set; }
        public int NotSold { get; set; }
        public long Commissions { get; set; }
        public long Paid { get; set; }
        public long PendingAmount { get; set; }
        public int PendingCount { get; set; }
        public long Balances { get; set; }
    }
}
