namespace BE_SalesEmployee.DelegatedManager;

/// <summary>Company paths for director ملخص (قلعة الضمان financial matrix).</summary>
public static class DirectorSummaryPaths
{
    /// <summary>City IDs for قلعة الضمان sales branches (excludes منهج: 7 كوت, 15/18 عقيل, 16 ميسان).</summary>
    public static readonly HashSet<string> QalaaCityValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "1", "2", "3", "4", "5", "6", "8", "9", "10", "11", "12", "13", "14"
    };

    public static bool IsQalaaCity(string? cityValue)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return false;
        }

        return QalaaCityValues.Contains(cityValue.Trim());
    }

    public static string CompanyMatrix(int year, int month, string metric, string? branchName)
    {
        var path = "sales-manager/director-summary/matrix"
            + $"?year={year}"
            + $"&month={month}"
            + $"&metric={Uri.EscapeDataString(metric)}";
        if (!string.IsNullOrWhiteSpace(branchName))
        {
            path += $"&branchName={Uri.EscapeDataString(branchName.Trim())}";
        }

        return path;
    }

    public static string CompanyDrilldown(int year, int month, int day, string metric, string? branchName)
    {
        var path = "sales-manager/director-summary/drilldown"
            + $"?year={year}"
            + $"&month={month}"
            + $"&day={day}"
            + $"&metric={Uri.EscapeDataString(metric)}";
        if (!string.IsNullOrWhiteSpace(branchName))
        {
            path += $"&branchName={Uri.EscapeDataString(branchName.Trim())}";
        }

        return path;
    }
}
