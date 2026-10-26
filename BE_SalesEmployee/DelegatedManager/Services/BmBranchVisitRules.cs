namespace BE_SalesEmployee.DelegatedManager.Services;

public static class BmBranchVisitRules
{
    /// <summary>Long depart note — short stubs are rejected.</summary>
    public const int MinDepartNoteLength = 50;

    public static bool IsValidDelegateScore(int score) => score is >= 0 and <= 10;

    public static bool CanOpen(bool hasOpenVisitForActor) => !hasOpenVisitForActor;

    public static bool CanDepart(bool isOpen, bool openedByActor) => isOpen && openedByActor;

    public static bool IsValidDepartNote(string? note)
    {
        var t = (note ?? "").Trim();
        return t.Length >= MinDepartNoteLength;
    }

    /// <summary>
    /// Every required delegate list must have a saved 0–10 rating before leaving the branch.
    /// Empty required set = no lists in branch → allowed.
    /// </summary>
    public static bool AreAllDelegatesRated(
        IEnumerable<int> requiredDelegateIds,
        IEnumerable<int> ratedDelegateIds)
    {
        var required = requiredDelegateIds
            .Where(id => id > 0)
            .Distinct()
            .ToHashSet();
        if (required.Count == 0)
            return true;

        var rated = ratedDelegateIds
            .Where(id => id > 0)
            .ToHashSet();
        return required.All(rated.Contains);
    }

    public static IReadOnlyList<(Guid UserId, string DisplayName)> SelectMembersForCity(
        IEnumerable<(Guid Id, string DisplayName, bool IsActive, IReadOnlyList<string> Cities)> users,
        string cityValue)
    {
        var city = (cityValue ?? "").Trim();
        if (city.Length == 0) return [];

        return users
            .Where(u => u.IsActive)
            .Where(u => u.Cities.Any(c => string.Equals(c?.Trim(), city, StringComparison.OrdinalIgnoreCase)))
            .Select(u => (u.Id, string.IsNullOrWhiteSpace(u.DisplayName) ? u.Id.ToString("D") : u.DisplayName.Trim()))
            .OrderBy(u => u.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
