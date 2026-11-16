using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class ShopCaptureListProgressDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";
    public IReadOnlyList<string> Followers { get; set; } = [];
    public int CustomerCount { get; set; }
    public int WithLocation { get; set; }
    public int WithPhoto { get; set; }
}

public sealed class ShopCaptureOverviewDto
{
    public int CustomerCount { get; set; }
    public int WithLocation { get; set; }
    public int WithPhoto { get; set; }
    public IReadOnlyList<ShopCaptureListProgressDto> Lists { get; set; } = [];
}

public sealed class ShopCaptureCustomerDto
{
    public int CustomerId { get; set; }
    public int ListId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? ShopName { get; set; }
    public string? PhoneNumber { get; set; }

    /// <summary>Shop GPS taken by the follower (متابع) at the shop.</summary>
    public double? FollowerLatitude { get; set; }
    public double? FollowerLongitude { get; set; }
    public double? FollowerAccuracyM { get; set; }
    public DateTime? FollowerLocationAtUtc { get; set; }
    public string? LocationBy { get; set; }

    /// <summary>Shop GPS taken by the sales app.</summary>
    public double? SalesLatitude { get; set; }
    public double? SalesLongitude { get; set; }

    public bool HasPhoto { get; set; }
    public DateTime? PhotoAtUtc { get; set; }
    public string? PhotoBy { get; set; }
}

public interface IFollowerShopCaptureOverviewService
{
    Task<ShopCaptureOverviewDto> GetOverviewAsync(CancellationToken ct);

    Task<IReadOnlyList<ShopCaptureCustomerDto>> GetListCustomersAsync(int listId, CancellationToken ct);

    Task<ShopCaptureCustomerDto?> GetCustomerAsync(int customerId, CancellationToken ct);

    Task<(byte[] Bytes, string ContentType)?> GetPhotoAsync(int customerId, CancellationToken ct);
}

/// <summary>
/// Read-only view of the follower shop GPS + photo (tables written by the follower API in the branch DB)
/// for «عمل المتابعين» and the customer profile. Counts the same customers the follower app counts:
/// customers of follower-assigned lists who still owe money and are not legal.
/// </summary>
public sealed class FollowerShopCaptureOverviewService : IFollowerShopCaptureOverviewService
{
    private readonly SalesDevelopmentGuard _guard;

    public FollowerShopCaptureOverviewService(SalesDevelopmentGuard guard) => _guard = guard;

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var cs = _guard.GetSalesConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("لا يوجد اتصال بقاعدة بيانات الفرع.");
        }

        var c = new SqlConnection(cs);
        await c.OpenAsync(ct);
        return c;
    }

    private static Task<int> ExistsAsync(SqlConnection c, string table, CancellationToken ct) =>
        c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(@T, N'U') IS NOT NULL THEN 1 ELSE 0 END;", new { T = table }, cancellationToken: ct));

    private const string CurrentCustomersCte = @"
cur AS (
    SELECT C.CustomerID, C.DelegateID
    FROM dbo.Customers C
    INNER JOIN dbo.View_Customers VC ON VC.CustomerID = C.CustomerID
    WHERE ISNULL(VC.AmountRemaining, 0) > 0 AND ISNULL(C.IsLegal, 0) = 0
)";

    private const string AllCustomersCte = @"
cur AS (SELECT C.CustomerID, C.DelegateID FROM dbo.Customers C)";

    public async Task<ShopCaptureOverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        if (await ExistsAsync(c, "dbo.FollowerUserLists", ct) != 1)
        {
            return new ShopCaptureOverviewDto();
        }

        var hasCapture = await ExistsAsync(c, "dbo.CustomerShopFollowerCapture", ct) == 1;
        var captureJoin = hasCapture
            ? "LEFT JOIN dbo.CustomerShopFollowerCapture f ON f.CustomerID = cur.CustomerID"
            : "OUTER APPLY (SELECT CAST(NULL AS decimal(9,6)) AS Latitude, CAST(NULL AS datetime2) AS PhotoAtUtc) f";

        string Sql(string cte) => $@"
