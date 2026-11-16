using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

/// <summary>
/// Copies Customers.IsLegal onto rows that carry الواصل/الباقي but do not select the flag themselves.
/// </summary>
public static class LegalCustomerFlagStamp
{
    public static async Task ApplyAsync<T>(
        SqlConnection connection,
        IEnumerable<T>? rows,
        Func<T, int?> customerId,
        Action<T, bool> setLegal)
    {
        if (rows == null)
            return;

        var list = rows as IList<T> ?? rows.ToList();
        var ids = list.Select(customerId).Where(id => id is > 0).Select(id => id!.Value).Distinct().ToArray();
        if (ids.Length == 0)
            return;

        var flags = await connection.QueryAsync<LegalFlag>(
            "SELECT CustomerID, CAST(ISNULL(IsLegal, 0) AS bit) AS IsLegal FROM dbo.Customers WHERE CustomerID IN @Ids",
            new { Ids = ids });
        var map = flags.ToDictionary(f => f.CustomerID, f => f.IsLegal);
        foreach (var row in list)
        {
            var id = customerId(row);
            if (id is int key && map.TryGetValue(key, out var legal))
                setLegal(row, legal);
        }
    }

    private sealed class LegalFlag
    {
        public int CustomerID { get; set; }
        public bool IsLegal { get; set; }
    }
}
