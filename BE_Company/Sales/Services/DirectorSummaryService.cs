using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class DirectorSummaryDayTotalDto
{
    public int Day { get; set; }
    public decimal Total { get; set; }
}

public sealed class DirectorSummaryMatrixDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Metric { get; set; } = "receipts";
    public int DaysInMonth { get; set; }
    public string? BoxName { get; set; }
    /// <summary>المبلغ الكلي = خزينة + ابو سجاد + قاصات نشطة.</summary>
    public decimal BoxTotalAmountDenar { get; set; }
    /// <summary>المبلغ الفعلي = رصيد خزينة/قاصة المحافظة فقط (بدون تجميع).</summary>
    public decimal TreasuryAmountDenar { get; set; }
    public string? TreasuryBoxName { get; set; }
    public List<DirectorSummaryDayTotalDto> Days { get; set; } = [];
}

public sealed class DirectorSummaryDrilldownDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public string Metric { get; set; } = "receipts";
    public decimal GrandTotal { get; set; }
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
}

public interface IDirectorSummaryService
{
    Task<DirectorSummaryMatrixDto> GetMatrixAsync(int year, int month, string metric, string? branchName, CancellationToken ct);
    Task<DirectorSummaryDrilldownDto> GetDrilldownAsync(int year, int month, int day, string metric, string? branchName, CancellationToken ct);
}

/// <summary>
/// Read-only day×metric aggregates for director ملخص (per-branch SQL).
/// Metrics: receipts | expenses | sales | buys.
/// </summary>
public sealed class DirectorSummaryService : IDirectorSummaryService
{
    private static readonly HashSet<string> AllowedMetrics = new(StringComparer.OrdinalIgnoreCase)
    {
        "receipts", "expenses", "salePrice", "purchasePrice", "sales", "buys"
    };

    private readonly SalesDevelopmentGuard _guard;

    public DirectorSummaryService(SalesDevelopmentGuard guard)
    {
        _guard = guard;
    }

