using System.Globalization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.CustomerApp;

/// <summary>
/// Which sales branches the customer app may reach: قلعة الضمان only. Same rule as نظرة عامة
/// (<see cref="SalesOverviewBranches.IsExcluded"/>: عقيل، الكوت، ميسان) on top of the gateway sales-branch list.
/// A branch also needs a numeric city value (the app addresses branches by int cityValue).
/// </summary>
public static class CustomerAppBranchScope
{
    public static bool IsInScope(AdminCity city) =>
        !string.IsNullOrWhiteSpace(city.Link)
        && !AdminCitiesService.IsExcludedSalesBranch(city.Name)
        && !SalesOverviewBranches.IsExcluded(city.Name, city.Database, city.Link)
        && TryCityValue(city, out _);

    public static List<CustomerAppBranch> Filter(IEnumerable<AdminCity> cities) =>
        cities
            .Where(IsInScope)
            .Select(c =>
            {
                TryCityValue(c, out var value);
                return new CustomerAppBranch { CityValue = value, Name = c.Name, City = c };
            })
            .GroupBy(b => b.CityValue)
            .Select(g => g.First())
            .OrderBy(b => b.CityValue)
            .ToList();

    public static bool TryCityValue(AdminCity city, out int value) =>
        int.TryParse(city.Value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
}

/// <summary>Iraqi mobile → 10-digit key 7XXXXXXXXX (mirror of BE_Company SalesPhoneNormalizer.ForMatch).</summary>
public static class CustomerAppPhone
{
    public static string ToMatchKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("00964", StringComparison.Ordinal) && digits.Length >= 15)
        {
            digits = digits[5..];
        }
        else if (digits.StartsWith("964", StringComparison.Ordinal) && digits.Length >= 13)
        {
            digits = digits[3..];
        }

        if (digits.StartsWith('0') && digits.Length == 11)
        {
            digits = digits[1..];
        }

        return digits.Length == 10 && digits.StartsWith('7') ? digits : "";
    }

    /// <summary>07XXXXXXXXX form sent to branches, or empty when invalid.</summary>
    public static string ToLocal(string? value)
    {
        var key = ToMatchKey(value);
        return key.Length == 10 ? "0" + key : "";
    }
}

public sealed class CustomerAppStatusGroups
{
    public Dictionary<int, (CustomerAppBranch Branch, List<Guid> OrderUuids)> ByBranch { get; } = new();
    /// <summary>Items whose cityValue is not an in-scope branch: they can never be found.</summary>
    public List<CustomerAppStatusQueryItem> OutOfScope { get; } = [];
}

public static class CustomerAppStatusGrouping
{
    public const int MaxItems = 200;

    public static CustomerAppStatusGroups Group(
        IEnumerable<CustomerAppStatusQueryItem> items,
        IReadOnlyList<CustomerAppBranch> branches)
    {
        var groups = new CustomerAppStatusGroups();
        var byValue = branches.ToDictionary(b => b.CityValue);
        var seen = new HashSet<(int, Guid)>();
        foreach (var item in items)
        {
            if (item.OrderUuid == Guid.Empty || !seen.Add((item.CityValue, item.OrderUuid)))
            {
                continue;
            }

            if (!byValue.TryGetValue(item.CityValue, out var branch))
            {
                groups.OutOfScope.Add(item);
                continue;
            }

            if (!groups.ByBranch.TryGetValue(branch.CityValue, out var group))
            {
                group = (branch, []);
                groups.ByBranch[branch.CityValue] = group;
            }

            group.OrderUuids.Add(item.OrderUuid);
        }

        return groups;
    }
}
