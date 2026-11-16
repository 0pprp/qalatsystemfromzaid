using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Users;

/// <summary>A promoter account (agents.Users row without the password).</summary>
public sealed record UserRow(
    long Id, string Username, string Phone, long? ParentUserId, decimal ParentCutPercent, bool IsFull, bool IsSuspended,
    DateTime CreatedAt)
{
    public const string Columns = "Id, Username, Phone, ParentUserId, ParentCutPercent, IsFull, IsSuspended, CreatedAt";

    /// <summary>The share of this account's commission its parent keeps (0 when it is a full account or has no parent).</summary>
    public decimal EffectiveCut => ParentUserId is null || IsFull ? 0 : ParentCutPercent;

    /// <summary>Only full accounts may create sub-accounts.</summary>
    public bool CanCreateAccounts => IsFull;
}

/// <summary>
/// How one sale's commission is shared between the promoter and the account that created them.
/// The promoter's share per unit is rounded to whole dinars and is exactly what the app shows them; the parent gets the rest.
/// Example: 25,000 per unit with a 60% cut → promoter 10,000, parent 15,000.
/// </summary>
public static class CommissionSplit
{
    public static long SellerUnit(long unitCommission, decimal cutPercent)
    {
        if (cutPercent <= 0) return unitCommission;
        if (cutPercent >= 100) return 0;
        var parent = (long)Math.Round(unitCommission * cutPercent / 100m, MidpointRounding.AwayFromZero);
        return unitCommission - parent;
    }

    public static (long Total, long Seller, long Parent) For(long unitCommission, int quantity, decimal cutPercent)
    {
        var total = unitCommission * quantity;
        var seller = SellerUnit(unitCommission, cutPercent) * quantity;
        return (total, seller, total - seller);
    }
}

public static partial class Usernames
{
    /// <summary>
    /// Usernames may be Arabic or English. Case (English), surrounding spaces, Arabic diacritics and tatweel are
    /// ignored, and Arabic-Indic digits become 0-9, so «علي١» and «عَلي1» are the same user.
    /// </summary>
    public static string? Normalize(string? username)
    {
        if (username is null) return null;
        var sb = new StringBuilder(username.Length);
        foreach (var ch in username.Trim())
        {
            int code = ch;
            if (code is 0x0640 or (>= 0x064B and <= 0x065F) or 0x0670) continue;
            if (code is >= 0x0660 and <= 0x0669) sb.Append((char)('0' + code - 0x0660));
            else if (code is >= 0x06F0 and <= 0x06F9) sb.Append((char)('0' + code - 0x06F0));
            else sb.Append(char.ToLowerInvariant(ch));
        }
        var u = sb.ToString();
        return string.IsNullOrEmpty(u) ? null : u;
    }

    public const string Rule = "اسم المستخدم: من 2 إلى 30 حرفاً عربياً أو إنكليزياً أو أرقاماً، بدون مسافات";

    public static bool IsValid(string? normalized) => normalized is not null && Pattern().IsMatch(normalized);

    [GeneratedRegex(@"^[a-z0-9._\-\p{IsArabic}]{2,30}$")]
    private static partial Regex Pattern();
}

/// <summary>The fields shared by «create an account» in the dashboard and in the app.</summary>
public sealed record NewAccount(string Username, string PasswordHash, string Phone);

public static class AccountRules
{
    /// <summary>Validates username, password and phone. Errors are keyed by field name.</summary>
    public static (NewAccount? Account, Dictionary<string, string> Errors) Validate(string? username, string? password, string? phone, bool requirePassword = true)
    {
        var errors = new Dictionary<string, string>();
        var name = Usernames.Normalize(username);
        if (!Usernames.IsValid(name)) errors["username"] = Usernames.Rule;
        if (requirePassword && Auth.PasswordHasher.Validate(password) is { } pe) errors["password"] = pe;
        var p = IraqiPhone.Normalize(phone);
        if (p is null) errors["phone"] = "رقم هاتف عراقي غير صحيح (07XXXXXXXXX)";
        if (errors.Count > 0) return (null, errors);
        return (new NewAccount(name!, requirePassword ? Auth.PasswordHasher.Hash(password!) : "", p!), errors);
    }

    public static string? ValidateCut(decimal? cut) =>
        cut is null ? "أدخل النسبة" : cut is < 0 or > 100 ? "النسبة بين 0 و 100" : decimal.Round(cut.Value, 2) != cut ? "رقمان بعد الفاصلة كحد أقصى" : null;

    /// <summary>Inserts the account; a taken username becomes a field error.</summary>
    public static async Task<long> InsertAsync(SqlConnection c, SqlTransaction tx, NewAccount a, long? parentId, decimal cut, bool isFull, string? createdByAdmin)
    {
        var taken = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM promoters.Users WITH (UPDLOCK, HOLDLOCK) WHERE Username = @Username", new { a.Username }, tx);
        if (taken > 0) throw ApiException.Validation(new() { ["username"] = "اسم المستخدم مستخدم مسبقاً" });
        try
        {
            return await c.ExecuteScalarAsync<long>("""
                INSERT promoters.Users(Username, PasswordHash, Phone, ParentUserId, ParentCutPercent, IsFull, CreatedByAdmin, PasswordChangedAt)
                OUTPUT inserted.Id
                VALUES (@Username, @PasswordHash, @Phone, @parentId, @cut, @isFull, @createdByAdmin, SYSUTCDATETIME())
                """, new { a.Username, a.PasswordHash, a.Phone, parentId, cut, isFull, createdByAdmin }, tx);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Users_Username"))
        {
            throw ApiException.Validation(new() { ["username"] = "اسم المستخدم مستخدم مسبقاً" });
        }
    }
}
