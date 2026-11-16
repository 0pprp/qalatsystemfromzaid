using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Users;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Gateway;

namespace BE_CustomerApp.Domain.Admin;

public sealed record AgentInput(
    string? Username, string? Password, string? FirstName, string? FatherName, string? GrandfatherName, string? Surname,
    string? Phone, int? CityValue);

public sealed record AgentRow(
    long Id, string FullName, string? Username, string Phone, int? CityValue, string? CityName, bool IsSuspended, string? SuspendReason,
    DateTime CreatedAt, DateTime? LastLoginAt, int OrdersCount, int OpenOrdersCount, int SoldCount, int FinishedCount,
    bool IsWorking, DateTime? LastSeenAt, int? DailyWorkMinutesTarget);

public sealed record AgentDetailDto(
    AgentRow Agent, string FirstName, string FatherName, string GrandfatherName, string? Surname, string? CreatedByAdmin, DateTime? PasswordChangedAt);

/// <summary>Field agents' accounts. Only the dashboard creates them; the app has no sign-up.
/// Agents belong to the company HQ, not to a branch: their CityValue is always NULL (each order picks its own branch).</summary>
public sealed partial class AdminAgentsService
{
    private const string RowSelect = """
        u.Id, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName, ISNULL(' ' + u.Surname, '')) AS FullName, u.Username, u.Phone,
        u.CityValue, CAST(NULL AS NVARCHAR(100)) AS CityName, u.IsSuspended, u.SuspendReason, u.CreatedAt, u.LastLoginAt,
        (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id) AS OrdersCount,
        (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.Status IN ('New','UnderReview','Transferred','FieldInspection')) AS OpenOrdersCount,
        (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.Status IN ('Sold','Finished')) AS SoldCount,
        (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.Status = 'Finished') AS FinishedCount,
        CAST(CASE WHEN EXISTS (SELECT 1 FROM agents.WorkSessions w WHERE w.UserId = u.Id AND w.EndedAt IS NULL) THEN 1 ELSE 0 END AS BIT) AS IsWorking,
        (SELECT MAX(p.RecordedAt) FROM agents.LocationPoints p WHERE p.UserId = u.Id) AS LastSeenAt,
        u.DailyWorkMinutesTarget
        """;

    private readonly Db _db;
    private readonly UserService _users;
    private readonly BranchCatalog _branches;
    private readonly AuditService _audit;

    public AdminAgentsService(Db db, UserService users, BranchCatalog branches, AuditService audit)
    {
        _db = db;
        _users = users;
        _branches = branches;
        _audit = audit;
    }

