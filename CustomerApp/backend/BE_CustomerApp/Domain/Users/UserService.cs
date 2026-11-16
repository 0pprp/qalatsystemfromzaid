using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Domain.Users;

public sealed record UserRow(
    long Id, string Phone, string FirstName, string FatherName, string GrandfatherName, string? Surname,
    int? CityValue, string ReferralCode, long? ParentUserId, int Depth, int DirectCount, int NetworkSize,
    bool MarketingOptIn, bool IsSuspended, bool EarningsFrozen, bool IsDeleted, DateTime CreatedAt)
{
    public string FullName => string.Join(' ', new[] { FirstName, FatherName, GrandfatherName, Surname }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

public sealed record PersonName(string FirstName, string FatherName, string GrandfatherName, string? Surname)
{
    private static readonly HashSet<string> Prefixes = ["عبد", "ابو", "أبو", "ابي", "أبي"];

    /// <summary>
    /// Splits a full name typed in one field («محمد جاسم عبد الله الكعبي») into first, father and grandfather names and
    /// a surname (everything after the third name). «عبد»/«أبو» are joined to the next word so «عبد الله» stays one name.
    /// Returns null when fewer than three names were typed.
    /// </summary>
    public static PersonName? FromFullName(string? fullName)
    {
        var words = (fullName ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var names = new List<string>();
        for (var i = 0; i < words.Length; i++)
        {
            if (Prefixes.Contains(words[i]) && i + 1 < words.Length) names.Add(words[i] + words[++i]);
            else names.Add(words[i]);
        }
        if (names.Count < 3) return null;
        return new PersonName(names[0], names[1], names[2], names.Count > 3 ? string.Join(' ', names.Skip(3)) : null);
    }
}

/// <summary>The signed-in agent.</summary>
public sealed record MeDto(
    long Id, string? Username, string Phone, string FirstName, string FatherName, string GrandfatherName, string? Surname, string FullName,
    DateTime CreatedAt);

public sealed partial class UserService
{
    public const string Columns = """
        Id, Phone, FirstName, FatherName, GrandfatherName, Surname, CityValue, ReferralCode, ParentUserId, Depth,
        DirectCount, NetworkSize, MarketingOptIn, IsSuspended, EarningsFrozen, IsDeleted, CreatedAt
        """;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly Db _db;

    public UserService(Db db) => _db = db;

    public async Task<UserRow?> FindByPhoneAsync(string phone, SqlConnection connection, SqlTransaction? tx = null) =>
        await connection.QuerySingleOrDefaultAsync<UserRow>($"SELECT {Columns} FROM agents.Users WHERE Phone = @phone", new { phone }, tx);

    public async Task<UserRow> GetAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _db.OpenAsync(ct);
        return await GetAsync(id, connection);
    }

    public static async Task<UserRow> GetAsync(long id, SqlConnection connection, SqlTransaction? tx = null) =>
        await connection.QuerySingleOrDefaultAsync<UserRow>($"SELECT {Columns} FROM agents.Users WHERE Id = @id AND IsDeleted = 0", new { id }, tx)
        ?? throw new ApiException(401, "unauthorized", "انتهت الجلسة، يرجى تسجيل الدخول مجدداً");

    /// <summary>Customer endpoints call this first: deleted/suspended accounts are rejected.</summary>
    public async Task<UserRow> GetActiveAsync(long id, CancellationToken ct = default)
    {
        var user = await GetAsync(id, ct);
        if (user.IsSuspended) throw ApiException.Forbidden("account_suspended", "تم إيقاف هذا الحساب، يرجى التواصل مع الشركة");
        return user;
    }

    public static Dictionary<string, string> ValidateName(PersonName? name, string prefix = "")
    {
        var errors = new Dictionary<string, string>();
        void Check(string field, string? value, bool required)
        {
            // "عبد الله" is stored as one token ("عبدالله") so the 4-part name keeps its positions for matching.
            var v = (value ?? "").Replace(" ", "");
            if (v.Length == 0) { if (required) errors[prefix + field] = "مطلوب"; return; }
            if (!SingleArabicWord().IsMatch(v)) errors[prefix + field] = "يرجى كتابة اسم واحد بالحروف العربية";
        }
        Check("firstName", name?.FirstName, true);
        Check("fatherName", name?.FatherName, true);
        Check("grandfatherName", name?.GrandfatherName, true);
        // Surname may be two words (e.g. "آل محمد").
        var surname = name?.Surname is null ? null : CollapseSpaces().Replace(name.Surname.Trim(), " ");
        if (!string.IsNullOrEmpty(surname) && !ArabicWords().IsMatch(surname)) errors[prefix + "surname"] = "يرجى كتابة اللقب بالحروف العربية";
        return errors;
    }

    public static PersonName Clean(PersonName n) => new(
        n.FirstName.Replace(" ", ""), n.FatherName.Replace(" ", ""), n.GrandfatherName.Replace(" ", ""),
        string.IsNullOrWhiteSpace(n.Surname) ? null : CollapseSpaces().Replace(n.Surname.Trim(), " "));

    /// <summary>Inserts an agent (the dashboard sets the username and password right after). Agents belong to the HQ: no branch.</summary>
    public async Task<long> CreateAsync(string phone, PersonName name, SqlConnection connection, SqlTransaction tx)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                // ReferralCode is a legacy unique column of the Users table; it is filled with a random code and never shown.
                return await connection.ExecuteScalarAsync<long>("""
                    INSERT agents.Users(Phone, FirstName, FatherName, GrandfatherName, Surname, ReferralCode, TermsAcceptedAt)
                    OUTPUT inserted.Id
                    VALUES (@phone, @FirstName, @FatherName, @GrandfatherName, @Surname, @code, SYSUTCDATETIME())
                    """, new { phone, name.FirstName, name.FatherName, name.GrandfatherName, name.Surname, code = NewCode() }, tx);
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Users_ReferralCode") && attempt < 5)
            {
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Users_Phone"))
            {
                throw ApiException.Validation(new() { ["phone"] = "رقم الهاتف مسجل لحساب آخر" });
            }
        }
    }

    public async Task<MeDto> GetMeAsync(long userId, CancellationToken ct = default)
    {
        await using var connection = await _db.OpenAsync(ct);
        var user = await GetAsync(userId, connection);
        var username = await connection.ExecuteScalarAsync<string?>("SELECT Username FROM agents.Users WHERE Id = @userId", new { userId });
        return new MeDto(user.Id, username, user.Phone, user.FirstName, user.FatherName, user.GrandfatherName, user.Surname, user.FullName, user.CreatedAt);
    }

    private static string NewCode()
    {
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < chars.Length; i++) chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    [GeneratedRegex(@"^[ء-يٱ-ۓ]{2,30}$")]
    private static partial Regex SingleArabicWord();

    [GeneratedRegex(@"^[ء-يٱ-ۓ]+( [ء-يٱ-ۓ]+){0,2}$")]
    private static partial Regex ArabicWords();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseSpaces();
}
