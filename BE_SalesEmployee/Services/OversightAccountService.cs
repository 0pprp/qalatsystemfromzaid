using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace BE_SalesEmployee.Services;

public sealed class OversightLoginIdentity
{
    public required string UserName { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public IReadOnlyList<string> CityValues { get; init; } = Array.Empty<string>();
}

public sealed class OversightAccountService
{
    private readonly IOversightAccountStore _store;
    private readonly IConfiguration _configuration;

    public OversightAccountService(IOversightAccountStore store, IConfiguration configuration)
    {
        _store = store;
        _configuration = configuration;
    }

    public Task<IReadOnlyList<OversightCommitteeRecord>> ListCommitteesAsync(CancellationToken ct) =>
        _store.ListCommitteesAsync(ct);

    public Task<IReadOnlyList<OversightAccountRecord>> ListAccountsAsync(CancellationToken ct) =>
        _store.ListAccountsAsync(ct);

    public async Task<(bool Ok, string? Error, OversightCommitteeRecord? Row)> CreateCommitteeAsync(
        string? actorRole, string? name, IReadOnlyList<string>? cityValues, string? createdBy, CancellationToken ct)
    {
        if (!OversightAccessRules.CanManageCommittees(actorRole))
            return (false, "غير مصرح بإنشاء لجنة مراقبة", null);
        if (string.IsNullOrWhiteSpace(name))
            return (false, "اسم اللجنة مطلوب", null);
        var cities = (cityValues ?? Array.Empty<string>())
            .Select(v => (v ?? "").Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cities.Count == 0)
            return (false, "اختر فرعاً واحداً على الأقل", null);
        var row = await _store.CreateCommitteeAsync(name, cities, createdBy, ct);
        return (true, null, row);
    }

    public async Task<(bool Ok, string? Error)> SetBranchesAsync(
        string? actorRole, int committeeId, IReadOnlyList<string>? cityValues, CancellationToken ct)
    {
        if (!OversightAccessRules.CanManageCommittees(actorRole))
            return (false, "غير مصرح بتعديل فروع اللجنة");
        var cities = (cityValues ?? Array.Empty<string>())
            .Select(v => (v ?? "").Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cities.Count == 0)
            return (false, "اختر فرعاً واحداً على الأقل");
        var ok = await _store.SetCommitteeBranchesAsync(committeeId, cities, ct);
        return ok ? (true, null) : (false, "اللجنة غير موجودة");
    }

    public async Task<(bool Ok, string? Error, OversightAccountRecord? Row)> CreateAccountAsync(
        string? actorRole,
        string? userName,
        string? password,
        string? role,
        int? committeeId,
        string? createdBy,
        CancellationToken ct)
    {
        if (!OversightAccessRules.CanCreate(actorRole, role))
            return (false, "غير مصرح بإنشاء هذا النوع من الحسابات", null);
        var nameError = OversightAccessRules.ValidateUserName(userName);
        if (nameError != null)
            return (false, nameError, null);
        var passwordError = OversightAccessRules.ValidatePassword(password);
        if (passwordError != null)
            return (false, passwordError, null);

        var trimmedName = userName!.Trim();
        if (await _store.UserNameExistsAsync(trimmedName, ct) || ConfigUserNameExists(trimmedName))
            return (false, "اسم المستخدم مستخدم مسبقاً", null);

        if (OversightAccessRules.IsCommitteeRole(role))
        {
            if (committeeId is not int id || id <= 0)
                return (false, "اختر لجنة المراقبة أولاً", null);
            var committees = await _store.ListCommitteesAsync(ct);
            var committee = committees.FirstOrDefault(c => c.Id == id);
            if (committee is null)
                return (false, "لجنة المراقبة غير موجودة", null);
            if (committee.CityValues.Count == 0)
                return (false, "اللجنة بلا فروع", null);
        }
        else
        {
            committeeId = null;
        }

        var row = await _store.CreateAccountAsync(
            trimmedName, trimmedName, password!, role!, committeeId, createdBy, ct);
        return (true, null, row);
    }

    public async Task<(bool Ok, string? Error)> DeleteAccountAsync(
        string? actorRole, int accountId, CancellationToken ct)
    {
        var accounts = await _store.ListAccountsAsync(ct);
        var target = accounts.FirstOrDefault(a => a.Id == accountId);
        if (target is null)
            return (false, "الحساب غير موجود");
        if (!OversightAccessRules.CanCreate(actorRole, target.Role))
            return (false, "غير مصرح بحذف هذا الحساب");
        var ok = await _store.DeleteAccountAsync(accountId, ct);
        return ok ? (true, null) : (false, "الحساب غير موجود");
    }

    public async Task<(bool Ok, string? Error)> DeleteCommitteeAsync(
        string? actorRole, int committeeId, CancellationToken ct)
    {
        if (!OversightAccessRules.CanManageCommittees(actorRole))
            return (false, "غير مصرح بحذف لجنة المراقبة");
        var committees = await _store.ListCommitteesAsync(ct);
        if (committees.All(c => c.Id != committeeId))
            return (false, "اللجنة غير موجودة");
        var accounts = await _store.ListAccountsAsync(ct);
        if (accounts.Any(a => a.CommitteeId == committeeId))
            return (false, "احذف حسابات اللجنة أولاً");
        var ok = await _store.DeleteCommitteeAsync(committeeId, ct);
        return ok ? (true, null) : (false, "تعذر حذف اللجنة");
    }

    public async Task<OversightLoginIdentity?> AuthenticateAsync(string? userName, string? password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            return null;
        var row = await _store.FindByUserNameAsync(userName, CancellationToken.None);
        if (row is null || string.IsNullOrEmpty(row.Password))
            return null;
        if (!PasswordEquals(password, row.Password))
            return null;
        return new OversightLoginIdentity
        {
            UserName = row.UserName,
            DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? row.UserName : row.DisplayName,
            Role = row.Role,
            CityValues = row.CityValues,
        };
    }

    public async Task<IReadOnlyList<string>?> CitiesForUserAsync(string? userName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return null;
        var row = await _store.FindByUserNameAsync(userName, ct);
        if (row is null || !OversightAccessRules.IsCommitteeRole(row.Role))
            return null;
        return row.CityValues;
    }

    private bool ConfigUserNameExists(string userName)
    {
        var candidates = new[]
        {
            _configuration["DirectorAccount:UserName"],
            _configuration["MonitorAccount:UserName"],
            _configuration["SalesManagerAccount:UserName"],
            _configuration["DelegatedManagerAccount:UserName"],
        };
        if (candidates.Any(c => NamesEqual(c, userName)))
            return true;
        foreach (var section in new[] { "DirectorAccounts", "MonitorAccounts", "SalesManagerAccounts", "DelegatedManagerAccounts", "MainFollowerAccounts" })
        {
            foreach (var child in _configuration.GetSection(section).GetChildren())
            {
                if (NamesEqual(child["UserName"], userName))
                    return true;
            }
        }
        return false;
    }

    private static bool NamesEqual(string? left, string right) =>
        !string.IsNullOrWhiteSpace(left)
        && string.Equals(
            DirectorAccountService.NormalizeUserName(left),
            DirectorAccountService.NormalizeUserName(right),
            StringComparison.OrdinalIgnoreCase);

    private static bool PasswordEquals(string supplied, string expected)
    {
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
