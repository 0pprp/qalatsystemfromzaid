namespace BE_SalesEmployee.Services;

public sealed record ArchiveRecipient(string Name, string Role, string? CommitteeName);

/// <summary>
/// Committee accounts (مسؤول / مساعد مسؤول لجنة المراقبة) responsible for a branch — the people a sales
/// agent hands paper archives to, and the archive manager hands follower requests to.
/// </summary>
public sealed class ArchiveRecipientsService
{
    private readonly IOversightAccountStore _store;

    public ArchiveRecipientsService(IOversightAccountStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<ArchiveRecipient>> ForCityAsync(string? cityValue, CancellationToken ct)
    {
        var city = (cityValue ?? string.Empty).Trim();
        if (city.Length == 0)
            return [];
        var accounts = await _store.ListAccountsAsync(ct);
        return Filter(accounts, city);
    }

    public static IReadOnlyList<ArchiveRecipient> Filter(IEnumerable<OversightAccountRecord> accounts, string cityValue) =>
        accounts
            .Where(a => OversightAccessRules.IsCommitteeRole(a.Role))
            .Where(a => a.CityValues.Any(c => string.Equals(c?.Trim(), cityValue.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(a => new ArchiveRecipient(
                string.IsNullOrWhiteSpace(a.DisplayName) ? a.UserName : a.DisplayName.Trim(),
                a.Role,
                a.CommitteeName))
            .Where(r => r.Name.Length >= 2)
            .GroupBy(r => r.Name, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(r => r.Role == OversightAccessRules.RoleCommitteeHead ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.CurrentCulture)
            .ToList();
}
