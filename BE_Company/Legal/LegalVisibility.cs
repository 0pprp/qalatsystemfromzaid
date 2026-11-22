using BE_Company.Sales.Authorization;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Legal;

/// <summary>
/// القانونية visibility: a customer marked legal (Customers.IsLegal) leaves his branch and belongs
/// to القانونية. He is visible only to محاسب القانونية / مدير القانونية / المدير. Those three are
/// central accounts, so they always arrive through the sales gateway, which stamps
/// <see cref="ViewerHeader"/> on their calls. Every other caller is a non-viewer.
/// The Aqeel branches are outside the legal system, so nothing is hidden there.
/// </summary>
public static class LegalVisibility
{
    public const string ViewerHeader = "X-Legal-Viewer";
    public const string ConfigKey = "Legal:HideFromNonLegalUsers";
    public const string ExcludedDatabasesKey = "Legal:ExcludedDatabases";

    /// <summary>الرصافة عقيل، الكرخ عقيل، ميسان، الكوت — matched on the branch database name.</summary>
    private static readonly string[] DefaultExcludedDatabases = ["Aqeel", "Maysan", "Kot"];

    /// <summary>
    /// Hiding is on unless Legal:HideFromNonLegalUsers=false, or this instance serves a branch
    /// that is not part of the legal system (see <see cref="IsExcludedBranch"/>).
    /// </summary>
    public static bool HidingEnabled(IConfiguration configuration) =>
        configuration.GetValue(ConfigKey, true) && !IsExcludedBranch(configuration);

    public static bool IsExcludedBranch(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DataBaseConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;
        string database;
        try
        {
            database = new SqlConnectionStringBuilder(connectionString).InitialCatalog ?? "";
        }
        catch (ArgumentException)
        {
            return false;
        }

        var patterns = configuration.GetSection(ExcludedDatabasesKey).Get<string[]>() ?? DefaultExcludedDatabases;
        return patterns.Any(p => !string.IsNullOrWhiteSpace(p)
            && database.Contains(p.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsLegalViewer(HttpContext context)
    {
        var user = context.User;
        var fromGateway = string.Equals(
            user.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value,
            SalesGatewayKeyHandler.AuthSourceGateway,
            StringComparison.Ordinal);
        if (!fromGateway)
            return false;
        return context.Request.Headers.TryGetValue(ViewerHeader, out var value)
            && string.Equals(value.ToString().Trim(), "1", StringComparison.Ordinal);
    }

    /// <summary>Customer ids (out of <paramref name="ids"/>) that non-viewers must not see.</summary>
    public static async Task<HashSet<int>> HiddenCustomerIdsAsync(SqlConnection connection, IReadOnlyCollection<int> ids)
    {
        var hidden = new HashSet<int>();
        foreach (var chunk in ids.Chunk(1500))
        {
            var rows = await connection.QueryAsync<int>(
                "SELECT CustomerID FROM dbo.Customers WHERE CustomerID IN @Ids AND ISNULL(IsLegal, 0) = 1",
                new { Ids = chunk });
            hidden.UnionWith(rows);
        }

        return hidden;
    }
}