    public async Task<DirectorSummaryMatrixDto> GetMatrixAsync(
        int year, int month, string metric, string? branchName, CancellationToken ct)
    {
        NormalizePeriod(ref year, ref month, out var daysInMonth, out var from, out var toExclusive);
        metric = NormalizeMetric(metric);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(ct);

        var (boxId, treasuryBoxName, treasuryAmount) = await LoadPrimaryBoxAsync(connection, branchName, ct);
        var (boxName, boxTotal) = await LoadDirectorBoxTotalAsync(connection, branchName, ct);

        List<DirectorSummaryDayTotalDto> dayTotals;
        if (metric == "expenses")
        {
            dayTotals = await QueryExpenseDayTotalsAsync(connection, from, toExclusive, boxId, ct);
        }
        else if (metric is "salePrice" or "sales")
        {
            dayTotals = await QueryDayTotalsAsync(connection, @"
SELECT DAY(v.DateCreate) AS [Day], CAST(SUM(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0)) AS decimal(18,3)) AS Total
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @ToExclusive
GROUP BY DAY(v.DateCreate);", from, toExclusive, ct);
        }
        else if (metric == "purchasePrice")
        {
            dayTotals = await QueryDayTotalsAsync(connection, @"
SELECT DAY(v.DateCreate) AS [Day], CAST(SUM(ISNULL(v.AmountTotalCostDenar, 0)) AS decimal(18,3)) AS Total
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @ToExclusive
GROUP BY DAY(v.DateCreate);", from, toExclusive, ct);
        }
        else if (metric == "buys")
        {
            dayTotals = await QueryDayTotalsAsync(connection, @"
SELECT DAY(v.DateCreate) AS [Day], CAST(SUM(ISNULL(v.TotalAmountDenar, 0)) AS decimal(18,3)) AS Total
FROM dbo.View_Buys v
WHERE v.DateCreate >= @From AND v.DateCreate < @ToExclusive
  AND ISNULL(v.BuyState, 1) = 1
GROUP BY DAY(v.DateCreate);", from, toExclusive, ct);
        }
        else
        {
            // Legal customers: exclude الواصل from director ملخص (accountant/director must not see it).
            dayTotals = await QueryDayTotalsAsync(connection, @"
SELECT DAY(v.PaymentDate) AS [Day], CAST(SUM(ISNULL(v.AmountDenar, 0)) AS decimal(18,3)) AS Total
FROM dbo.View_CustomersPayments v
INNER JOIN dbo.Customers c ON c.CustomerID = v.CustomerID
WHERE v.PaymentDate >= @From AND v.PaymentDate < @ToExclusive
  AND ISNULL(c.IsLegal, 0) = 0
GROUP BY DAY(v.PaymentDate);", from, toExclusive, ct);
        }

        var byDay = dayTotals.ToDictionary(x => x.Day, x => x.Total);
        var days = new List<DirectorSummaryDayTotalDto>(daysInMonth);
        for (var d = 1; d <= daysInMonth; d++)
        {
            days.Add(new DirectorSummaryDayTotalDto
            {
                Day = d,
                Total = byDay.TryGetValue(d, out var t) ? t : 0m
            });
        }

        return new DirectorSummaryMatrixDto
        {
            Year = year,
            Month = month,
            Metric = metric,
            DaysInMonth = daysInMonth,
            BoxName = boxName,
            BoxTotalAmountDenar = boxTotal,
            TreasuryAmountDenar = treasuryAmount,
            TreasuryBoxName = treasuryBoxName,
            Days = days
        };
    }

    public async Task<DirectorSummaryDrilldownDto> GetDrilldownAsync(
        int year, int month, int day, string metric, string? branchName, CancellationToken ct)
    {
        NormalizePeriod(ref year, ref month, out var daysInMonth, out _, out _);
        if (day < 1 || day > daysInMonth)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        metric = NormalizeMetric(metric);
        var dayStart = new DateTime(year, month, day);
        var dayEnd = dayStart.AddDays(1);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(ct);

        var (boxId, _, _) = await LoadPrimaryBoxAsync(connection, branchName, ct);
        var rows = metric switch
        {
            "expenses" => await QueryExpenseRowsAsync(connection, dayStart, dayEnd, boxId, ct),
            "salePrice" or "sales" => await QuerySaleRowsAsync(connection, dayStart, dayEnd, saleAmount: true, ct),
            "purchasePrice" => await QuerySaleRowsAsync(connection, dayStart, dayEnd, saleAmount: false, ct),
            "buys" => await QueryBuyRowsAsync(connection, dayStart, dayEnd, ct),
            _ => await QueryReceiptRowsAsync(connection, dayStart, dayEnd, ct),
        };

        var grand = rows.Sum(r => Convert.ToDecimal(r.GetValueOrDefault("amount") ?? 0m));
        return new DirectorSummaryDrilldownDto
        {
            Year = year,
            Month = month,
            Day = day,
            Metric = metric,
            GrandTotal = grand,
            Rows = rows
        };
    }

    private string RequireCs() =>
        _guard.GetSalesConnectionString()
        ?? throw new InvalidOperationException("Sales connection string missing.");

    private static string NormalizeMetric(string? metric)
    {
        var m = (metric ?? "receipts").Trim();
        // camelCase keys from FE
        if (string.Equals(m, "salePrice", StringComparison.OrdinalIgnoreCase))
        {
            return "salePrice";
        }

        if (string.Equals(m, "purchasePrice", StringComparison.OrdinalIgnoreCase))
        {
            return "purchasePrice";
        }

        m = m.ToLowerInvariant();
        if (m == "sales")
        {
            return "salePrice";
        }

        return AllowedMetrics.Contains(m) ? m : "receipts";
    }

    private static void NormalizePeriod(ref int year, ref int month, out int daysInMonth, out DateTime from, out DateTime toExclusive)
    {
        var baghdad = DateTime.UtcNow.AddHours(3);
        if (year < 2000 || year > 2100)
        {
            year = baghdad.Year;
        }

        if (month < 1 || month > 12)
        {
            month = baghdad.Month;
        }

        daysInMonth = DateTime.DaysInMonth(year, month);
        from = new DateTime(year, month, 1);
        toExclusive = from.AddMonths(1);
    }

    private static async Task<List<DirectorSummaryDayTotalDto>> QueryDayTotalsAsync(
        SqlConnection connection,
        string sql,
        DateTime from,
        DateTime toExclusive,
        CancellationToken ct,
        object? extraParams = null)
    {
        object param = extraParams == null
            ? new { From = from, ToExclusive = toExclusive }
            : MergeAnon(new { From = from, ToExclusive = toExclusive }, extraParams);
        var rows = await connection.QueryAsync<DirectorSummaryDayTotalDto>(new CommandDefinition(
            sql,
            param,
            commandTimeout: 120,
            cancellationToken: ct));
        return rows.ToList();
    }

    private static object MergeAnon(object a, object b)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in a.GetType().GetProperties())
        {
            dict[p.Name] = p.GetValue(a);
        }

        foreach (var p in b.GetType().GetProperties())
        {
            dict[p.Name] = p.GetValue(b);
        }

        return dict;
    }

    /// <summary>
    /// المبلغ الكلي = خزينة المحافظة + قاصة ابو سجاد + قاصات القوائم النشطة
    /// (قائمة نشطة = أي زبون فيها سدّد خلال آخر 30 يوماً). رصيد القاصة = View_Box.AmountDenar.
    /// «دين ابو سجاد» لا يدخل بالمعادلة.
    /// </summary>
    private static async Task<(string? Label, decimal Total)> LoadDirectorBoxTotalAsync(
        SqlConnection connection, string? branchName, CancellationToken ct)
    {
        var (treasuryId, _, treasuryAmount) = await LoadPrimaryBoxAsync(connection, branchName, ct);
        var (abuId, abuAmount) = await LoadAbuSajjadCashBoxAsync(connection, ct);

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var cutoff = asOf.AddDays(-30);

        var exclude = new HashSet<int>();
        if (treasuryId > 0)
        {
            exclude.Add(treasuryId);
        }

        if (abuId > 0)
        {
            exclude.Add(abuId);
        }

        decimal activeListsAmount;
        if (exclude.Count == 0)
        {
            activeListsAmount = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(@"
SELECT CAST(SUM(ISNULL(vb.AmountDenar, 0)) AS decimal(18,3))
FROM dbo.Delegates d
INNER JOIN dbo.View_Box vb ON vb.BoxID = d.BoxID
WHERE ISNULL(d.DelegateState, 0) = 1
  AND d.BoxID IS NOT NULL
  AND EXISTS (
      SELECT 1
      FROM dbo.Customers c
      INNER JOIN dbo.View_CustomersPaymentsDelegate p ON p.CustomerID = c.CustomerID
      WHERE c.DelegateID = d.DelegateID
        AND CAST(p.PaymentDate AS date) >= @Cutoff
  );",
                new { Cutoff = cutoff },
                cancellationToken: ct)) ?? 0m;
        }
        else
        {
            activeListsAmount = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(@"
SELECT CAST(SUM(ISNULL(vb.AmountDenar, 0)) AS decimal(18,3))
FROM dbo.Delegates d
INNER JOIN dbo.View_Box vb ON vb.BoxID = d.BoxID
WHERE ISNULL(d.DelegateState, 0) = 1
  AND d.BoxID IS NOT NULL
  AND d.BoxID NOT IN @ExcludeIds
  AND EXISTS (
      SELECT 1
      FROM dbo.Customers c
      INNER JOIN dbo.View_CustomersPaymentsDelegate p ON p.CustomerID = c.CustomerID
      WHERE c.DelegateID = d.DelegateID
        AND CAST(p.PaymentDate AS date) >= @Cutoff
  );",
                new { Cutoff = cutoff, ExcludeIds = exclude.ToArray() },
                cancellationToken: ct)) ?? 0m;
        }