WITH {cte}
SELECT d.DelegateID AS ListId, LTRIM(RTRIM(ISNULL(d.DelegateName, N''))) AS ListName,
       COUNT(cur.CustomerID) AS CustomerCount,
       ISNULL(SUM(CASE WHEN f.Latitude IS NOT NULL THEN 1 ELSE 0 END), 0) AS WithLocation,
       ISNULL(SUM(CASE WHEN f.PhotoAtUtc IS NOT NULL THEN 1 ELSE 0 END), 0) AS WithPhoto
FROM dbo.Delegates d
LEFT JOIN cur ON cur.DelegateID = d.DelegateID
{captureJoin}
WHERE d.DelegateID IN (SELECT ListId FROM dbo.FollowerUserLists)
GROUP BY d.DelegateID, d.DelegateName
ORDER BY d.DelegateName;";

        List<ShopCaptureListProgressDto> lists;
        try
        {
            lists = (await c.QueryAsync<ShopCaptureListProgressDto>(new CommandDefinition(
                Sql(CurrentCustomersCte), commandTimeout: 120, cancellationToken: ct))).ToList();
        }
        catch (SqlException)
        {
            lists = (await c.QueryAsync<ShopCaptureListProgressDto>(new CommandDefinition(
                Sql(AllCustomersCte), commandTimeout: 120, cancellationToken: ct))).ToList();
        }

        var followers = (await c.QueryAsync<(int ListId, string? Name)>(new CommandDefinition(@"
SELECT ful.ListId, LTRIM(RTRIM(ISNULL(u.UserName, N'')))
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Users u ON u.UserID = ful.UserId
WHERE ISNULL(u.UserState, 1) = 1;", cancellationToken: ct)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.ListId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Name!).Distinct().OrderBy(n => n).ToList());

        foreach (var l in lists)
        {
            l.Followers = followers.GetValueOrDefault(l.ListId, []);
            if (string.IsNullOrWhiteSpace(l.ListName))
            {
                l.ListName = $"قائمة {l.ListId}";
            }
        }

        return new ShopCaptureOverviewDto
        {
            CustomerCount = lists.Sum(l => l.CustomerCount),
            WithLocation = lists.Sum(l => l.WithLocation),
            WithPhoto = lists.Sum(l => l.WithPhoto),
            Lists = lists,
        };
    }

    public async Task<IReadOnlyList<ShopCaptureCustomerDto>> GetListCustomersAsync(int listId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        List<ShopCaptureCustomerDto> rows;
        try
        {
            rows = (await c.QueryAsync<ShopCaptureCustomerDto>(new CommandDefinition(@"
SELECT C.CustomerID AS CustomerId, C.DelegateID AS ListId, ISNULL(VC.CustomerName, C.CustomerName) AS CustomerName,
       C.ShopName, ISNULL(VC.PhoneNumber, C.PhoneNumber) AS PhoneNumber
FROM dbo.Customers C
INNER JOIN dbo.View_Customers VC ON VC.CustomerID = C.CustomerID
WHERE C.DelegateID = @ListId AND ISNULL(VC.AmountRemaining, 0) > 0 AND ISNULL(C.IsLegal, 0) = 0
ORDER BY ISNULL(VC.CustomerName, C.CustomerName);",
                new { ListId = listId }, commandTimeout: 90, cancellationToken: ct))).ToList();
        }
        catch (SqlException)
        {
            rows = (await c.QueryAsync<ShopCaptureCustomerDto>(new CommandDefinition(@"
SELECT C.CustomerID AS CustomerId, C.DelegateID AS ListId, C.CustomerName, C.ShopName, C.PhoneNumber
FROM dbo.Customers C WHERE C.DelegateID = @ListId ORDER BY C.CustomerName;",
                new { ListId = listId }, commandTimeout: 90, cancellationToken: ct))).ToList();
        }

        await FillCaptureAsync(c, rows, ct);
        return rows;
    }

    public async Task<ShopCaptureCustomerDto?> GetCustomerAsync(int customerId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<ShopCaptureCustomerDto>(new CommandDefinition(@"
SELECT CustomerID AS CustomerId, ISNULL(DelegateID, 0) AS ListId, CustomerName, ShopName, PhoneNumber
FROM dbo.Customers WHERE CustomerID = @CustomerId;",
            new { CustomerId = customerId }, cancellationToken: ct));
        if (row == null)
        {
            return null;
        }

        await FillCaptureAsync(c, [row], ct);
        return row;
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetPhotoAsync(int customerId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        if (await ExistsAsync(c, "dbo.CustomerShopFollowerPhotos", ct) != 1)
        {
            return null;
        }

        var row = await c.QuerySingleOrDefaultAsync<(byte[]? Photo, string? ContentType)>(new CommandDefinition(
            "SELECT Photo, ContentType FROM dbo.CustomerShopFollowerPhotos WHERE CustomerID = @CustomerId;",
            new { CustomerId = customerId }, cancellationToken: ct));
        return row.Photo is { Length: > 0 } ? (row.Photo, row.ContentType ?? "image/jpeg") : null;
    }

    private static async Task FillCaptureAsync(SqlConnection c, List<ShopCaptureCustomerDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var byId = rows.ToDictionary(r => r.CustomerId);
        foreach (var chunk in byId.Keys.Chunk(1500))
        {
            if (await ExistsAsync(c, "dbo.CustomerShopFollowerCapture", ct) == 1)
            {
                var caps = await c.QueryAsync<(int CustomerID, double? Lat, double? Lng, double? Acc, DateTime? LocAt, string? LocBy, DateTime? PhotoAt, string? PhotoBy)>(
                    new CommandDefinition(@"
SELECT f.CustomerID, CAST(f.Latitude AS float), CAST(f.Longitude AS float), f.AccuracyM, f.LocationAtUtc,
       ul.UserName, f.PhotoAtUtc, up.UserName
FROM dbo.CustomerShopFollowerCapture f
LEFT JOIN dbo.Users ul ON ul.UserID = f.LocationByUserId
LEFT JOIN dbo.Users up ON up.UserID = f.PhotoByUserId
WHERE f.CustomerID IN @Ids;", new { Ids = chunk }, cancellationToken: ct));
                foreach (var x in caps)
                {
                    var r = byId[x.CustomerID];
                    r.FollowerLatitude = x.Lat;
                    r.FollowerLongitude = x.Lng;
                    r.FollowerAccuracyM = x.Acc;
                    r.FollowerLocationAtUtc = x.LocAt;
                    r.LocationBy = x.Lat == null ? null : x.LocBy?.Trim();
                    r.HasPhoto = x.PhotoAt != null;
                    r.PhotoAtUtc = x.PhotoAt;
                    r.PhotoBy = x.PhotoAt == null ? null : x.PhotoBy?.Trim();
                }
            }

            var hasSales = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NOT NULL
             AND OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL THEN 1 ELSE 0 END;", cancellationToken: ct));
            if (hasSales != 1)
            {
                continue;
            }

            var sales = await c.QueryAsync<(int CustomerID, double Lat, double Lng)>(new CommandDefinition(@"
SELECT x.CustomerID, x.Latitude, x.Longitude
FROM (
    SELECT COALESCE(NULLIF(sp.CustomerId, 0), d.CustomerId) AS CustomerID,
           CAST(sp.Latitude AS float) AS Latitude, CAST(sp.Longitude AS float) AS Longitude,
           ROW_NUMBER() OVER (PARTITION BY COALESCE(NULLIF(sp.CustomerId, 0), d.CustomerId) ORDER BY sp.Id DESC) AS rn
    FROM dbo.SalesShopProfiles sp
    LEFT JOIN dbo.SalesDrafts d ON d.SaleId = sp.SaleId
    WHERE sp.Latitude IS NOT NULL AND sp.Longitude IS NOT NULL
) x
WHERE x.rn = 1 AND x.CustomerID IN @Ids;", new { Ids = chunk }, commandTimeout: 60, cancellationToken: ct));
            foreach (var s in sales)
            {
                if (ShopLocationEstimator.Normalize(s.Lat, s.Lng) is { } p && byId.TryGetValue(s.CustomerID, out var r))
                {
                    r.SalesLatitude = p.Latitude;
                    r.SalesLongitude = p.Longitude;
                }
            }
        }
    }
}
