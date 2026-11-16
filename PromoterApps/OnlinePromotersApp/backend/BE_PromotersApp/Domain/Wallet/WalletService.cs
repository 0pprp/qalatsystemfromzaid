using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Admin;
using BE_PromotersApp.Domain.Notifications;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Wallet;

public static class EntryTypes
{
    public const string Sale = "Sale";
    public const string Team = "Team";
    public const string SaleReversal = "SaleReversal";
    public const string TeamReversal = "TeamReversal";
    public const string Payout = "Payout";
    public const string PayoutReturned = "PayoutReturned";
    public const string Adjustment = "Adjustment";

    public static readonly string[] Earnings = [Sale, Team, SaleReversal, TeamReversal];

    public static string Label(string type) => type switch
    {
        Sale => "عمولة بيع",
        Team => "عمولة من فريقي",
        SaleReversal => "إلغاء عمولة بيع",
        TeamReversal => "إلغاء عمولة من فريقي",
        Payout => "طلب سحب",
        PayoutReturned => "إرجاع مبلغ طلب سحب",
        Adjustment => "تعديل من الإدارة",
        _ => type,
    };
}

public static class PayoutStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Rejected = "Rejected";

    public static string Label(string s) => s switch { Pending => "قيد المعالجة", Paid => "تم التحويل", Rejected => "مرفوض", _ => s };
}

public static class PayoutMethods
{
    public const string QiCard = "QiCard";
    public const string ZainCash = "ZainCash";

    public static string Label(string m) => m == ZainCash ? "زين كاش" : "كي كارد";
}

public sealed record WalletSummaryDto(long Balance, long TotalEarned, long EarnedThisMonth, long TotalWithdrawn, long PendingWithdrawal, long MinPayout);

public sealed record WalletEntryDto(
    long Id, long Amount, string Type, string TypeLabel, string? Note, long? OrderId, string? OrderNumber, string? ProductName,
    long? FromUserId, string? FromUsername, long? PayoutId, DateTime CreatedAt);

public sealed record PayoutDto(
    long Id, long Amount, string Method, string MethodLabel, string AccountNumber, string Status, string StatusLabel, string? AdminNote,
    DateTime? DecidedAt, DateTime CreatedAt, DateTime? LastMessageAt, int UnreadMessages, bool HasReceipt = false);

/// <summary>A chat message; AttachmentKind is "image" or "file" when a photo or document is attached (download it from …/attachment).</summary>
public sealed record PayoutMessageDto(
    long Id, bool FromAdmin, string SenderName, string Body, DateTime CreatedAt, DateTime? ReadAt,
    string? AttachmentName, string? AttachmentKind, string? AttachmentContentType, long? AttachmentSize);

public sealed record PayoutDetailDto(PayoutDto Payout, IReadOnlyList<PayoutMessageDto> Messages);

public sealed record PayoutInput(long? Amount, string? Method, string? AccountNumber);

public sealed record MessageInput(string? Body);

public sealed record AdminPayoutRow(
    long Id, long UserId, string Username, string Phone, long Amount, string Method, string MethodLabel, string AccountNumber,
    string Status, string StatusLabel, string? AdminNote, string? DecidedBy, DateTime? DecidedAt, DateTime CreatedAt,
    DateTime? LastMessageAt, int UnreadMessages, long Balance, bool HasReceipt);

public sealed record AdminPayoutDetailDto(AdminPayoutRow Payout, IReadOnlyList<PayoutMessageDto> Messages);

public sealed record AdjustmentInput(long? Amount, string? Note);

/// <summary>
/// The promoters' wallets. Every change is a row in promoters.WalletEntries; the balance is their sum.
/// Commission is added only when an order becomes a sale (and taken back if the company cancels that sale).
/// A withdrawal request takes its amount out of the balance at once; rejecting it puts the amount back.
/// </summary>
public sealed class WalletService
{
    private readonly Db _db;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;
    private readonly AppSettingsService _settings;

    public WalletService(Db db, NotificationService notifications, AuditService audit, AppSettingsService settings)
    {
        _db = db;
        _notifications = notifications;
        _audit = audit;
        _settings = settings;
    }

    public static Task<long> BalanceAsync(SqlConnection c, long userId, SqlTransaction? tx = null) =>
        c.ExecuteScalarAsync<long>("SELECT ISNULL(SUM(Amount), 0) FROM promoters.WalletEntries WHERE UserId = @userId", new { userId }, tx);

