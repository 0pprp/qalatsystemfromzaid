namespace BE_SalesEmployee.MainFollower.Domain;

public static class MissionAccessRules
{
    public static string? AuthorizeMemberCity(MissionRecord? mission, string? memberUserName, string? cityValue)
    {
        if (mission is null)
        {
            return "المهمة غير موجودة.";
        }

        var user = (memberUserName ?? "").Trim();
        if (!mission.MemberUserNames.Any(m => string.Equals(m, user, StringComparison.OrdinalIgnoreCase)))
        {
            return "غير مصرح لك بهذه المهمة.";
        }

        var city = (cityValue ?? "").Trim();
        if (!mission.CityValues.Any(c => string.Equals(c, city, StringComparison.OrdinalIgnoreCase)))
        {
            return "هذه المحافظة ليست ضمن مهمة اللجنة.";
        }

        return null;
    }

    /// <summary>
    /// Server-side province scope for a member across all their missions.
    /// Never trust caller-supplied city alone for cross-mission feeds.
    /// </summary>
    public static HashSet<string> AllowedCitiesForMember(IEnumerable<MissionRecord> missions, string? memberUserName)
    {
        var user = (memberUserName ?? "").Trim();
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mission in missions)
        {
            if (!mission.MemberUserNames.Any(m => string.Equals(m, user, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var city in mission.CityValues)
            {
                var c = (city ?? "").Trim();
                if (c.Length > 0)
                {
                    set.Add(c);
                }
            }
        }

        return set;
    }

    /// <summary>
    /// Optional city filter must still be within the member's assigned provinces.
    /// Returns null when allowed; otherwise an Arabic error message.
    /// </summary>
    public static string? AuthorizeOptionalCityFilter(
        IReadOnlyCollection<string> allowedCities,
        string? cityValue)
    {
        if (allowedCities.Count == 0)
        {
            return "لا توجد محافظات مصرّحة لحسابك.";
        }

        var city = (cityValue ?? "").Trim();
        if (city.Length == 0)
        {
            return null;
        }

        if (!allowedCities.Any(c => string.Equals(c, city, StringComparison.OrdinalIgnoreCase)))
        {
            return "هذه المحافظة ليست ضمن مهامك.";
        }

        return null;
    }

    public static bool CityIsAllowed(IReadOnlyCollection<string> allowedCities, string? cityValue) =>
        allowedCities.Any(c => string.Equals(c, (cityValue ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
}
