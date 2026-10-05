namespace BE_SalesEmployee.MainFollower.Domain;

public static class CommitteeRules
{
    public const int MinMembers = 2;

    public static string? ValidateMembers(IEnumerable<string?>? memberUserNames)
    {
        var names = NormalizeMembers(memberUserNames);
        if (names.Count < MinMembers)
        {
            return $"اللجنة تحتاج على الأقل {MinMembers} من المتابعين الرئيسيين.";
        }

        return null;
    }

    public static string? ValidateCities(IEnumerable<string?>? cityValues)
    {
        var cities = NormalizeCities(cityValues);
        if (cities.Count == 0)
        {
            return "يجب اختيار محافظة واحدة على الأقل للمهمة.";
        }

        return null;
    }

    public static IReadOnlyList<string> NormalizeMembers(IEnumerable<string?>? memberUserNames)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (memberUserNames == null)
        {
            return Array.Empty<string>();
        }

        foreach (var raw in memberUserNames)
        {
            var name = (raw ?? "").Trim();
            if (name.Length == 0)
            {
                continue;
            }

            set.Add(name);
        }

        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> NormalizeCities(IEnumerable<string?>? cityValues)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (cityValues == null)
        {
            return Array.Empty<string>();
        }

        foreach (var raw in cityValues)
        {
            var city = (raw ?? "").Trim();
            if (city.Length == 0)
            {
                continue;
            }

            set.Add(city);
        }

        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