        var total = treasuryAmount + abuAmount + activeListsAmount;
        return ("المبلغ الكلي", total);
    }

    /// <summary>
    /// قاصة/خزينة ابو سجاد فقط — يستبعد أي صندوق اسمه «دين …».
    /// </summary>
    private static async Task<(int BoxId, decimal Amount)> LoadAbuSajjadCashBoxAsync(
        SqlConnection connection, CancellationToken ct)
    {
        var row = await connection.QueryFirstOrDefaultAsync<(int BoxID, decimal AmountDenar)>(
            new CommandDefinition(@"
SELECT TOP 1
    b.BoxID,
    CAST(ISNULL(v.AmountDenar, 0) AS decimal(18,3)) AS AmountDenar
FROM dbo.Boxes b
LEFT JOIN dbo.View_Box v ON v.BoxID = b.BoxID
WHERE ISNULL(b.BoxState, 1) = 1
  AND (
        b.BoxName LIKE N'%ابو سجاد%'
     OR b.BoxName LIKE N'%أبو سجاد%'
     OR b.BoxName LIKE N'%ابوسجاد%'
  )
  AND b.BoxName NOT LIKE N'%دين%'
  AND (
        b.BoxName LIKE N'%قاصة%'
     OR b.BoxName LIKE N'%خزين%'
     OR b.BoxName LIKE N'%خزينة%'
     OR b.BoxName LIKE N'%خزينه%'
  )
ORDER BY ISNULL(v.AmountDenar, 0) DESC;",
                cancellationToken: ct));

        if (row.BoxID <= 0)
        {
            return (0, 0m);
        }

        return (row.BoxID, row.AmountDenar);
    }

    /// <summary>
    /// Province treasury box (خزينة المحافظة) — used only as FromBox for صرفيات transfers.
    /// </summary>
    private static async Task<(int BoxId, string? BoxName, decimal Amount)> LoadPrimaryBoxAsync(
        SqlConnection connection, string? branchName, CancellationToken ct)
    {
        var boxes = (await connection.QueryAsync<(int BoxID, string? BoxName, bool? BoxState, decimal? AmountDenar)>(
            new CommandDefinition(@"
SELECT TOP 50
    b.BoxID,
    b.BoxName,
    b.BoxState,
    CAST(ISNULL(v.AmountDenar, 0) AS decimal(18,3)) AS AmountDenar
FROM dbo.Boxes b
LEFT JOIN dbo.View_Box v ON v.BoxID = b.BoxID
WHERE ISNULL(b.BoxState, 1) = 1
ORDER BY ISNULL(v.AmountDenar, 0) DESC;",
                cancellationToken: ct))).ToList();

        if (boxes.Count == 0)
        {
            return (0, null, 0m);
        }

        // Prefer explicit province treasury naming.
        var treasury = boxes.FirstOrDefault(b =>
            !string.IsNullOrWhiteSpace(b.BoxName)
            && (b.BoxName.Contains("خزينة المحافظة", StringComparison.OrdinalIgnoreCase)
                || b.BoxName.Contains("خزينة المحافظه", StringComparison.OrdinalIgnoreCase)
                || (b.BoxName.Contains("خزينة", StringComparison.OrdinalIgnoreCase)
                    && b.BoxName.Contains("محافظ", StringComparison.OrdinalIgnoreCase))));
        if (treasury.BoxID > 0)
        {
            return (treasury.BoxID, treasury.BoxName, treasury.AmountDenar ?? 0m);
        }

        var name = (branchName ?? "").Trim();
        if (!string.IsNullOrEmpty(name))
        {
            // Prefer «خزينة {المحافظة}» over صرفيات / ذمم that also contain the city name.
            var treasuryNamed = boxes.FirstOrDefault(b =>
                !string.IsNullOrWhiteSpace(b.BoxName)
                && b.BoxName.Contains("خزين", StringComparison.OrdinalIgnoreCase)
                && !b.BoxName.Contains("صرف", StringComparison.OrdinalIgnoreCase)
                && (b.BoxName.Contains(name, StringComparison.OrdinalIgnoreCase)
                    || name.Contains(b.BoxName, StringComparison.OrdinalIgnoreCase)));
            if (treasuryNamed.BoxID > 0)
            {
                return (treasuryNamed.BoxID, treasuryNamed.BoxName, treasuryNamed.AmountDenar ?? 0m);
            }

            var match = boxes.FirstOrDefault(b =>
                !string.IsNullOrWhiteSpace(b.BoxName)
                && !b.BoxName.Contains("صرف", StringComparison.OrdinalIgnoreCase)
                && (b.BoxName.Contains(name, StringComparison.OrdinalIgnoreCase)
                    || name.Contains(b.BoxName, StringComparison.OrdinalIgnoreCase)));
            if (match.BoxID > 0)
            {
                return (match.BoxID, match.BoxName, match.AmountDenar ?? 0m);
            }
        }

        var qasa = boxes.FirstOrDefault(b =>
            !string.IsNullOrWhiteSpace(b.BoxName)
            && b.BoxName.Contains("قاصة", StringComparison.OrdinalIgnoreCase)
            && !b.BoxName.Contains("صرف", StringComparison.OrdinalIgnoreCase));
        if (qasa.BoxID > 0)
        {
            return (qasa.BoxID, qasa.BoxName, qasa.AmountDenar ?? 0m);
        }

        var top = boxes[0];
        return (top.BoxID, top.BoxName, top.AmountDenar ?? 0m);
    }

    /// <summary>
    /// صرفيات = مجموع النقل من خزينة المحافظة إلى قاصة الصرفيات (View_TransferBoxs) يومياً.
    /// </summary>
    private static async Task<List<DirectorSummaryDayTotalDto>> QueryExpenseDayTotalsAsync(
        SqlConnection connection, DateTime from, DateTime toExclusive, int primaryBoxId, CancellationToken ct)
    {
        if (primaryBoxId <= 0)
        {
            return [];
        }

        return await QueryDayTotalsAsync(connection, @"
SELECT DAY(t.DateCreate) AS [Day], CAST(SUM(ISNULL(t.AmountDenar, 0)) AS decimal(18,3)) AS Total
FROM dbo.View_TransferBoxs t
WHERE t.DateCreate >= @From AND t.DateCreate < @ToExclusive
  AND t.FromBoxID = @FromBoxId
  AND (
        t.ToBoxName LIKE N'%صرف%'
     OR t.ToBoxName LIKE N'%المصروفات%'
  )
GROUP BY DAY(t.DateCreate);", from, toExclusive, ct, new { FromBoxId = primaryBoxId });
    }

    private static async Task<List<Dictionary<string, object?>>> QueryReceiptRowsAsync(
        SqlConnection connection, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await connection.QueryAsync(new CommandDefinition(@"
SELECT
    ISNULL(NULLIF(LTRIM(RTRIM(v.DelegateName)), N''), N'بدون قائمة') AS name,
    CAST(SUM(ISNULL(v.AmountDenar, 0)) AS decimal(18,3)) AS amount,
    MAX(v.DelegateID) AS delegateId
FROM dbo.View_CustomersPayments v
INNER JOIN dbo.Customers c ON c.CustomerID = v.CustomerID
WHERE v.PaymentDate >= @From AND v.PaymentDate < @To
  AND ISNULL(c.IsLegal, 0) = 0
GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(v.DelegateName)), N''), N'بدون قائمة')
HAVING SUM(ISNULL(v.AmountDenar, 0)) > 0
ORDER BY amount DESC;",
            new { From = from, To = to },
            commandTimeout: 120,
            cancellationToken: ct));

        return MapRows(rows);
    }

    private static async Task<List<Dictionary<string, object?>>> QueryExpenseRowsAsync(
        SqlConnection connection, DateTime from, DateTime to, int primaryBoxId, CancellationToken ct)
    {
        if (primaryBoxId <= 0)
        {
            return [];
        }

        var rows = await connection.QueryAsync(new CommandDefinition(@"
SELECT
    CAST(ISNULL(t.AmountDenar, 0) AS decimal(18,3)) AS amount,
    ISNULL(t.FromBoxName, N'') AS fromBoxName,
    ISNULL(t.ToBoxName, N'') AS toBoxName,
    ISNULL(t.Notes, N'') AS notes,
    t.DateCreate AS dateCreate,
    t.TransferBoxID AS id
FROM dbo.View_TransferBoxs t
WHERE t.DateCreate >= @From AND t.DateCreate < @To
  AND t.FromBoxID = @FromBoxId
  AND (
        t.ToBoxName LIKE N'%صرف%'
     OR t.ToBoxName LIKE N'%المصروفات%'
  )
  AND ISNULL(t.AmountDenar, 0) <> 0
ORDER BY t.DateCreate DESC, t.TransferBoxID DESC;",
            new { From = from, To = to, FromBoxId = primaryBoxId },
            commandTimeout: 120,
            cancellationToken: ct));

        return MapRows(rows);
    }

    private static async Task<List<Dictionary<string, object?>>> QuerySaleRowsAsync(
        SqlConnection connection, DateTime from, DateTime to, bool saleAmount, CancellationToken ct)
    {
        var amountExpr = saleAmount
            ? "CAST(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0) AS decimal(18,3))"
            : "CAST(ISNULL(v.AmountTotalCostDenar, 0) AS decimal(18,3))";
        var rows = await connection.QueryAsync(new CommandDefinition($@"
SELECT
    ISNULL(NULLIF(LTRIM(RTRIM(v.CustomerName)), N''), N'بدون اسم') AS name,
    {amountExpr} AS amount,
    CAST(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0) AS decimal(18,3)) AS salePrice,
    CAST(ISNULL(v.AmountTotalCostDenar, 0) AS decimal(18,3)) AS purchaseCost,
    ISNULL(v.DelegateName, N'') AS listName,
    v.CustomerSaleID AS id,
    v.CustomerID AS customerId,
    v.DateCreate AS dateCreate
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
  AND {amountExpr} <> 0
ORDER BY v.DateCreate DESC, v.CustomerSaleID DESC;",
            new { From = from, To = to },
            commandTimeout: 120,
            cancellationToken: ct));

        return MapRows(rows);
    }

    private static async Task<List<Dictionary<string, object?>>> QueryBuyRowsAsync(
        SqlConnection connection, DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await connection.QueryAsync(new CommandDefinition(@"
SELECT
    CAST(ISNULL(v.TotalAmountDenar, 0) AS decimal(18,3)) AS amount,
    ISNULL(v.SupplierName, N'') AS supplierName,
    ISNULL(v.Notes, N'') AS notes,
    ISNULL(v.StoreName, N'') AS storeName,
    ISNULL(v.BoxName, N'') AS boxName,
    v.BuyID AS id,
    v.DateCreate AS dateCreate
FROM dbo.View_Buys v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
  AND ISNULL(v.BuyState, 1) = 1
  AND ISNULL(v.TotalAmountDenar, 0) <> 0
ORDER BY v.DateCreate DESC, v.BuyID DESC;",
            new { From = from, To = to },
            commandTimeout: 120,
            cancellationToken: ct));

        return MapRows(rows);
    }

    private static List<Dictionary<string, object?>> MapRows(IEnumerable<dynamic> rows)
    {
        var list = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            list.Add(ToDict(row));
        }

        return list;
    }

    private static Dictionary<string, object?> ToDict(object row)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (row is IDictionary<string, object> map)
        {
            foreach (var kv in map)
            {
                dict[ToCamel(kv.Key)] = kv.Value is DBNull ? null : kv.Value;
            }
        }

        return dict;
    }

    private static string ToCamel(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (name.Length == 1)
        {
            return name.ToLowerInvariant();
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