    /// <summary>Called inside the transaction that marks an order Sold. Idempotent (unique per account, order and type).</summary>
    public static async Task CreditSaleAsync(SqlConnection c, SqlTransaction tx, long orderId, long sellerId, long sellerAmount, long? parentId, long parentAmount)
    {
        await c.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM promoters.WalletEntries WHERE UserId = @sellerId AND OrderId = @orderId AND Type = 'Sale')
                INSERT promoters.WalletEntries(UserId, Amount, Type, OrderId) VALUES (@sellerId, @sellerAmount, 'Sale', @orderId)
            """, new { orderId, sellerId, sellerAmount }, tx);
        if (parentId is not null && parentAmount > 0)
            await c.ExecuteAsync("""
                IF NOT EXISTS (SELECT 1 FROM promoters.WalletEntries WHERE UserId = @parentId AND OrderId = @orderId AND Type = 'Team')
                    INSERT promoters.WalletEntries(UserId, Amount, Type, OrderId, FromUserId) VALUES (@parentId, @parentAmount, 'Team', @orderId, @sellerId)
                """, new { orderId, sellerId, parentId, parentAmount }, tx);
    }

    /// <summary>The company cancelled a sale: whatever it credited is taken back (once).</summary>
    public static async Task ReverseSaleAsync(SqlConnection c, SqlTransaction tx, long orderId, string? note)
    {
        await c.ExecuteAsync("""
            INSERT promoters.WalletEntries(UserId, Amount, Type, OrderId, FromUserId, Note)
            SELECT w.UserId, -w.Amount, CASE w.Type WHEN 'Sale' THEN 'SaleReversal' ELSE 'TeamReversal' END, w.OrderId, w.FromUserId, @note
            FROM promoters.WalletEntries w
            WHERE w.OrderId = @orderId AND w.Type IN ('Sale','Team')
              AND NOT EXISTS (SELECT 1 FROM promoters.WalletEntries r WHERE r.UserId = w.UserId AND r.OrderId = w.OrderId
                              AND r.Type = CASE w.Type WHEN 'Sale' THEN 'SaleReversal' ELSE 'TeamReversal' END)
            """, new { orderId, note = note is null ? null : Text.Truncate(note, 300) }, tx);
    }

    // ===================== App =====================

    public async Task<WalletSummaryDto> SummaryAsync(long userId, CancellationToken ct)
    {
        var monthStart = IraqTime.MonthStartUtc(DateTime.UtcNow);
        var minPayout = (await _settings.GetAsync(ct)).MinPayout;
        await using var c = await _db.OpenAsync(ct);
        var s = await c.QuerySingleAsync<(long Balance, long Earned, long Month, long Withdrawn, long Pending)>("""
            SELECT
              ISNULL((SELECT SUM(Amount) FROM promoters.WalletEntries WHERE UserId = @userId), 0),
              ISNULL((SELECT SUM(Amount) FROM promoters.WalletEntries WHERE UserId = @userId AND Type IN @earn), 0),
              ISNULL((SELECT SUM(Amount) FROM promoters.WalletEntries WHERE UserId = @userId AND Type IN @earn AND CreatedAt >= @monthStart), 0),
              ISNULL((SELECT SUM(Amount) FROM promoters.Payouts WHERE UserId = @userId AND Status = 'Paid'), 0),
              ISNULL((SELECT SUM(Amount) FROM promoters.Payouts WHERE UserId = @userId AND Status = 'Pending'), 0)
            """, new { userId, earn = EntryTypes.Earnings, monthStart });
        return new WalletSummaryDto(s.Balance, s.Earned, s.Month, s.Withdrawn, s.Pending, minPayout);
    }

    public async Task<PagedResult<WalletEntryDto>> EntriesAsync(long userId, Paging paging, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await EntriesAsync(c, userId, paging);
    }

    private static async Task<PagedResult<WalletEntryDto>> EntriesAsync(SqlConnection c, long userId, Paging paging)
    {
        using var multi = await c.QueryMultipleAsync("""
            SELECT COUNT(*) FROM promoters.WalletEntries WHERE UserId = @userId;
            SELECT w.Id, w.Amount, w.Type, w.Note, w.OrderId, o.OrderNumber, o.ProductName, w.FromUserId, f.Username AS FromUsername, w.PayoutId, w.CreatedAt
            FROM promoters.WalletEntries w
            LEFT JOIN promoters.Orders o ON o.Id = w.OrderId
            LEFT JOIN promoters.Users f ON f.Id = w.FromUserId
            WHERE w.UserId = @userId ORDER BY w.Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { userId, paging.Offset, paging.PageSize });
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<(long Id, long Amount, string Type, string? Note, long? OrderId, string? OrderNumber, string? ProductName,
                long? FromUserId, string? FromUsername, long? PayoutId, DateTime CreatedAt)>())
            .Select(r => new WalletEntryDto(r.Id, r.Amount, r.Type, EntryTypes.Label(r.Type), r.Note, r.OrderId, r.OrderNumber, r.ProductName,
                r.FromUserId, r.FromUsername, r.PayoutId, r.CreatedAt))
            .ToList();
        return new PagedResult<WalletEntryDto>(rows, total, paging.Page, paging.PageSize);
    }

    public async Task<PagedResult<PayoutDto>> PayoutsAsync(long userId, Paging paging, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM promoters.Payouts WHERE UserId = @userId;
            SELECT {PayoutSelect(false)} FROM promoters.Payouts p WHERE p.UserId = @userId
            ORDER BY p.Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, new { userId, paging.Offset, paging.PageSize });
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<PayoutRaw>()).Select(ToDto).ToList();
        return new PagedResult<PayoutDto>(rows, total, paging.Page, paging.PageSize);
    }

    public async Task<PayoutDetailDto> CreatePayoutAsync(long userId, PayoutInput input, CancellationToken ct)
    {
        var minPayout = (await _settings.GetAsync(ct)).MinPayout;
        var e = new Dictionary<string, string>();
        var method = input.Method is PayoutMethods.QiCard or PayoutMethods.ZainCash ? input.Method : null;
        if (method is null) e["method"] = "اختر طريقة الاستلام";
        var account = new string((input.AccountNumber ?? "").Where(ch => !char.IsWhiteSpace(ch) && ch != '-').ToArray());
        account = string.Concat(account.Select(ch => ch is >= '٠' and <= '٩' ? (char)('0' + ch - '٠') : ch is >= '۰' and <= '۹' ? (char)('0' + ch - '۰') : ch));
        if (method == PayoutMethods.ZainCash)
        {
            var phone = Users.IraqiPhone.Normalize(account);
            if (phone is null) e["accountNumber"] = "أدخل رقم زين كاش (07XXXXXXXXX)";
            else account = phone;
        }
        else if (method == PayoutMethods.QiCard && (account.Length is < QiCardMinDigits or > QiCardMaxDigits || !account.All(char.IsAsciiDigit)))
        {
            e["accountNumber"] = QiCardRule;
        }
        if (input.Amount is null or <= 0) e["amount"] = "أدخل المبلغ";
        else if (input.Amount < minPayout) e["amount"] = $"أقل مبلغ للسحب {minPayout:N0} د.ع";
        if (e.Count > 0) throw ApiException.Validation(e);

        long payoutId;
        try
        {
            payoutId = await _db.InTransactionAsync(async (c, tx) =>
            {
                // Serialises requests of the same promoter so two at once cannot both spend the balance.
                await c.ExecuteAsync("SELECT Id FROM promoters.Users WITH (UPDLOCK, HOLDLOCK) WHERE Id = @userId", new { userId }, tx);
                if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM promoters.Payouts WHERE UserId = @userId AND Status = 'Pending'", new { userId }, tx) > 0)
                    throw ApiException.Conflict("payout_pending", "لديك طلب سحب قيد المعالجة. انتظر إكماله قبل طلب جديد");
                var balance = await BalanceAsync(c, userId, tx);
                if (input.Amount > balance)
                    throw ApiException.Validation(new() { ["amount"] = $"المبلغ أكبر من رصيدك ({Math.Max(0, balance):N0} د.ع)" });
                var id = await c.ExecuteScalarAsync<long>("""
                    INSERT promoters.Payouts(UserId, Amount, Method, AccountNumber, Status) OUTPUT inserted.Id
                    VALUES (@userId, @Amount, @method, @account, 'Pending')
                    """, new { userId, input.Amount, method, account }, tx);
                await c.ExecuteAsync("INSERT promoters.WalletEntries(UserId, Amount, Type, PayoutId) VALUES (@userId, @amount, 'Payout', @id)",
                    new { userId, amount = -input.Amount!.Value, id }, tx);
                return id;
            }, ct: ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Payouts_OnePending"))
        {
            throw ApiException.Conflict("payout_pending", "لديك طلب سحب قيد المعالجة. انتظر إكماله قبل طلب جديد");
        }
        return await GetPayoutAsync(userId, payoutId, ct);
    }

    /// <summary>The request and its chat. Opening it marks the dashboard's messages as read.</summary>
    public async Task<PayoutDetailDto> GetPayoutAsync(long userId, long payoutId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync("""
            UPDATE m SET ReadAt = SYSUTCDATETIME() FROM promoters.PayoutMessages m JOIN promoters.Payouts p ON p.Id = m.PayoutId
            WHERE m.PayoutId = @payoutId AND p.UserId = @userId AND m.FromAdmin = 1 AND m.ReadAt IS NULL
            """, new { payoutId, userId });
        var raw = await c.QuerySingleOrDefaultAsync<PayoutRaw>($"SELECT {PayoutSelect(false)} FROM promoters.Payouts p WHERE p.Id = @payoutId AND p.UserId = @userId",
                      new { payoutId, userId }) ?? throw ApiException.NotFound("طلب السحب غير موجود");
        return new PayoutDetailDto(ToDto(raw), await MessagesAsync(c, payoutId, 0));
    }

    /// <summary>New messages after <paramref name="afterId"/> (the app polls this while the chat is open).</summary>
    public async Task<IReadOnlyList<PayoutMessageDto>> MessagesSinceAsync(long? userId, long payoutId, long afterId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var owner = await c.ExecuteScalarAsync<long?>("SELECT UserId FROM promoters.Payouts WHERE Id = @payoutId", new { payoutId });
        if (owner is null || (userId is not null && owner != userId)) throw ApiException.NotFound("طلب السحب غير موجود");
        // Whoever is polling has now seen the other side's messages.
        await c.ExecuteAsync("UPDATE promoters.PayoutMessages SET ReadAt = SYSUTCDATETIME() WHERE PayoutId = @payoutId AND FromAdmin = @fromAdmin AND ReadAt IS NULL",
            new { payoutId, fromAdmin = userId is not null });
        return await MessagesAsync(c, payoutId, afterId);
    }

    public async Task<PayoutMessageDto> UserMessageAsync(long userId, long payoutId, MessageInput input, CancellationToken ct,
        IFormFile? file = null, string? privateRoot = null)
    {
        var attachment = file is null ? null : await ReadAttachmentAsync(file, ct);
        var body = ValidateMessage(input, attachment is not null);
        await using var c = await _db.OpenAsync(ct);
        var username = await c.ExecuteScalarAsync<string?>("""
            SELECT u.Username FROM promoters.Payouts p JOIN promoters.Users u ON u.Id = p.UserId WHERE p.Id = @payoutId AND p.UserId = @userId
            """, new { payoutId, userId }) ?? throw ApiException.NotFound("طلب السحب غير موجود");
        var stored = attachment is null ? null : await SaveAttachmentAsync(attachment, payoutId, privateRoot!, ct);
        return await AddMessageAsync(c, payoutId, false, username, body, stored);
    }

    /// <summary>
    /// The file attached to a chat message. <paramref name="userId"/> null = dashboard; otherwise only the request's owner.
    /// </summary>
    public async Task<(string Path, string ContentType, string Name)> AttachmentAsync(long? userId, long payoutId, long messageId, string privateRoot, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var r = await c.QuerySingleOrDefaultAsync<(long UserId, string? AttachmentPath, string? AttachmentContentType, string? AttachmentName)>("""
            SELECT p.UserId, m.AttachmentPath, m.AttachmentContentType, m.AttachmentName
            FROM promoters.PayoutMessages m JOIN promoters.Payouts p ON p.Id = m.PayoutId
            WHERE m.Id = @messageId AND m.PayoutId = @payoutId
            """, new { messageId, payoutId });
        if (r.UserId == 0 || (userId is not null && r.UserId != userId) || r.AttachmentPath is null) throw ApiException.NotFound("الملف غير موجود");
        var root = Path.GetFullPath(privateRoot);
        var full = Path.GetFullPath(Path.Combine(root, r.AttachmentPath));
        if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full)) throw ApiException.NotFound("الملف غير موجود");
        return (full, r.AttachmentContentType ?? "application/octet-stream", r.AttachmentName ?? Path.GetFileName(full));
    }

    // ===================== Dashboard =====================

    public async Task<PagedResult<AdminPayoutRow>> AdminListAsync(string? status, long? userId, string? q, Paging paging, CancellationToken ct)
    {
        var where = new StringBuilder("1 = 1");
        var args = new DynamicParameters(new { paging.Offset, paging.PageSize });
        if (!string.IsNullOrWhiteSpace(status)) { where.Append(" AND p.Status = @status"); args.Add("status", status); }
        if (userId is not null) { where.Append(" AND p.UserId = @userId"); args.Add("userId", userId); }
        if (!string.IsNullOrWhiteSpace(q))
        {
            where.Append(" AND (u.Username LIKE '%' + @q + '%' OR u.Phone LIKE '%' + @q + '%' OR p.AccountNumber LIKE '%' + @q + '%')");
            args.Add("q", q.Trim());
        }
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM promoters.Payouts p JOIN promoters.Users u ON u.Id = p.UserId WHERE {where};
            SELECT {AdminSelect} FROM promoters.Payouts p JOIN promoters.Users u ON u.Id = p.UserId WHERE {where}
            ORDER BY CASE WHEN p.Status = 'Pending' THEN 0 ELSE 1 END, ISNULL(p.LastMessageAt, p.CreatedAt) DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, args);
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AdminPayoutRow>((await multi.ReadAsync<AdminPayoutRow>()).ToList(), total, paging.Page, paging.PageSize);
    }

    /// <summary>For the dashboard badge: open requests and unread promoter messages.</summary>
    public async Task<object> AdminCountsAsync(CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var r = await c.QuerySingleAsync<(int Pending, int Paid, int Rejected, int Unread)>("""
            SELECT (SELECT COUNT(*) FROM promoters.Payouts WHERE Status = 'Pending'),
                   (SELECT COUNT(*) FROM promoters.Payouts WHERE Status = 'Paid'),
                   (SELECT COUNT(*) FROM promoters.Payouts WHERE Status = 'Rejected'),
                   (SELECT COUNT(*) FROM promoters.PayoutMessages WHERE FromAdmin = 0 AND ReadAt IS NULL)
            """);
        return new { pending = r.Pending, paid = r.Paid, rejected = r.Rejected, unreadMessages = r.Unread, all = r.Pending + r.Paid + r.Rejected };
    }

    public async Task<AdminPayoutDetailDto> AdminGetAsync(long payoutId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        await c.ExecuteAsync("UPDATE promoters.PayoutMessages SET ReadAt = SYSUTCDATETIME() WHERE PayoutId = @payoutId AND FromAdmin = 0 AND ReadAt IS NULL", new { payoutId });
        var row = await c.QuerySingleOrDefaultAsync<AdminPayoutRow>(
                      $"SELECT {AdminSelect} FROM promoters.Payouts p JOIN promoters.Users u ON u.Id = p.UserId WHERE p.Id = @payoutId", new { payoutId })
                  ?? throw ApiException.NotFound("طلب السحب غير موجود");
        return new AdminPayoutDetailDto(row, await MessagesAsync(c, payoutId, 0));
    }

    public async Task<PayoutMessageDto> AdminMessageAsync(long payoutId, MessageInput input, AdminActor actor, CancellationToken ct,
        IFormFile? file = null, string? privateRoot = null)
    {
        var attachment = file is null ? null : await ReadAttachmentAsync(file, ct);
        var body = ValidateMessage(input, attachment is not null);
        await using var c = await _db.OpenAsync(ct);
        var userId = await c.ExecuteScalarAsync<long?>("SELECT UserId FROM promoters.Payouts WHERE Id = @payoutId", new { payoutId })
                     ?? throw ApiException.NotFound("طلب السحب غير موجود");
        var stored = attachment is null ? null : await SaveAttachmentAsync(attachment, payoutId, privateRoot!, ct);
        var msg = await AddMessageAsync(c, payoutId, true, "الإدارة", body, stored);
        var preview = body.Length > 0 ? Text.Truncate(body, 200) : attachment!.Kind == "image" ? "📷 صورة" : $"📎 {attachment.Name}";
        await _notifications.NotifyAsync(userId, NotificationTypes.Payout, "رسالة جديدة بخصوص طلب السحب", preview,
            new Dictionary<string, string> { ["payoutId"] = payoutId.ToString() });
        return msg;
    }

    /// <summary>«تم التحويل»: needs a picture of the transfer receipt (JPEG, PNG or WebP, 10 MB at most).</summary>
    public async Task<AdminPayoutDetailDto> MarkPaidAsync(long payoutId, string? note, IFormFile? receipt, string receiptsRoot, AdminActor actor, CancellationToken ct)
    {
        var (bytes, ext, contentType) = await ReadReceiptAsync(receipt, ct);
        var relative = $"{DateTime.UtcNow:yyyy-MM}/{payoutId}-{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(receiptsRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, bytes, ct);
        (long UserId, long Amount, string Method, string AccountNumber) p;
        try
        {
            p = await DecideAsync(payoutId, PayoutStatus.Paid, note, actor, ct, relative, contentType);
        }
        catch
        {
            File.Delete(full); // not marked paid: do not keep an orphan receipt
            throw;
        }
        await _notifications.NotifyAsync(p.UserId, NotificationTypes.Payout, "تم تحويل مبلغ السحب ✅",
            $"تم تحويل {p.Amount:N0} د.ع إلى {PayoutMethods.Label(p.Method)} {p.AccountNumber}. وصل التحويل في طلب السحب"
            + (string.IsNullOrWhiteSpace(note) ? "" : $". {note.Trim()}"),
            new Dictionary<string, string> { ["payoutId"] = payoutId.ToString() });
        return await AdminGetAsync(payoutId, ct);
    }

    /// <summary>The receipt of a paid request. <paramref name="userId"/> null = dashboard; otherwise only the request's owner.</summary>
    public async Task<(string Path, string ContentType)> ReceiptAsync(long? userId, long payoutId, string receiptsRoot, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var r = await c.QuerySingleOrDefaultAsync<(long UserId, string? ReceiptPath, string? ReceiptContentType)>(
            "SELECT UserId, ReceiptPath, ReceiptContentType FROM promoters.Payouts WHERE Id = @payoutId", new { payoutId });
        if (r.UserId == 0 || (userId is not null && r.UserId != userId) || r.ReceiptPath is null) throw ApiException.NotFound("لا يوجد وصل لهذا الطلب");
        var root = Path.GetFullPath(receiptsRoot);
        var full = Path.GetFullPath(Path.Combine(root, r.ReceiptPath));
        if (!full.StartsWith(root, StringComparison.Ordinal) || !File.Exists(full)) throw ApiException.NotFound("لا يوجد وصل لهذا الطلب");
        return (full, r.ReceiptContentType ?? "image/jpeg");
    }

    public const int QiCardMinDigits = 10;
    public const int QiCardMaxDigits = 13;
    public const string QiCardRule = "رقم كي كارد من 10 إلى 13 رقماً (أرقام فقط)";
    private const long MaxReceiptBytes = 10 * 1024 * 1024;

    private static async Task<(byte[] Bytes, string Ext, string ContentType)> ReadReceiptAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw ApiException.Validation(new() { ["receipt"] = "أرفق صورة وصل التحويل" });
        if (file.Length > MaxReceiptBytes) throw ApiException.Validation(new() { ["receipt"] = "حجم الصورة يجب ألا يتجاوز 10 ميغابايت" });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var b = ms.ToArray();
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return (b, ".jpg", "image/jpeg");
        if (b.Length >= 4 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return (b, ".png", "image/png");
        if (b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return (b, ".webp", "image/webp");
        throw ApiException.Validation(new() { ["receipt"] = "الملف ليس صورة (JPG أو PNG أو WEBP)" });
    }

    public async Task<AdminPayoutDetailDto> RejectAsync(long payoutId, string? reason, AdminActor actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
            throw ApiException.Validation(new() { ["reason"] = "اكتب سبب الرفض الذي سيظهر للمروّج" });
        var p = await DecideAsync(payoutId, PayoutStatus.Rejected, reason, actor, ct);
        await _notifications.NotifyAsync(p.UserId, NotificationTypes.Payout, "تعذر تنفيذ طلب السحب",
            $"أُعيد مبلغ {p.Amount:N0} د.ع إلى رصيدك. السبب: {reason.Trim()}",
            new Dictionary<string, string> { ["payoutId"] = payoutId.ToString() });
        return await AdminGetAsync(payoutId, ct);
    }

    public async Task<PagedResult<WalletEntryDto>> AdminEntriesAsync(long userId, Paging paging, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        return await EntriesAsync(c, userId, paging);
    }

    /// <summary>A manual correction by the dashboard (positive adds, negative deducts). Shown to the promoter with its note.</summary>
    public async Task<WalletSummaryDto> AdjustAsync(long userId, AdjustmentInput input, AdminActor actor, CancellationToken ct)
    {
        var e = new Dictionary<string, string>();
        if (input.Amount is null or 0 || Math.Abs(input.Amount.Value) > 1_000_000_000) e["amount"] = "أدخل المبلغ (سالب للخصم)";
        var note = input.Note?.Trim() ?? "";
        if (note.Length is < 3 or > 300) e["note"] = "اكتب سبب التعديل";
        if (e.Count > 0) throw ApiException.Validation(e);
        await _db.InTransactionAsync(async (c, tx) =>
        {
            if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM promoters.Users WHERE Id = @userId", new { userId }, tx) == 0)
                throw ApiException.NotFound("المروّج غير موجود");
            await c.ExecuteAsync("INSERT promoters.WalletEntries(UserId, Amount, Type, Note, CreatedBy) VALUES (@userId, @Amount, 'Adjustment', @note, @Name)",
                new { userId, input.Amount, note, actor.Name }, tx);
            await _audit.WriteAsync(actor, "wallet.adjust", "User", userId, null, new { input.Amount, note }, c, tx);
            return 0;
        }, ct: ct);
        await _notifications.NotifyAsync(userId, NotificationTypes.Wallet, input.Amount > 0 ? "أضيف مبلغ إلى رصيدك" : "خُصم مبلغ من رصيدك",
            $"{Math.Abs(input.Amount!.Value):N0} د.ع — {note}");
        return await SummaryAsync(userId, ct);
    }

    // ===================== Helpers =====================

    private async Task<(long UserId, long Amount, string Method, string AccountNumber)> DecideAsync(
        long payoutId, string status, string? note, AdminActor actor, CancellationToken ct, string? receiptPath = null, string? receiptType = null)
    {
        return await _db.InTransactionAsync(async (c, tx) =>
        {
            var p = await c.QuerySingleOrDefaultAsync<(long UserId, long Amount, string Method, string AccountNumber, string Status)>(
                        "SELECT UserId, Amount, Method, AccountNumber, Status FROM promoters.Payouts WITH (UPDLOCK) WHERE Id = @payoutId", new { payoutId }, tx);
            if (p.UserId == 0) throw ApiException.NotFound("طلب السحب غير موجود");
            if (p.Status != PayoutStatus.Pending)
                throw ApiException.Conflict("invalid_state", $"تمت معالجة هذا الطلب مسبقاً ({PayoutStatus.Label(p.Status)})");
            var cleanNote = Text.NullIfBlank(note) is { } n ? Text.Truncate(n, 400) : null;
            await c.ExecuteAsync("""
                UPDATE promoters.Payouts SET Status = @status, AdminNote = @cleanNote, DecidedBy = @Name, DecidedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME(),
                       ReceiptPath = COALESCE(@receiptPath, ReceiptPath), ReceiptContentType = COALESCE(@receiptType, ReceiptContentType)
                WHERE Id = @payoutId
                """, new { status, cleanNote, actor.Name, payoutId, receiptPath, receiptType }, tx);
            if (status == PayoutStatus.Rejected)
                await c.ExecuteAsync("INSERT promoters.WalletEntries(UserId, Amount, Type, PayoutId, Note, CreatedBy) VALUES (@UserId, @Amount, 'PayoutReturned', @payoutId, @cleanNote, @Name)",
                    new { p.UserId, p.Amount, payoutId, cleanNote, actor.Name }, tx);
            await _audit.WriteAsync(actor, status == PayoutStatus.Paid ? "payout.paid" : "payout.reject", "Payout", payoutId,
                new { p.Status }, new { Status = status, note = cleanNote, p.Amount }, c, tx);
            return (p.UserId, p.Amount, p.Method, p.AccountNumber);
        }, ct: ct);
    }

    /// <summary>A message needs text, unless a photo or document is attached.</summary>
    private static string ValidateMessage(MessageInput input, bool hasAttachment = false)
    {
        var body = input.Body?.Trim() ?? "";
        if ((body.Length == 0 && !hasAttachment) || body.Length > 1000) throw ApiException.Validation(new() { ["body"] = "اكتب الرسالة (1000 حرف كحد أقصى)" });
        return body;
    }

    public const long MaxAttachmentBytes = 15L * 1024 * 1024;

    private sealed record Attachment(byte[] Bytes, string Name, string Kind, string Ext, string ContentType);

    private sealed record StoredAttachment(string Path, string Name, string Kind, string ContentType, long Size);

    /// <summary>Photos (JPEG, PNG, WebP) and documents (PDF, Word, Excel), checked by their content, 15 MB at most.</summary>
    private static async Task<Attachment> ReadAttachmentAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) throw ApiException.Validation(new() { ["file"] = "الملف فارغ" });
        if (file.Length > MaxAttachmentBytes) throw ApiException.Validation(new() { ["file"] = "حجم الملف يجب ألا يتجاوز 15 ميغابايت" });
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var b = ms.ToArray();
        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        var name = Text.Truncate(Path.GetFileName(file.FileName ?? "") is { Length: > 0 } n ? n : "ملف", 200);
        bool Starts(params byte[] sig) => b.Length >= sig.Length && b.AsSpan(0, sig.Length).SequenceEqual(sig);

        if (Starts(0xFF, 0xD8, 0xFF)) return new(b, name, "image", ".jpg", "image/jpeg");
        if (Starts(0x89, 0x50, 0x4E, 0x47)) return new(b, name, "image", ".png", "image/png");
        if (b.Length >= 12 && Starts(0x52, 0x49, 0x46, 0x46) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return new(b, name, "image", ".webp", "image/webp");
        if (Starts(0x25, 0x50, 0x44, 0x46)) return new(b, name, "file", ".pdf", "application/pdf");
        // Word / Excel: .docx and .xlsx are ZIP files, .doc and .xls are OLE compound files.
        if (Starts(0x50, 0x4B, 0x03, 0x04) && ext is ".docx" or ".xlsx")
            return new(b, name, "file", ext, ext == ".docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        if (Starts(0xD0, 0xCF, 0x11, 0xE0) && ext is ".doc" or ".xls")
            return new(b, name, "file", ext, ext == ".doc" ? "application/msword" : "application/vnd.ms-excel");
        throw ApiException.Validation(new() { ["file"] = "الملفات المسموحة: صور (JPG, PNG, WEBP) أو مستندات (PDF, Word, Excel)" });
    }

    private static async Task<StoredAttachment> SaveAttachmentAsync(Attachment a, long payoutId, string privateRoot, CancellationToken ct)
    {
        var relative = $"chat/{DateTime.UtcNow:yyyy-MM}/{payoutId}-{Guid.NewGuid():N}{a.Ext}";
        var full = Path.Combine(privateRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, a.Bytes, ct);
        return new StoredAttachment(relative, a.Name, a.Kind, a.ContentType, a.Bytes.LongLength);
    }

    private const string MessageColumns =
        "Id, FromAdmin, SenderName, Body, CreatedAt, ReadAt, AttachmentName, AttachmentKind, AttachmentContentType, AttachmentSize";

    private static async Task<PayoutMessageDto> AddMessageAsync(SqlConnection c, long payoutId, bool fromAdmin, string sender, string body,
        StoredAttachment? file = null)
    {
        var msg = await c.QuerySingleAsync<PayoutMessageDto>("""
            INSERT promoters.PayoutMessages(PayoutId, FromAdmin, SenderName, Body, AttachmentPath, AttachmentName, AttachmentKind, AttachmentContentType, AttachmentSize)
            OUTPUT inserted.Id, inserted.FromAdmin, inserted.SenderName, inserted.Body, inserted.CreatedAt, inserted.ReadAt,
                   inserted.AttachmentName, inserted.AttachmentKind, inserted.AttachmentContentType, inserted.AttachmentSize
            VALUES (@payoutId, @fromAdmin, @sender, @body, @path, @name, @kind, @type, @size)
            """, new { payoutId, fromAdmin, sender, body, path = file?.Path, name = file?.Name, kind = file?.Kind, type = file?.ContentType, size = file?.Size });
        await c.ExecuteAsync("UPDATE promoters.Payouts SET LastMessageAt = SYSUTCDATETIME() WHERE Id = @payoutId", new { payoutId });
        return msg;
    }

    private static async Task<List<PayoutMessageDto>> MessagesAsync(SqlConnection c, long payoutId, long afterId) =>
        (await c.QueryAsync<PayoutMessageDto>($"""
            SELECT TOP 500 {MessageColumns} FROM promoters.PayoutMessages
            WHERE PayoutId = @payoutId AND Id > @afterId ORDER BY Id
            """, new { payoutId, afterId })).ToList();

    private static string PayoutSelect(bool admin) => $"""
        p.Id, p.Amount, p.Method, p.AccountNumber, p.Status, p.AdminNote, p.DecidedAt, p.CreatedAt, p.LastMessageAt,
        (SELECT COUNT(*) FROM promoters.PayoutMessages m WHERE m.PayoutId = p.Id AND m.FromAdmin = {(admin ? 0 : 1)} AND m.ReadAt IS NULL) AS UnreadMessages,
        CAST(CASE WHEN p.ReceiptPath IS NULL THEN 0 ELSE 1 END AS BIT) AS HasReceipt
        """;

    private const string AdminSelect = """
        p.Id, p.UserId, u.Username, u.Phone, p.Amount, p.Method,
        CASE p.Method WHEN 'ZainCash' THEN N'زين كاش' ELSE N'كي كارد' END AS MethodLabel,
        p.AccountNumber, p.Status,
        CASE p.Status WHEN 'Pending' THEN N'قيد المعالجة' WHEN 'Paid' THEN N'تم التحويل' ELSE N'مرفوض' END AS StatusLabel,
        p.AdminNote, p.DecidedBy, p.DecidedAt, p.CreatedAt, p.LastMessageAt,
        (SELECT COUNT(*) FROM promoters.PayoutMessages m WHERE m.PayoutId = p.Id AND m.FromAdmin = 0 AND m.ReadAt IS NULL) AS UnreadMessages,
        ISNULL((SELECT SUM(w.Amount) FROM promoters.WalletEntries w WHERE w.UserId = p.UserId), 0) AS Balance,
        CAST(CASE WHEN p.ReceiptPath IS NULL THEN 0 ELSE 1 END AS BIT) AS HasReceipt
        """;

    private sealed record PayoutRaw(long Id, long Amount, string Method, string AccountNumber, string Status, string? AdminNote,
        DateTime? DecidedAt, DateTime CreatedAt, DateTime? LastMessageAt, int UnreadMessages, bool HasReceipt);

    private static PayoutDto ToDto(PayoutRaw r) => new(r.Id, r.Amount, r.Method, PayoutMethods.Label(r.Method), r.AccountNumber, r.Status,
        PayoutStatus.Label(r.Status), r.AdminNote, r.DecidedAt, r.CreatedAt, r.LastMessageAt, r.UnreadMessages, r.HasReceipt);
}
