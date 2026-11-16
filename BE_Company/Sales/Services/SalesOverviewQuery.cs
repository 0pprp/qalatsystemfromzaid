using BE_Company.Sales;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Month overview for one branch database.
    /// Official sales come from View_CustomersSales (hard-deleted rows are already gone).
    /// Sales still linked to a soft-deleted طلب بيع are excluded as well.
    /// </summary>
    public sealed class SalesOverviewQuery
    {
        private readonly SalesDevelopmentGuard _guard;

        public SalesOverviewQuery(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        public async Task FillAsync(SalesManagerDashboardDTO dto, int? year, int? month, DateTime iraqNow, CancellationToken ct)
        {
            var y = year ?? 0;
            var m = month ?? 0;
            SalesOverviewMath.NormalizePeriod(ref y, ref m, iraqNow, out var from, out var toExclusive, out var previousFrom, out var days);
            dto.Year = y;
            dto.Month = m;
            dto.DaysInMonth = days;
            for (var day = 1; day <= days; day++)
            {
                dto.Daily.Add(new SalesOverviewDayDTO { Day = day });
            }

            var cs = _guard.GetSalesConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
            {
                dto.OverviewError = "لا يوجد اتصال بقاعدة الفرع.";
                return;
            }

            try
            {
                await using var connection = new SqlConnection(cs);
                await connection.OpenAsync(ct);
                var canLinkDeleted = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE
    WHEN OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL
     AND OBJECT_ID(N'dbo.SalesRequests', N'U') IS NOT NULL
     AND COL_LENGTH(N'dbo.SalesDrafts', N'CustomerSaleId') IS NOT NULL
     AND COL_LENGTH(N'dbo.SalesRequests', N'IsDeleted') IS NOT NULL
    THEN 1 ELSE 0 END", cancellationToken: ct)) == 1;

                var exclude = canLinkDeleted
                    ? @"
AND NOT EXISTS (
    SELECT 1
    FROM dbo.SalesDrafts d
    INNER JOIN dbo.SalesRequests r ON r.Id = d.SalesRequestId
    WHERE d.CustomerSaleId = v.CustomerSaleID
      AND ISNULL(r.IsDeleted, 0) = 1
)"
                    : string.Empty;

                var current = await SumAsync(connection, from, toExclusive, exclude, ct);
                var previous = await SumAsync(connection, previousFrom, from, exclude, ct);
                dto.MonthSalesCount = current.SalesCount;
                dto.MonthSalePrice = current.SalePrice;
                dto.PreviousMonthSalesCount = previous.SalesCount;
                dto.PreviousMonthSalePrice = previous.SalePrice;
                dto.SalesCountGrowthPercent = SalesOverviewMath.GrowthPercent(current.SalesCount, previous.SalesCount);
                dto.SalePriceGrowthPercent = SalesOverviewMath.GrowthPercent(current.SalePrice, previous.SalePrice);
                dto.AverageSalePrice = SalesOverviewMath.Average(current.SalePrice, current.SalesCount);
                await FillMonthCollectedAsync(connection, dto, from, toExclusive, exclude, ct);

                var daily = await connection.QueryAsync<DayRow>(new CommandDefinition($@"
SELECT DAY(v.DateCreate) AS [Day],
       COUNT(*) AS SalesCount,
       CAST(ISNULL(SUM(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0)), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}
GROUP BY DAY(v.DateCreate)",
                    new { From = from, To = toExclusive },
                    commandTimeout: 20,
                    cancellationToken: ct));
                foreach (var row in daily)
                {
                    if (row.Day < 1 || row.Day > days)
                    {
                        continue;
                    }

                    var slot = dto.Daily[row.Day - 1];
                    slot.SalesCount = row.SalesCount;
                    slot.SalePrice = row.SalePrice;
                }

                var items = (await connection.QueryAsync<ItemRow>(new CommandDefinition($@"
SELECT ISNULL(NULLIF(LTRIM(RTRIM(i.ItemName)), N''), N'بدون اسم') AS ItemName,
       SUM(ISNULL(sis.Quantity, 0)) AS Quantity,
       COUNT(DISTINCT v.CustomerSaleID) AS SalesCount,
       CAST(ISNULL(SUM(ISNULL(i.ItemPrice, 0) * 1448.0 * ISNULL(sis.Quantity, 0)), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
INNER JOIN dbo.SelectItemsSales sis ON sis.CustomerSaleID = v.CustomerSaleID
INNER JOIN dbo.Items i ON i.ItemID = sis.ItemID
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}
GROUP BY ISNULL(NULLIF(LTRIM(RTRIM(i.ItemName)), N''), N'بدون اسم')",
                    new { From = from, To = toExclusive },
                    commandTimeout: 20,
                    cancellationToken: ct))).ToList();
                dto.Items = items
                    .Select(row => new SalesOverviewItemDTO
                    {
                        ItemName = row.ItemName ?? "بدون اسم",
                        Category = SalesOverviewMath.CategorizeItem(row.ItemName),
                        Quantity = row.Quantity,
                        SalesCount = row.SalesCount,
                        SalePrice = row.SalePrice
                    })
                    .OrderByDescending(x => x.SalePrice)
                    .ThenBy(x => x.ItemName)
                    .ToList();
                dto.MonthItemQuantity = dto.Items.Sum(x => x.Quantity);
                dto.Categories = SalesOverviewMath.CategoriesFrom(dto.Items);

                var ratedSales = await TryFillPaymentRatingsAsync(connection, dto, from, toExclusive, exclude, iraqNow.Date, ct);
                if (!ratedSales)
                {
                    dto.Sellers = (await connection.QueryAsync<PersonRow>(new CommandDefinition($@"
SELECT CASE WHEN v.UserName IS NULL OR LTRIM(RTRIM(v.UserName)) = N'' THEN N'بدون اسم' ELSE LTRIM(RTRIM(v.UserName)) END AS Name,
       CAST(N'' AS nvarchar(100)) AS Role,
       COUNT(*) AS SalesCount,
       CAST(ISNULL(SUM(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0)), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}
GROUP BY CASE WHEN v.UserName IS NULL OR LTRIM(RTRIM(v.UserName)) = N'' THEN N'بدون اسم' ELSE LTRIM(RTRIM(v.UserName)) END",
                        new { From = from, To = toExclusive },
                        commandTimeout: 20,
                        cancellationToken: ct)))
                        .Select(ToPerson)
                        .OrderByDescending(x => x.SalePrice)
                        .ThenBy(x => x.Name)
                        .ToList();
                    dto.UnscoredSalesCount = dto.MonthSalesCount;
                }

                if (canLinkDeleted)
                {
                    dto.Recommenders = (await connection.QueryAsync<PersonRow>(new CommandDefinition($@"
SELECT CASE WHEN rec.Name IS NULL OR LTRIM(RTRIM(rec.Name)) = N'' THEN N'بدون موصي' ELSE LTRIM(RTRIM(rec.Name)) END AS Name,
       ISNULL(rec.Role, N'') AS Role,
       COUNT(*) AS SalesCount,
       CAST(ISNULL(SUM(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0)), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
LEFT JOIN (
    SELECT d.CustomerSaleId AS CustomerSaleId,
           MAX(LTRIM(RTRIM(ISNULL(r.CreatedByName, N'')))) AS Name,
           MAX(LTRIM(RTRIM(ISNULL(r.CreatedByUserType, N'')))) AS Role
    FROM dbo.SalesDrafts d
    INNER JOIN dbo.SalesRequests r ON r.Id = d.SalesRequestId AND ISNULL(r.IsDeleted, 0) = 0
    WHERE d.CustomerSaleId IS NOT NULL
    GROUP BY d.CustomerSaleId
) rec ON rec.CustomerSaleId = v.CustomerSaleID
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}
GROUP BY
    CASE WHEN rec.Name IS NULL OR LTRIM(RTRIM(rec.Name)) = N'' THEN N'بدون موصي' ELSE LTRIM(RTRIM(rec.Name)) END,
    ISNULL(rec.Role, N'')",
                        new { From = from, To = toExclusive },
                        commandTimeout: 20,
                        cancellationToken: ct)))
                        .Select(ToPerson)
                        .OrderByDescending(x => x.SalePrice)
                        .ThenBy(x => x.Name)
                        .ToList();

                    dto.MonthDownPayment = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition($@"
SELECT CAST(ISNULL(SUM(x.DownPayment), 0) AS decimal(18,3))
FROM (
    SELECT v.CustomerSaleID, MAX(ISNULL(d.DownPayment, 0)) AS DownPayment
    FROM dbo.View_CustomersSales v
    INNER JOIN dbo.SalesDrafts d ON d.CustomerSaleId = v.CustomerSaleID
    WHERE v.DateCreate >= @From AND v.DateCreate < @To
    {exclude}
    GROUP BY v.CustomerSaleID
) x",
                        new { From = from, To = toExclusive },
                        commandTimeout: 20,
                        cancellationToken: ct));
                }

                if (await TableExistsAsync(connection, "SalesRequests", ct) == 1
                    && await ColumnExistsAsync(connection, "SalesRequests", "IsDeleted", ct) == 1)
                {
                    var fromUtc = IraqTimeService.ToUtcFromIraq(from);
                    var toUtc = IraqTimeService.ToUtcFromIraq(toExclusive);
                    var orders = await connection.QuerySingleAsync<OrderRow>(new CommandDefinition(@"
SELECT
  ISNULL(SUM(CASE WHEN CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc THEN 1 ELSE 0 END), 0) AS CreatedThisMonth,
  ISNULL(SUM(CASE WHEN CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc AND Status = N'Completed' THEN 1 ELSE 0 END), 0) AS ConvertedFromCreated,
  ISNULL(SUM(CASE WHEN CompletedAtUtc >= @FromUtc AND CompletedAtUtc < @ToUtc THEN 1 ELSE 0 END), 0) AS FinishedThisMonth,
  ISNULL(SUM(CASE WHEN RejectedAtUtc >= @FromUtc AND RejectedAtUtc < @ToUtc THEN 1 ELSE 0 END), 0) AS RejectedThisMonth,
  ISNULL(SUM(CASE WHEN Status NOT IN (N'Completed', N'Rejected') THEN 1 ELSE 0 END), 0) AS OpenNow,
  ISNULL(SUM(CASE WHEN Status = N'Pending' THEN 1 ELSE 0 END), 0) AS PendingNow,
  ISNULL(SUM(CASE WHEN Status = N'New' THEN 1 ELSE 0 END), 0) AS UnassignedNow
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0",
                        new { FromUtc = fromUtc, ToUtc = toUtc },
                        commandTimeout: 20,
                        cancellationToken: ct));
                    dto.OrdersCreated = orders.CreatedThisMonth;
                    dto.OrdersConverted = orders.ConvertedFromCreated;
                    dto.OrdersFinished = orders.FinishedThisMonth;
                    dto.OrdersRejected = orders.RejectedThisMonth;
                    dto.OrdersOpen = orders.OpenNow;
                    dto.OrdersPending = orders.PendingNow;
                    dto.OrdersUnassigned = orders.UnassignedNow;
                    dto.OrderConversionPercent = SalesOverviewMath.ConversionPercent(orders.ConvertedFromCreated, orders.CreatedThisMonth);
                    dto.OrderStatuses = (await connection.QueryAsync<StatusRow>(new CommandDefinition(@"
SELECT Status, COUNT(*) AS [Count]
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
  AND CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc
GROUP BY Status",
                        new { FromUtc = fromUtc, ToUtc = toUtc },
                        commandTimeout: 20,
                        cancellationToken: ct)))
                        .Select(row => new SalesOverviewStatusDTO
                        {
                            Status = row.Status ?? string.Empty,
                            Count = row.Count
                        })
                        .OrderByDescending(x => x.Count)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                dto.OverviewError = "تعذر حساب إحصاءات الشهر.";
                Console.Error.WriteLine($"sales overview failed: {ex.Message}");
            }
        }

        /// <summary>
        /// One aggregate on CustomersPayments and AddToBox for customers who bought this month.
        /// Payments count from the sale day onward and are capped at that customer's month sale price.
        /// Does not read View_CustomersPaymentsDelegate.
        /// </summary>
        private static async Task FillMonthCollectedAsync(
            SqlConnection connection,
            SalesManagerDashboardDTO dto,
            DateTime from,
            DateTime toExclusive,
            string exclude,
            CancellationToken ct)
        {
            try
            {
                var ready = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE
    WHEN OBJECT_ID(N'dbo.CustomersPayments', N'U') IS NULL THEN 0
    WHEN OBJECT_ID(N'dbo.AddToBox', N'U') IS NULL THEN 0
    WHEN COL_LENGTH(N'dbo.View_CustomersSales', N'CustomerID') IS NULL THEN 0
    WHEN COL_LENGTH(N'dbo.CustomersPayments', N'CustomerID') IS NULL THEN 0
    WHEN COL_LENGTH(N'dbo.CustomersPayments', N'PaymentDate') IS NULL THEN 0
    WHEN COL_LENGTH(N'dbo.AddToBox', N'CustomerPaymentID') IS NULL THEN 0
    WHEN COL_LENGTH(N'dbo.AddToBox', N'Amount') IS NULL THEN 0
    ELSE 1 END", cancellationToken: ct));
                if (ready != 1)
                {
                    return;
                }

                dto.MonthCollected = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition($@"
WITH MonthSales AS (
    SELECT ISNULL(v.CustomerID, 0) AS CustomerID,
           CAST(v.DateCreate AS date) AS SaleDay,
           CAST(ISNULL(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0), 0) AS decimal(18,3)) AS SalePrice
    FROM dbo.View_CustomersSales v
    WHERE v.DateCreate >= @From AND v.DateCreate < @To
    {exclude}
),
Bounds AS (
    SELECT CustomerID,
           MIN(SaleDay) AS FirstSaleDay,
           SUM(SalePrice) AS SalePrice
    FROM MonthSales
    WHERE CustomerID > 0
    GROUP BY CustomerID
),
Paid AS (
    SELECT b.CustomerID,
           b.SalePrice,
           CAST(ISNULL(SUM(ab.Amount * 1448.0), 0) AS decimal(18,3)) AS Paid
    FROM Bounds b
    LEFT JOIN dbo.CustomersPayments cp
        ON cp.CustomerID = b.CustomerID
       AND cp.PaymentDate >= b.FirstSaleDay
    LEFT JOIN dbo.AddToBox ab
        ON ab.CustomerPaymentID = cp.CustomerPaymentID
    GROUP BY b.CustomerID, b.SalePrice
)
SELECT CAST(ISNULL(SUM(
    CASE
        WHEN Paid <= 0 THEN 0
        WHEN Paid >= SalePrice THEN SalePrice
        ELSE Paid
    END
), 0) AS decimal(18,3))
FROM Paid",
                    new { From = from, To = toExclusive },
                    commandTimeout: 20,
                    cancellationToken: ct));
                dto.CollectedPercent = SalesOverviewMath.CollectedPercent(dto.MonthCollected, dto.MonthSalePrice);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"sales overview collected percent failed: {ex.Message}");
            }
        }

        private static async Task<TotalRow> SumAsync(
            SqlConnection connection,
            DateTime from,
            DateTime toExclusive,
            string exclude,
            CancellationToken ct)
        {
            return await connection.QuerySingleAsync<TotalRow>(new CommandDefinition($@"
SELECT COUNT(*) AS SalesCount,
       CAST(ISNULL(SUM(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0)), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}",
                new { From = from, To = toExclusive },
                commandTimeout: 20,
                cancellationToken: ct));
        }

        private static Task<int> TableExistsAsync(SqlConnection connection, string table, CancellationToken ct) =>
            connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(@Name, N'U') IS NULL THEN 0 ELSE 1 END",
                new { Name = "dbo." + table },
                cancellationToken: ct));

        private static Task<int> ColumnExistsAsync(SqlConnection connection, string table, string column, CancellationToken ct) =>
            connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN COL_LENGTH(@Table, @Column) IS NULL THEN 0 ELSE 1 END",
                new { Table = "dbo." + table, Column = column },
                cancellationToken: ct));

        private static async Task<bool> TryFillPaymentRatingsAsync(
            SqlConnection connection,
            SalesManagerDashboardDTO dto,
            DateTime from,
            DateTime toExclusive,
            string exclude,
            DateTime asOf,
            CancellationToken ct)
        {
            try
            {
                var ready = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE
    WHEN COL_LENGTH(N'dbo.View_CustomersSales', N'CustomerID') IS NULL THEN 0
    WHEN OBJECT_ID(N'dbo.View_CustomersSalesDelegate', N'V') IS NULL THEN 0
    WHEN OBJECT_ID(N'dbo.View_CustomersPaymentsDelegate', N'V') IS NULL THEN 0
    WHEN COL_LENGTH(N'dbo.Customers', N'IsLegal') IS NULL THEN 0
    WHEN COL_LENGTH(N'dbo.Customers', N'IsFakeSale') IS NULL THEN 0
    ELSE 1 END", cancellationToken: ct));
                if (ready != 1)
                {
                    return false;
                }

                var hasEquation = await ColumnExistsAsync(connection, "Customers", "RatingEquationFactor", ct) == 1;
                var equationSql = hasEquation
                    ? "CAST(ISNULL(C.RatingEquationFactor, 1) AS float)"
                    : "CAST(1 AS float)";
                var sales = (await connection.QueryAsync<SaleCustomerRow>(new CommandDefinition($@"
SELECT ISNULL(v.CustomerID, 0) AS CustomerId,
       CASE WHEN v.UserName IS NULL OR LTRIM(RTRIM(v.UserName)) = N'' THEN N'بدون اسم' ELSE LTRIM(RTRIM(v.UserName)) END AS Seller,
       CAST(ISNULL(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}",
                    new { From = from, To = toExclusive },
                    commandTimeout: 20,
                    cancellationToken: ct))).ToList();

                var facts = new Dictionary<int, CustomerRatingFacts>();
                var ids = sales.Select(row => row.CustomerId).Where(id => id > 0).Distinct().ToList();
                foreach (var chunk in SqlInClauseBatch.Chunk(ids))
                {
                    var rows = await connection.QueryAsync<PaymentFactRow>(new CommandDefinition($@"
SELECT
    C.CustomerID AS CustomerId,
    C.DelegateID AS DelegateId,
    CAST(ISNULL(C.IsLegal, 0) AS bit) AS IsLegal,
    CAST(ISNULL(C.IsFakeSale, 0) AS bit) AS IsFakeSale,
    S.DateSaleDevice,
    P.LastPaymentDate,
    CAST(ISNULL(S.AmountTotalSales, 0) AS float) AS AmountTotalSales,
    CAST(ISNULL(P.ReceiptsTotal, 0) AS float) AS ReceiptsTotal,
    CAST(ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) AS float) AS AmountRemaining,
    CAST(ISNULL(P.ReceiptCount, 0) AS int) AS ReceiptCount,
    {equationSql} AS RatingEquationFactor
FROM dbo.Customers C
OUTER APPLY (
    SELECT
        MAX(V.DateCreate) AS DateSaleDevice,
        ROUND(ISNULL(SUM(V.AmountTotalSalesDenar), 0), -3) AS AmountTotalSales
    FROM dbo.View_CustomersSalesDelegate V
    WHERE V.CustomerID = C.CustomerID
) S
OUTER APPLY (
    SELECT
        MAX(V.PaymentDate) AS LastPaymentDate,
        ROUND(ISNULL(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal,
        COUNT(V.CustomerPaymentID) AS ReceiptCount
    FROM dbo.View_CustomersPaymentsDelegate V
    WHERE V.CustomerID = C.CustomerID
) P
WHERE C.CustomerID IN @Ids",
                        new { Ids = chunk },
                        commandTimeout: 20,
                        cancellationToken: ct));
                    foreach (var row in rows)
                    {
                        facts[row.CustomerId] = new CustomerRatingFacts(
                            row.CustomerId,
                            row.DelegateId,
                            row.IsLegal,
                            row.IsFakeSale,
                            row.DateSaleDevice,
                            row.LastPaymentDate,
                            row.AmountTotalSales,
                            row.ReceiptsTotal,
                            row.AmountRemaining,
                            row.ReceiptCount,
                            row.RatingEquationFactor <= 0 ? 1.0 : row.RatingEquationFactor);
                    }
                }

                SalesOverviewPayment.Apply(
                    dto,
                    sales.Select(row => new SalesOverviewPayment.SaleLine(row.CustomerId, row.Seller ?? "بدون اسم", row.SalePrice)).ToList(),
                    facts,
                    asOf);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"sales overview payment rating failed: {ex.Message}");
                return false;
            }
        }

        private static SalesOverviewPersonDTO ToPerson(PersonRow row) => new()
        {
            Name = string.IsNullOrWhiteSpace(row.Name) ? "بدون اسم" : row.Name.Trim(),
            Role = row.Role?.Trim() ?? string.Empty,
            SalesCount = row.SalesCount,
            SalePrice = row.SalePrice,
            ScoredSales = row.ScoredSales,
            HighScores = row.HighScores,
            LowScores = row.LowScores,
            ScoreSum = row.ScoreSum,
            AverageScore = SalesOverviewMath.AverageScore(row.ScoreSum, row.ScoredSales)
        };

        private sealed class TotalRow
        {
            public int SalesCount { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class DayRow
        {
            public int Day { get; set; }
            public int SalesCount { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class ItemRow
        {
            public string? ItemName { get; set; }
            public int Quantity { get; set; }
            public int SalesCount { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class PersonRow
        {
            public string? Name { get; set; }
            public string? Role { get; set; }
            public int SalesCount { get; set; }
            public decimal SalePrice { get; set; }
            public int ScoredSales { get; set; }
            public int HighScores { get; set; }
            public int LowScores { get; set; }
            public decimal ScoreSum { get; set; }
        }

        private sealed class SaleCustomerRow
        {
            public int CustomerId { get; set; }
            public string? Seller { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class PaymentFactRow
        {
            public int CustomerId { get; set; }
            public int? DelegateId { get; set; }
            public bool IsLegal { get; set; }
            public bool IsFakeSale { get; set; }
            public DateTime? DateSaleDevice { get; set; }
            public DateTime? LastPaymentDate { get; set; }
            public double AmountTotalSales { get; set; }
            public double ReceiptsTotal { get; set; }
            public double AmountRemaining { get; set; }
            public int ReceiptCount { get; set; }
            public double RatingEquationFactor { get; set; } = 1;
        }

        private sealed class OrderRow
        {
            public int CreatedThisMonth { get; set; }
            public int ConvertedFromCreated { get; set; }
            public int FinishedThisMonth { get; set; }
            public int RejectedThisMonth { get; set; }
            public int OpenNow { get; set; }
            public int PendingNow { get; set; }
            public int UnassignedNow { get; set; }
        }

        private sealed class StatusRow
        {
            public string? Status { get; set; }
            public int Count { get; set; }
        }
    }
}
