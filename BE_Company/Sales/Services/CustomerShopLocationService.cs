using System.Collections.Concurrent;
using System.Data;
using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class ListCustomerLocationDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? ShopName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public double AmountRemaining { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public bool IsActive { get; set; }
    public string RatingLabel { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>"follower" = captured at the shop by the follower (متابع); "gps" = captured by the sales app.</summary>
    public string Source { get; set; } = "gps";

    /// <summary>high | medium | low — only for estimated.</summary>
    public string? Confidence { get; set; }

    public int? ClusterPoints { get; set; }
    public int? TotalPoints { get; set; }

    /// <summary>Both confirmed locations (either may be null) so the map can show one source or both.</summary>
    public double? FollowerLatitude { get; set; }
    public double? FollowerLongitude { get; set; }
    public double? SalesLatitude { get; set; }
    public double? SalesLongitude { get; set; }
}

public sealed class ListCustomerLocationsDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";
    public int CustomerCount { get; set; }
    public int WithoutLocationCount { get; set; }
    public DateTime? EstimatedAtUtc { get; set; }

    /// <summary>Collected from this list over the last 30 days (Iraq calendar, today included), in dinars.</summary>
    public double Collected30d { get; set; }

    /// <summary><see cref="Collected30d"/> / 30.</summary>
    public double AvgDailyCollected30d { get; set; }

    /// <summary>The same 30 days one by one (oldest first, days with nothing collected included).</summary>
    public IReadOnlyList<DailyCollectionDto> Daily30d { get; set; } = [];

    /// <summary>Every current customer — rows with Source "none" have no confirmed location.</summary>
    public IReadOnlyList<ListCustomerLocationDto> Customers { get; set; } = [];
}

public sealed class DailyCollectionDto
{
    public DateTime Date { get; set; }
    public double Amount { get; set; }
    public int Payments { get; set; }
}

public sealed class CustomerPaymentLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime? PaymentDate { get; set; }
    public double Amount { get; set; }

    /// <summary>"shop" = in the group the pin was placed on; "office" = spot used for many customers (ignored); "other".</summary>
    public string Kind { get; set; } = "other";
}

public sealed class CustomerPaymentLocationsDto
{
    public int CustomerId { get; set; }

    /// <summary>Recent payments that had a usable GPS point (out of the latest 60 with a location).</summary>
    public IReadOnlyList<CustomerPaymentLocationDto> Points { get; set; } = [];
}

public interface ICustomerShopLocationService
{
    Task<ListCustomerLocationsDto?> GetListLocationsAsync(int listId, CancellationToken ct);

    Task<CustomerPaymentLocationsDto?> GetCustomerPaymentLocationsAsync(int listId, int customerId, CancellationToken ct);
}

/// <summary>
/// متابعة المندوبين map: non-zeroed, non-legal customers of a list with their shop location —
/// sales-app GPS when present, otherwise an estimate from payment GPS points
/// (<see cref="ShopLocationEstimator"/>), recomputed per list at most once a day.
/// </summary>
public sealed class CustomerShopLocationService : ICustomerShopLocationService
{
    public static readonly TimeSpan EstimateMaxAge = TimeSpan.FromHours(24);

    /// <summary>Only the most recent payments per customer — follows a shop that moved.</summary>
    public const int RecentPaymentsPerCustomer = 60;

    private static readonly ConcurrentDictionary<int, SemaphoreSlim> ListLocks = new();
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static volatile bool _schemaReady;

    private readonly SalesDevelopmentGuard _guard;
    private readonly IRatingDataSource _rating;

    public CustomerShopLocationService(SalesDevelopmentGuard guard, IRatingDataSource rating)
    {
        _guard = guard;
        _rating = rating;
    }

    public async Task<ListCustomerLocationsDto?> GetListLocationsAsync(int listId, CancellationToken ct)
    {
        if (listId <= 0)
        {
            return null;
        }

        var cs = _guard.GetSalesConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("لا يوجد اتصال بقاعدة بيانات الفرع.");
        }

        await EnsureSchemaAsync(cs, ct);

        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(ct);