    public async Task<PagedResult<AgentRow>> SearchAsync(string? q, string? status, int? cityValue, Paging paging, CancellationToken ct)
    {
        var where = new StringBuilder("u.IsDeleted = 0 AND u.Username IS NOT NULL");
        var args = new DynamicParameters(new { paging.Offset, paging.PageSize, cityValue });
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            where.Append(" AND (u.Phone = @phone OR u.Username = @username OR CONCAT(u.FirstName,' ',u.FatherName,' ',u.GrandfatherName,' ',u.Surname) LIKE '%' + @term + '%')");
            args.Add("term", term);
            args.Add("phone", IraqiPhone.Normalize(term) ?? term);
            args.Add("username", term.ToLowerInvariant());
        }
        if (cityValue is not null) where.Append(" AND u.CityValue = @cityValue");
        where.Append(status switch
        {
            "suspended" => " AND u.IsSuspended = 1",
            "active" => " AND u.IsSuspended = 0",
            "working" => " AND EXISTS (SELECT 1 FROM agents.WorkSessions w WHERE w.UserId = u.Id AND w.EndedAt IS NULL)",
            _ => "",
        });
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM agents.Users u WHERE {where};
            SELECT {RowSelect} FROM agents.Users u WHERE {where} ORDER BY u.FirstName, u.Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, args);
        var total = await multi.ReadSingleAsync<int>();
        var rows = new List<AgentRow>();
        foreach (var r in await multi.ReadAsync<AgentRow>()) rows.Add(await WithCityAsync(r, ct));
        return new PagedResult<AgentRow>(rows, total, paging.Page, paging.PageSize);
    }

    public async Task<AgentDetailDto> GetAsync(long id, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<AgentRow>($"SELECT {RowSelect} FROM agents.Users u WHERE u.Id = @id AND u.IsDeleted = 0", new { id })
                  ?? throw ApiException.NotFound("المسوّق غير موجود");
        var extra = await c.QuerySingleAsync<(string FirstName, string FatherName, string GrandfatherName, string? Surname, string? CreatedByAdmin, DateTime? PasswordChangedAt)>(
            "SELECT FirstName, FatherName, GrandfatherName, Surname, CreatedByAdmin, PasswordChangedAt FROM agents.Users WHERE Id = @id", new { id });
        return new AgentDetailDto(await WithCityAsync(row, ct), extra.FirstName, extra.FatherName, extra.GrandfatherName, extra.Surname,
            extra.CreatedByAdmin, extra.PasswordChangedAt);
    }

    public async Task<long> CreateAsync(AgentInput input, AdminActor actor, CancellationToken ct)
    {
        var (name, phone, username, errors) = await ValidateAsync(input, ct);
        if (PasswordHasher.Validate(input.Password) is { } pe) errors["password"] = pe;
        if (errors.Count > 0) throw ApiException.Validation(errors);

        try
        {
            return await _db.InTransactionAsync(async (c, tx) =>
            {
                await EnsureUsernameFreeAsync(username!, null, c, tx);
                var id = await _users.CreateAsync(phone!, UserService.Clean(name), c, tx);
                await c.ExecuteAsync("""
                    UPDATE agents.Users SET Username = @username, PasswordHash = @hash, PasswordChangedAt = SYSUTCDATETIME(), CreatedByAdmin = @Name
                    WHERE Id = @id
                    """, new { id, username, hash = PasswordHasher.Hash(input.Password!), actor.Name }, tx);
                await _audit.WriteAsync(actor, "agent.create", "User", id, null, new { username, phone }, c, tx);
                return id;
            }, ct: ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Users_Username"))
        {
            throw ApiException.Validation(new() { ["username"] = "اسم المستخدم مستخدم مسبقاً" });
        }
    }

    /// <summary>Edits name, phone and username. The password is changed separately.</summary>
    public async Task UpdateAsync(long id, AgentInput input, AdminActor actor, CancellationToken ct)
    {
        var (name, phone, username, errors) = await ValidateAsync(input, ct);
        if (errors.Count > 0) throw ApiException.Validation(errors);
        var n = UserService.Clean(name);
        try
        {
            await _db.InTransactionAsync(async (c, tx) =>
            {
                await EnsureUsernameFreeAsync(username!, id, c, tx);
                var updated = await c.ExecuteAsync("""
                    UPDATE agents.Users SET FirstName = @FirstName, FatherName = @FatherName, GrandfatherName = @GrandfatherName, Surname = @Surname,
                           Phone = @phone, CityValue = NULL, Username = @username, UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = @id AND IsDeleted = 0
                    """, new { n.FirstName, n.FatherName, n.GrandfatherName, n.Surname, phone, username, id }, tx);
                if (updated == 0) throw ApiException.NotFound("المسوّق غير موجود");
                await _audit.WriteAsync(actor, "agent.update", "User", id, null, new { username, phone }, c, tx);
                return 0;
            }, ct: ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627 && ex.Message.Contains("UX_Users_Phone"))
        {
            throw ApiException.Validation(new() { ["phone"] = "رقم الهاتف مسجل لحساب آخر" });
        }
    }

    /// <summary>Sets a new password and signs the agent out of every device.</summary>
    public async Task SetPasswordAsync(long id, string? password, AdminActor actor, CancellationToken ct)
    {
        if (PasswordHasher.Validate(password) is { } pe) throw ApiException.Validation(new() { ["password"] = pe });
        await _db.InTransactionAsync(async (c, tx) =>
        {
            var updated = await c.ExecuteAsync(
                "UPDATE agents.Users SET PasswordHash = @hash, PasswordChangedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME() WHERE Id = @id AND IsDeleted = 0",
                new { id, hash = PasswordHasher.Hash(password!) }, tx);
            if (updated == 0) throw ApiException.NotFound("المسوّق غير موجود");
            await c.ExecuteAsync("UPDATE agents.RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE UserId = @id AND RevokedAt IS NULL", new { id }, tx);
            await _audit.WriteAsync(actor, "agent.password", "User", id, null, null, c, tx);
            return 0;
        }, ct: ct);
    }

    /// <summary>Suspending signs the agent out of every device and blocks sign-in until reactivated.</summary>
    public async Task SetSuspendedAsync(long id, bool suspended, string? reason, AdminActor actor, CancellationToken ct)
    {
        if (suspended && (reason?.Length ?? 0) > 300) throw ApiException.Validation(new() { ["reason"] = "السبب طويل جداً" });
        await _db.InTransactionAsync(async (c, tx) =>
        {
            var updated = await c.ExecuteAsync("""
                UPDATE agents.Users SET IsSuspended = @suspended, SuspendReason = @reason, UpdatedAt = SYSUTCDATETIME()
                WHERE Id = @id AND IsDeleted = 0 AND Username IS NOT NULL
                """, new { id, suspended, reason = suspended && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null }, tx);
            if (updated == 0) throw ApiException.NotFound("المسوّق غير موجود");
            if (suspended)
                await c.ExecuteAsync("UPDATE agents.RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE UserId = @id AND RevokedAt IS NULL", new { id }, tx);
            await _audit.WriteAsync(actor, suspended ? "agent.suspend" : "agent.activate", "User", id, null, new { reason }, c, tx);
            return 0;
        }, ct: ct);
    }

    private async Task<(PersonName Name, string? Phone, string? Username, Dictionary<string, string> Errors)> ValidateAsync(AgentInput input, CancellationToken ct)
    {
        var name = new PersonName(input.FirstName ?? "", input.FatherName ?? "", input.GrandfatherName ?? "", input.Surname);
        var errors = UserService.ValidateName(name);
        var phone = IraqiPhone.Normalize(input.Phone);
        if (phone is null) errors["phone"] = "رقم هاتف عراقي غير صحيح (07XXXXXXXXX)";
        var username = AuthService.NormalizeUsername(input.Username);
        if (username is null || !UsernamePattern().IsMatch(username))
            errors["username"] = "اسم المستخدم: من 2 إلى 30 حرفاً عربياً أو إنكليزياً أو أرقاماً، بدون مسافات";
        await Task.CompletedTask;
        return (name, phone, username, errors);
    }

    private static async Task EnsureUsernameFreeAsync(string username, long? exceptId, SqlConnection c, SqlTransaction tx)
    {
        var taken = await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM agents.Users WHERE Username = @username AND (@exceptId IS NULL OR Id <> @exceptId)", new { username, exceptId }, tx);
        if (taken > 0) throw ApiException.Validation(new() { ["username"] = "اسم المستخدم مستخدم مسبقاً" });
    }

    private async Task<AgentRow> WithCityAsync(AgentRow r, CancellationToken ct) =>
        r.CityValue is { } cv ? r with { CityName = await _branches.NameAsync(cv, ct) } : r;

    /// <summary>Arabic or English letters, digits and . _ - (no spaces), 2–30 characters.</summary>
    [GeneratedRegex(@"^[a-z0-9._\-\p{IsArabic}]{2,30}$")]
    private static partial Regex UsernamePattern();
}
