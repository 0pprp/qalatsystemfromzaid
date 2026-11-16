using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

/// <summary>Progress of one list: how many current customers still need a follower GPS point / shop photo.</summary>
public sealed class ShopCaptureListDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";
    public int CustomerCount { get; set; }
    public int WithLocation { get; set; }
    public int WithPhoto { get; set; }
}

public sealed class ShopCaptureCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? ShopName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }

    /// <summary>Captured at the shop by the follower (متابع).</summary>
    public double? FollowerLatitude { get; set; }
    public double? FollowerLongitude { get; set; }
    public double? FollowerAccuracyM { get; set; }
    public DateTime? FollowerLocationAtUtc { get; set; }

    /// <summary>Captured by the sales employee when the sale was made.</summary>
    public double? SalesLatitude { get; set; }
    public double? SalesLongitude { get; set; }

    public bool HasPhoto { get; set; }
    public DateTime? PhotoAtUtc { get; set; }
}

public interface IFollowerShopCaptureService
{
    Task<IReadOnlyList<ShopCaptureListDto>> GetListsAsync(int followerUserId, CancellationToken ct);

    Task<IReadOnlyList<ShopCaptureCustomerDto>> GetListCustomersAsync(int listId, CancellationToken ct);

    Task SaveLocationAsync(int customerId, double latitude, double longitude, double? accuracyM, int byUserId, CancellationToken ct);

    Task SavePhotoAsync(int customerId, byte[] bytes, string contentType, int byUserId, CancellationToken ct);

    Task<(byte[] Bytes, string ContentType)?> GetPhotoAsync(int customerId, CancellationToken ct);
}

/// <summary>
/// Shop GPS point + shop photo captured by the follower (متابع), one current value per customer
/// (editing overwrites). Kept apart from the sales-app GPS in <c>SalesShopProfiles</c>; both are returned.
/// Tables live in the branch DB so the director map (BE_Company) can read them too.
/// </summary>
public sealed class FollowerShopCaptureService : IFollowerShopCaptureService
{
    public const int MaxPhotoBytes = 6 * 1024 * 1024;

    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static volatile bool _schemaReady;
    private readonly string _cs;

    public FollowerShopCaptureService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing");
    }

    public static bool IsValidIraqPoint(double lat, double lng) =>
        lat is >= 29 and <= 38 && lng is >= 38 and <= 49;

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var c = new SqlConnection(_cs);
        await c.OpenAsync(ct);
        if (!_schemaReady)
        {
            await SchemaLock.WaitAsync(ct);
            try
            {
                if (!_schemaReady)
                {
                    await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
                    _schemaReady = true;
                }
            }
            finally
            {
                SchemaLock.Release();
            }
        }

        return c;
    }

    private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.CustomerShopFollowerCapture', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerShopFollowerCapture (
        CustomerID INT NOT NULL CONSTRAINT PK_CustomerShopFollowerCapture PRIMARY KEY,
        Latitude DECIMAL(9,6) NULL,
        Longitude DECIMAL(9,6) NULL,
        AccuracyM FLOAT NULL,
        LocationAtUtc DATETIME2(0) NULL,
        LocationByUserId INT NULL,
        PhotoAtUtc DATETIME2(0) NULL,
        PhotoByUserId INT NULL
    );
END;
IF OBJECT_ID(N'dbo.CustomerShopFollowerPhotos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerShopFollowerPhotos (
        CustomerID INT NOT NULL CONSTRAINT PK_CustomerShopFollowerPhotos PRIMARY KEY,
        Photo VARBINARY(MAX) NOT NULL,
        ContentType NVARCHAR(50) NOT NULL,
        UpdatedAtUtc DATETIME2(0) NOT NULL
    );