        var listName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT TOP 1 LTRIM(RTRIM(ISNULL(DelegateName, N''))) FROM dbo.Delegates WHERE DelegateID = @ListId;",
            new { ListId = listId }, cancellationToken: ct));
        if (listName is null)
        {
            return null;
        }

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var facts = (await _rating.GetFactsByListIdsAsync([listId], ct))
            .Where(f => f.DelegateId == listId && f.AmountRemaining > 0 && !f.IsLegal && !f.IsFakeSale)
            .Select(f => f with { RatingEquationFactor = 1.0 })
            .ToList();
        var labels = CustomerRatingCalculator
            .EvaluateMany(facts, asOf, usePhantomSaleDate: false)
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => CustomerRatingLabels.FromLevel(g.First().Level));

        var details = (await connection.QueryAsync<(int CustomerID, string? CustomerName, string? ShopName, string? PhoneNumber, string? Address)>(
                new CommandDefinition(@"
SELECT CustomerID, CustomerName, ShopName, PhoneNumber, Address
FROM dbo.Customers WHERE DelegateID = @ListId;",
                    new { ListId = listId }, commandTimeout: 60, cancellationToken: ct)))
            .ToDictionary(r => r.CustomerID);

        var daily = await LoadDaily30dAsync(connection, listId, asOf, ct);
        var collected = daily.Sum(d => d.Amount);

        // Confirmed locations only: follower GPS first (taken at the shop by the متابع), else sales-app GPS.
        // Payment-based estimates are no longer shown.
        var gps = await LoadShopGpsAsync(connection, listId, ct);
        var followerGps = await LoadFollowerGpsAsync(connection, listId, ct);

        var rows = new List<ListCustomerLocationDto>(facts.Count);
        var without = 0;
        foreach (var f in facts)
        {
            details.TryGetValue(f.CustomerId, out var d);
            var row = new ListCustomerLocationDto
            {
                CustomerId = f.CustomerId,
                CustomerName = (d.CustomerName ?? "").Trim(),
                ShopName = d.ShopName?.Trim(),
                PhoneNumber = d.PhoneNumber?.Trim(),
                Address = d.Address?.Trim(),
                AmountRemaining = f.AmountRemaining,
                LastPaymentDate = f.LastPaymentDate,
                IsActive = ActiveCustomerRules.IsActive(f.LastPaymentDate, asOf),
                RatingLabel = labels.GetValueOrDefault(f.CustomerId, ""),
            };

            var hasFollower = followerGps.TryGetValue(f.CustomerId, out var fg);
            var hasSales = gps.TryGetValue(f.CustomerId, out var g);
            if (hasFollower)
            {
                row.FollowerLatitude = fg.Latitude;
                row.FollowerLongitude = fg.Longitude;
            }

            if (hasSales)
            {
                row.SalesLatitude = g.Latitude;
                row.SalesLongitude = g.Longitude;
            }

            // Primary point (older clients): follower first, else sales app.
            if (hasFollower)
            {
                row.Latitude = fg.Latitude;
                row.Longitude = fg.Longitude;
                row.Source = "follower";
            }
            else if (hasSales)
            {
                row.Latitude = g.Latitude;
                row.Longitude = g.Longitude;
                row.Source = "gps";
            }
            else
            {
                row.Source = "none";
                without++;
            }

            rows.Add(row);
        }

        return new ListCustomerLocationsDto
        {
            ListId = listId,
            ListName = string.IsNullOrWhiteSpace(listName) ? $"قائمة {listId}" : listName,
            CustomerCount = facts.Count,
            WithoutLocationCount = without,
            Collected30d = collected,
            AvgDailyCollected30d = Math.Round(collected / 30.0, 0, MidpointRounding.AwayFromZero),
            Daily30d = daily,
            Customers = rows,
        };
    }

    public static string ConfidenceKey(ShopLocationConfidence c) => c switch
    {
        ShopLocationConfidence.High => "high",
        ShopLocationConfidence.Medium => "medium",
        _ => "low",
    };

    /// <summary>Collected from the list per day over the last 30 days (same source as branch-manager list totals).</summary>
    private static async Task<List<DailyCollectionDto>> LoadDaily30dAsync(SqlConnection connection, int listId, DateTime todayIraq, CancellationToken ct)
    {
        var to = todayIraq.Date;
        var from = to.AddDays(-29);
        var args = new { ListId = listId, FromDate = from, ToDate = to };
        IEnumerable<(DateTime Day, double Amount, int Payments)> rows;
        try
        {
            rows = await connection.QueryAsync<(DateTime Day, double Amount, int Payments)>(new CommandDefinition(@"
SELECT CONVERT(date, PaymentDate) AS Day, CAST(ISNULL(SUM(ISNULL(AmountDenar, 0)), 0) AS float) AS Amount, COUNT(*) AS Payments
FROM dbo.View_CustomersPayments
WHERE DelegateID = @ListId AND CONVERT(date, PaymentDate) >= @FromDate AND CONVERT(date, PaymentDate) <= @ToDate
GROUP BY CONVERT(date, PaymentDate);",
                args, commandTimeout: 60, cancellationToken: ct));
        }
        catch (SqlException)
        {
            try
            {
                rows = await connection.QueryAsync<(DateTime Day, double Amount, int Payments)>(new CommandDefinition(@"
SELECT CONVERT(date, r.PaymentDate) AS Day, CAST(ISNULL(SUM(ISNULL(r.AmountDenar, 0)), 0) AS float) AS Amount, COUNT(*) AS Payments
FROM dbo.View_ReceiptCustomerDate r
INNER JOIN dbo.Customers c ON c.CustomerID = r.CustomerID
WHERE c.DelegateID = @ListId AND CONVERT(date, r.PaymentDate) >= @FromDate AND CONVERT(date, r.PaymentDate) <= @ToDate
GROUP BY CONVERT(date, r.PaymentDate);",
                    args, commandTimeout: 60, cancellationToken: ct));
            }
            catch (SqlException)
            {
                rows = [];
            }
        }

        return BuildDaily30d(rows, to);
    }

    /// <summary>One entry per day from <paramref name="today"/>-29 to today, oldest first; missing days are zero.</summary>
    public static List<DailyCollectionDto> BuildDaily30d(IEnumerable<(DateTime Day, double Amount, int Payments)> rows, DateTime today)
    {
        var byDay = rows
            .GroupBy(r => r.Day.Date)
            .ToDictionary(g => g.Key, g => (Amount: g.Sum(x => x.Amount), Payments: g.Sum(x => x.Payments)));
        var from = today.Date.AddDays(-29);
        var list = new List<DailyCollectionDto>(30);
        for (var i = 0; i < 30; i++)
        {
            var day = from.AddDays(i);
            byDay.TryGetValue(day, out var v);
            list.Add(new DailyCollectionDto { Date = day, Amount = v.Amount, Payments = v.Payments });
        }

        return list;
    }

    /// <summary>Shop GPS saved by the follower app (table created by the delegate API on first use).</summary>
    private static async Task<Dictionary<int, GeoPoint>> LoadFollowerGpsAsync(
        SqlConnection connection, int listId, CancellationToken ct)
    {
        var hasTable = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.CustomerShopFollowerCapture', N'U') IS NOT NULL THEN 1 ELSE 0 END;",
            cancellationToken: ct));
        if (hasTable != 1)
        {
            return [];
        }

        var rows = await connection.QueryAsync<(int CustomerID, double Latitude, double Longitude)>(new CommandDefinition(@"
SELECT f.CustomerID, CAST(f.Latitude AS float), CAST(f.Longitude AS float)
FROM dbo.CustomerShopFollowerCapture f
INNER JOIN dbo.Customers c ON c.CustomerID = f.CustomerID
WHERE c.DelegateID = @ListId AND f.Latitude IS NOT NULL AND f.Longitude IS NOT NULL;",
            new { ListId = listId }, commandTimeout: 60, cancellationToken: ct));

        var map = new Dictionary<int, GeoPoint>();
        foreach (var r in rows)
        {
            if (ShopLocationEstimator.Normalize(r.Latitude, r.Longitude) is { } p)
            {
                map[r.CustomerID] = p;
            }
        }

        return map;
    }

    private static async Task<Dictionary<int, GeoPoint>> LoadShopGpsAsync(
        SqlConnection connection, int listId, CancellationToken ct)
    {
        var hasTables = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NOT NULL
             AND OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL THEN 1 ELSE 0 END;",
            cancellationToken: ct));
        if (hasTables != 1)
        {
            return [];
        }

        // Shop profiles are keyed by sale; a posted sale stores the customer it created on SalesDrafts.
        var rows = await connection.QueryAsync<(int CustomerID, double Latitude, double Longitude)>(new CommandDefinition(@"
SELECT x.CustomerID, x.Latitude, x.Longitude
FROM (
    SELECT COALESCE(NULLIF(sp.CustomerId, 0), d.CustomerId) AS CustomerID,
           CAST(sp.Latitude AS float) AS Latitude, CAST(sp.Longitude AS float) AS Longitude,
           ROW_NUMBER() OVER (PARTITION BY COALESCE(NULLIF(sp.CustomerId, 0), d.CustomerId) ORDER BY sp.Id DESC) AS rn
    FROM dbo.SalesShopProfiles sp
    LEFT JOIN dbo.SalesDrafts d ON d.SaleId = sp.SaleId
    WHERE sp.Latitude IS NOT NULL AND sp.Longitude IS NOT NULL
) x
INNER JOIN dbo.Customers c ON c.CustomerID = x.CustomerID
WHERE x.rn = 1 AND c.DelegateID = @ListId;",
            new { ListId = listId }, commandTimeout: 60, cancellationToken: ct));

        var map = new Dictionary<int, GeoPoint>();
        foreach (var r in rows)
        {
            if (ShopLocationEstimator.Normalize(r.Latitude, r.Longitude) is { } p)
            {
                map[r.CustomerID] = p;
            }
        }

        return map;
    }

    /// <summary>Recomputes this list's estimates when older than a day; returns when they were computed.</summary>
    private static async Task<DateTime?> EnsureFreshEstimatesAsync(
        SqlConnection connection, int listId, CancellationToken ct)
    {
        var last = await LastRefreshAsync(connection, listId, ct);
        if (last is { } at && DateTime.UtcNow - at < EstimateMaxAge)
        {
            return at;
        }

        var gate = ListLocks.GetOrAdd(listId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            last = await LastRefreshAsync(connection, listId, ct);
            if (last is { } again && DateTime.UtcNow - again < EstimateMaxAge)
            {
                return again;
            }

            var points = (await LoadListPaymentPointsAsync(connection, listId, ct)).Select(r => r.Point).ToList();
            var estimates = ShopLocationEstimator.Estimate(points);
            var now = DateTime.UtcNow;
            await SaveEstimatesAsync(connection, listId, estimates, now, ct);
            return now;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Latest 60 located payments per customer of the list (the set the estimator uses).</summary>
    private static async Task<List<(CustomerPaymentPoint Point, DateTime? PaymentDate, int PaymentId)>> LoadListPaymentPointsAsync(
        SqlConnection connection, int listId, CancellationToken ct)
    {
        var raw = await connection.QueryAsync<(int CustomerID, string Location, DateTime? PaymentDate, int PaymentId)>(new CommandDefinition(@"
SELECT CustomerID, Location, PaymentDate, CustomerPaymentID
FROM (
    SELECT cp.CustomerID, cp.Location, cp.PaymentDate, cp.CustomerPaymentID,
           ROW_NUMBER() OVER (PARTITION BY cp.CustomerID ORDER BY cp.PaymentDate DESC, cp.CustomerPaymentID DESC) AS rn
    FROM dbo.CustomersPayments cp
    INNER JOIN dbo.Customers c ON c.CustomerID = cp.CustomerID
    WHERE c.DelegateID = @ListId
      AND cp.Location LIKE N'%[0-9].[0-9]%,%[0-9].[0-9]%'
) x
WHERE x.rn <= @Take;",
            new { ListId = listId, Take = RecentPaymentsPerCustomer }, commandTimeout: 180, cancellationToken: ct));

        var points = new List<(CustomerPaymentPoint, DateTime?, int)>();
        foreach (var r in raw)
        {
            if (ShopLocationEstimator.ParseLocation(r.Location) is { } p)
            {
                points.Add((new CustomerPaymentPoint(r.CustomerID, p.Latitude, p.Longitude), r.PaymentDate, r.PaymentId));
            }
        }

        return points;
    }

    public async Task<CustomerPaymentLocationsDto?> GetCustomerPaymentLocationsAsync(int listId, int customerId, CancellationToken ct)
    {
        if (listId <= 0 || customerId <= 0)
        {
            return null;
        }

        var cs = _guard.GetSalesConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("لا يوجد اتصال بقاعدة بيانات الفرع.");
        }

        await EnsureSchemaAsync(cs, ct);
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(ct);

        var belongs = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM dbo.Customers WHERE CustomerID = @CustomerId AND DelegateID = @ListId;",
            new { CustomerId = customerId, ListId = listId }, cancellationToken: ct));
        if (belongs == 0)
        {
            return null;
        }

        var all = await LoadListPaymentPointsAsync(connection, listId, ct);
        var mine = all.Where(r => r.Point.CustomerId == customerId)
            .OrderByDescending(r => r.PaymentDate)
            .ToList();
        var office = ShopLocationEstimator.OfficeFlags(all.Select(r => r.Point).ToList(), mine.Select(r => r.Point).ToList());

        // Same estimate the pin uses, recomputed on this customer's non-office points.
        var kept = mine.Where((_, i) => !office[i]).Select(r => r.Point).ToList();
        var estimate = ShopLocationEstimator.EstimateCustomer(customerId, kept);

        // Amounts live on the box entries of each payment (same as the sales overview).
        var amounts = mine.Count == 0
            ? new Dictionary<int, double>()
            : (await connection.QueryAsync<(int PaymentId, double Amount)>(new CommandDefinition(@"
SELECT ab.CustomerPaymentID, CAST(SUM(ISNULL(ab.Amount, 0)) * 1448.0 AS float)
FROM dbo.AddToBox ab
WHERE ab.CustomerPaymentID IN @Ids
GROUP BY ab.CustomerPaymentID;",
                    new { Ids = mine.Select(r => r.PaymentId).ToArray() }, commandTimeout: 60, cancellationToken: ct)))
                .ToDictionary(r => r.PaymentId, r => r.Amount);

        return new CustomerPaymentLocationsDto
        {
            CustomerId = customerId,
            Points = mine.Select((r, i) => new CustomerPaymentLocationDto
            {
                Latitude = r.Point.Latitude,
                Longitude = r.Point.Longitude,
                PaymentDate = r.PaymentDate,
                Amount = amounts.GetValueOrDefault(r.PaymentId),
                Kind = office[i] ? "office"
                    : estimate is not null && ShopLocationEstimator.DistanceMeters(
                        estimate.Latitude, estimate.Longitude, r.Point.Latitude, r.Point.Longitude) <= ShopLocationEstimator.ClusterRadiusMeters
                        ? "shop" : "other",
            }).ToList(),
        };
    }

    private static Task<DateTime?> LastRefreshAsync(SqlConnection connection, int listId, CancellationToken ct) =>
        connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(
            "SELECT RefreshedAtUtc FROM dbo.CustomerLocationEstimateRefresh WHERE ListId = @ListId;",
            new { ListId = listId }, cancellationToken: ct));

    private static async Task SaveEstimatesAsync(
        SqlConnection connection,
        int listId,
        IReadOnlyList<ShopLocationEstimate> estimates,
        DateTime nowUtc,
        CancellationToken ct)
    {
        var table = new DataTable();
        table.Columns.Add("ListId", typeof(int));
        table.Columns.Add("CustomerID", typeof(int));
        table.Columns.Add("Latitude", typeof(decimal));
        table.Columns.Add("Longitude", typeof(decimal));
        table.Columns.Add("Confidence", typeof(byte));
        table.Columns.Add("ClusterPoints", typeof(int));
        table.Columns.Add("TotalPoints", typeof(int));
        table.Columns.Add("UpdatedAtUtc", typeof(DateTime));
        foreach (var e in estimates)
        {
            table.Rows.Add(
                listId,
                e.CustomerId,
                Math.Round((decimal)e.Latitude, 6),
                Math.Round((decimal)e.Longitude, 6),
                (byte)e.Confidence,
                e.ClusterPoints,
                e.TotalPoints,
                nowUtc);
        }

        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.CustomerLocationEstimates WHERE ListId = @ListId;",
            new { ListId = listId }, tx, commandTimeout: 60, cancellationToken: ct));

        if (table.Rows.Count > 0)
        {
            using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, tx)
            {
                DestinationTableName = "dbo.CustomerLocationEstimates",
                BulkCopyTimeout = 120,
            };
            foreach (DataColumn col in table.Columns)
            {
                bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            await bulk.WriteToServerAsync(table, ct);
        }

        await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.CustomerLocationEstimateRefresh SET RefreshedAtUtc = @Now WHERE ListId = @ListId;
IF @@ROWCOUNT = 0
    INSERT INTO dbo.CustomerLocationEstimateRefresh (ListId, RefreshedAtUtc) VALUES (@ListId, @Now);",
            new { ListId = listId, Now = nowUtc }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
    }

    private static async Task EnsureSchemaAsync(string cs, CancellationToken ct)
    {
        if (_schemaReady)
        {
            return;
        }

        await SchemaLock.WaitAsync(ct);
        try
        {
            if (_schemaReady)
            {
                return;
            }

            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    public const string SchemaSql = @"
IF OBJECT_ID(N'dbo.CustomerLocationEstimates', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerLocationEstimates (
        ListId INT NOT NULL,
        CustomerID INT NOT NULL,
        Latitude DECIMAL(9,6) NOT NULL,
        Longitude DECIMAL(9,6) NOT NULL,
        Confidence TINYINT NOT NULL,
        ClusterPoints INT NOT NULL,
        TotalPoints INT NOT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL,
        CONSTRAINT PK_CustomerLocationEstimates PRIMARY KEY (ListId, CustomerID)
    );
END;
IF OBJECT_ID(N'dbo.CustomerLocationEstimateRefresh', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerLocationEstimateRefresh (
        ListId INT NOT NULL PRIMARY KEY,
        RefreshedAtUtc DATETIME2 NOT NULL
    );
END;";
}