END;";

    /// <summary>Current customers of a list: still owe money and not legal (same set the director map uses).</summary>
    private static async Task<List<(int CustomerID, string? CustomerName, string? ShopName, string? PhoneNumber, string? Address)>> LoadCurrentCustomersAsync(
        SqlConnection c, int listId, CancellationToken ct)
    {
        try
        {
            return (await c.QueryAsync<(int, string?, string?, string?, string?)>(new CommandDefinition(@"
SELECT C.CustomerID, ISNULL(VC.CustomerName, C.CustomerName), C.ShopName, ISNULL(VC.PhoneNumber, C.PhoneNumber), C.Address
FROM dbo.Customers C
INNER JOIN dbo.View_Customers VC ON VC.CustomerID = C.CustomerID
WHERE C.DelegateID = @ListId
  AND ISNULL(VC.AmountRemaining, 0) > 0
  AND ISNULL(C.IsLegal, 0) = 0
ORDER BY ISNULL(VC.CustomerName, C.CustomerName);",
                new { ListId = listId }, commandTimeout: 90, cancellationToken: ct))).ToList();
        }
        catch (SqlException)
        {
            // Older branch DBs: View_Customers / IsLegal missing — fall back to every customer of the list.
            return (await c.QueryAsync<(int, string?, string?, string?, string?)>(new CommandDefinition(@"
SELECT C.CustomerID, C.CustomerName, C.ShopName, C.PhoneNumber, C.Address
FROM dbo.Customers C
WHERE C.DelegateID = @ListId
ORDER BY C.CustomerName;",
                new { ListId = listId }, commandTimeout: 90, cancellationToken: ct))).ToList();
        }
    }

    public async Task<IReadOnlyList<ShopCaptureListDto>> GetListsAsync(int followerUserId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        var lists = (await c.QueryAsync<(int ListId, string? ListName)>(new CommandDefinition(@"
SELECT ful.ListId, d.DelegateName
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Delegates d ON d.DelegateID = ful.ListId
WHERE ful.UserId = @UserId
ORDER BY d.DelegateName;",
            new { UserId = followerUserId }, cancellationToken: ct))).ToList();

        var result = new List<ShopCaptureListDto>(lists.Count);
        foreach (var (listId, listName) in lists)
        {
            var customers = await LoadCurrentCustomersAsync(c, listId, ct);
            var ids = customers.Select(x => x.CustomerID).ToArray();
            var withLoc = 0;
            var withPhoto = 0;
            if (ids.Length > 0)
            {
                var counts = await c.QuerySingleAsync<(int Loc, int Photo)>(new CommandDefinition(@"
SELECT
    ISNULL(SUM(CASE WHEN Latitude IS NOT NULL THEN 1 ELSE 0 END), 0),
    ISNULL(SUM(CASE WHEN PhotoAtUtc IS NOT NULL THEN 1 ELSE 0 END), 0)
FROM dbo.CustomerShopFollowerCapture
WHERE CustomerID IN @Ids;",
                    new { Ids = ids }, cancellationToken: ct));
                withLoc = counts.Loc;
                withPhoto = counts.Photo;
            }

            result.Add(new ShopCaptureListDto
            {
                ListId = listId,
                ListName = string.IsNullOrWhiteSpace(listName) ? $"قائمة {listId}" : listName.Trim(),
                CustomerCount = ids.Length,
                WithLocation = withLoc,
                WithPhoto = withPhoto,
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<ShopCaptureCustomerDto>> GetListCustomersAsync(int listId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        var customers = await LoadCurrentCustomersAsync(c, listId, ct);
        if (customers.Count == 0)
        {
            return [];
        }

        var ids = customers.Select(x => x.CustomerID).ToArray();
        var captures = (await c.QueryAsync<(int CustomerID, double? Lat, double? Lng, double? Acc, DateTime? LocAt, DateTime? PhotoAt)>(
                new CommandDefinition(@"
SELECT CustomerID, CAST(Latitude AS float), CAST(Longitude AS float), AccuracyM, LocationAtUtc, PhotoAtUtc
FROM dbo.CustomerShopFollowerCapture
WHERE CustomerID IN @Ids;",
                    new { Ids = ids }, cancellationToken: ct)))
            .ToDictionary(x => x.CustomerID);

        var sales = await LoadSalesGpsAsync(c, ids, ct);

        return customers.Select(x =>
        {
            captures.TryGetValue(x.CustomerID, out var cap);
            sales.TryGetValue(x.CustomerID, out var s);
            return new ShopCaptureCustomerDto
            {
                CustomerId = x.CustomerID,
                CustomerName = (x.CustomerName ?? "").Trim(),
                ShopName = x.ShopName?.Trim(),
                PhoneNumber = x.PhoneNumber?.Trim(),
                Address = x.Address?.Trim(),
                FollowerLatitude = cap.Lat,
                FollowerLongitude = cap.Lng,
                FollowerAccuracyM = cap.Acc,
                FollowerLocationAtUtc = cap.LocAt,
                SalesLatitude = s.Lat,
                SalesLongitude = s.Lng,
                HasPhoto = cap.PhotoAt != null,
                PhotoAtUtc = cap.PhotoAt,
            };
        }).ToList();
    }

    /// <summary>Latest sales-app shop GPS per customer (profiles are keyed by sale; a posted sale stores its customer on SalesDrafts).</summary>
    private static async Task<Dictionary<int, (double? Lat, double? Lng)>> LoadSalesGpsAsync(SqlConnection c, int[] ids, CancellationToken ct)
    {
        var hasTables = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NOT NULL
             AND OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL THEN 1 ELSE 0 END;", cancellationToken: ct));
        if (hasTables != 1)
        {
            return [];
        }

        var rows = await c.QueryAsync<(int CustomerID, double Lat, double Lng)>(new CommandDefinition(@"
SELECT x.CustomerID, x.Latitude, x.Longitude
FROM (
    SELECT COALESCE(NULLIF(sp.CustomerId, 0), d.CustomerId) AS CustomerID,
           CAST(sp.Latitude AS float) AS Latitude, CAST(sp.Longitude AS float) AS Longitude,
           ROW_NUMBER() OVER (PARTITION BY COALESCE(NULLIF(sp.CustomerId, 0), d.CustomerId) ORDER BY sp.Id DESC) AS rn
    FROM dbo.SalesShopProfiles sp
    LEFT JOIN dbo.SalesDrafts d ON d.SaleId = sp.SaleId
    WHERE sp.Latitude IS NOT NULL AND sp.Longitude IS NOT NULL
) x
WHERE x.rn = 1 AND x.CustomerID IN @Ids;",
            new { Ids = ids }, commandTimeout: 60, cancellationToken: ct));

        var map = new Dictionary<int, (double? Lat, double? Lng)>();
        foreach (var r in rows)
        {
            if (IsValidIraqPoint(r.Lat, r.Lng))
            {
                map[r.CustomerID] = (r.Lat, r.Lng);
            }
        }

        return map;
    }

    public async Task SaveLocationAsync(int customerId, double latitude, double longitude, double? accuracyM, int byUserId, CancellationToken ct)
    {
        if (!IsValidIraqPoint(latitude, longitude))
        {
            throw new InvalidOperationException("الموقع غير صالح");
        }

        await using var c = await OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.CustomerShopFollowerCapture WITH (HOLDLOCK) AS t
USING (SELECT @CustomerId AS CustomerID) AS s ON t.CustomerID = s.CustomerID
WHEN MATCHED THEN UPDATE SET Latitude = @Lat, Longitude = @Lng, AccuracyM = @Acc,
    LocationAtUtc = SYSUTCDATETIME(), LocationByUserId = @By
WHEN NOT MATCHED THEN INSERT (CustomerID, Latitude, Longitude, AccuracyM, LocationAtUtc, LocationByUserId)
    VALUES (@CustomerId, @Lat, @Lng, @Acc, SYSUTCDATETIME(), @By);",
            new { CustomerId = customerId, Lat = Math.Round(latitude, 6), Lng = Math.Round(longitude, 6), Acc = accuracyM, By = byUserId },
            cancellationToken: ct));
    }

    public async Task SavePhotoAsync(int customerId, byte[] bytes, string contentType, int byUserId, CancellationToken ct)
    {
        if (bytes.Length == 0 || bytes.Length > MaxPhotoBytes)
        {
            throw new InvalidOperationException("حجم الصورة غير مقبول");
        }

        await using var c = await OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.CustomerShopFollowerPhotos WITH (HOLDLOCK) AS t
USING (SELECT @CustomerId AS CustomerID) AS s ON t.CustomerID = s.CustomerID
WHEN MATCHED THEN UPDATE SET Photo = @Photo, ContentType = @Type, UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (CustomerID, Photo, ContentType, UpdatedAtUtc)
    VALUES (@CustomerId, @Photo, @Type, SYSUTCDATETIME());
MERGE dbo.CustomerShopFollowerCapture WITH (HOLDLOCK) AS t
USING (SELECT @CustomerId AS CustomerID) AS s ON t.CustomerID = s.CustomerID
WHEN MATCHED THEN UPDATE SET PhotoAtUtc = SYSUTCDATETIME(), PhotoByUserId = @By
WHEN NOT MATCHED THEN INSERT (CustomerID, PhotoAtUtc, PhotoByUserId) VALUES (@CustomerId, SYSUTCDATETIME(), @By);",
            new { CustomerId = customerId, Photo = bytes, Type = contentType, By = byUserId },
            transaction: tx, commandTimeout: 60, cancellationToken: ct));
        await tx.CommitAsync(ct);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetPhotoAsync(int customerId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<(byte[]? Photo, string? ContentType)>(new CommandDefinition(
            "SELECT Photo, ContentType FROM dbo.CustomerShopFollowerPhotos WHERE CustomerID = @CustomerId;",
            new { CustomerId = customerId }, cancellationToken: ct));
        return row.Photo is { Length: > 0 } ? (row.Photo, row.ContentType ?? "image/jpeg") : null;
    }
}
