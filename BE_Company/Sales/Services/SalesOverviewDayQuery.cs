using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// One calendar day in Iraq local time. Official sales use DateCreate.
    /// Sale orders use CreatedAtUtc converted from the Iraq day.
    /// </summary>
    public sealed class SalesOverviewDayQuery
    {
        private readonly SalesDevelopmentGuard _guard;

        public SalesOverviewDayQuery(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        public static bool TryNormalize(int? year, int? month, int? day, DateTime iraqNow, out DateTime date)
        {
            var y = year is >= 2000 and <= 2100 ? year.Value : iraqNow.Year;
            var m = month is >= 1 and <= 12 ? month.Value : iraqNow.Month;
            var last = DateTime.DaysInMonth(y, m);
            if (day is not int d || d < 1 || d > last)
            {
                date = default;
                return false;
            }

            date = new DateTime(y, m, d);
            return true;
        }

        public async Task<SalesDayDetailDTO> FillAsync(int? year, int? month, int? day, DateTime iraqNow, CancellationToken ct)
        {
            if (!TryNormalize(year, month, day, iraqNow, out var date))
            {
                return new SalesDayDetailDTO { DayError = "التاريخ غير صحيح." };
            }

            var dto = new SalesDayDetailDTO
            {
                Year = date.Year,
                Month = date.Month,
                Day = date.Day
            };
            var cs = _guard.GetSalesConnectionString();
            if (string.IsNullOrWhiteSpace(cs))
            {
                dto.DayError = "لا يوجد اتصال بقاعدة الفرع.";
                return dto;
            }

            var from = date;
            var to = date.AddDays(1);
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

                var total = await connection.QuerySingleAsync<TotalRow>(new CommandDefinition($@"
SELECT COUNT(*) AS SalesCount,
       CAST(ISNULL(SUM(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0)), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}",
                    new { From = from, To = to },
                    commandTimeout: 20,
                    cancellationToken: ct));
                dto.SalesCount = total.SalesCount;
                dto.SalePrice = total.SalePrice;
                dto.AverageSalePrice = SalesOverviewMath.Average(total.SalePrice, total.SalesCount);

                dto.Sellers = (await connection.QueryAsync<PersonRow>(new CommandDefinition($@"
SELECT CASE WHEN v.UserName IS NULL OR LTRIM(RTRIM(v.UserName)) = N'' THEN N'بدون اسم' ELSE LTRIM(RTRIM(v.UserName)) END AS Name,
       COUNT(*) AS [Count],
       CAST(ISNULL(SUM(COALESCE(v.AmountTotalSalesDenar, v.AmountTotalDenar, 0)), 0) AS decimal(18,3)) AS SalePrice
FROM dbo.View_CustomersSales v
WHERE v.DateCreate >= @From AND v.DateCreate < @To
{exclude}
GROUP BY CASE WHEN v.UserName IS NULL OR LTRIM(RTRIM(v.UserName)) = N'' THEN N'بدون اسم' ELSE LTRIM(RTRIM(v.UserName)) END",
                    new { From = from, To = to },
                    commandTimeout: 20,
                    cancellationToken: ct)))
                    .Select(row => new SalesDayPersonDTO
                    {
                        Name = string.IsNullOrWhiteSpace(row.Name) ? "بدون اسم" : row.Name.Trim(),
                        Count = row.Count,
                        SalePrice = row.SalePrice
                    })
                    .OrderByDescending(x => x.SalePrice)
                    .ThenBy(x => x.Name, StringComparer.Ordinal)
                    .ToList();

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
                    new { From = from, To = to },
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
                    .ThenBy(x => x.ItemName, StringComparer.Ordinal)
                    .ToList();
                dto.ItemQuantity = dto.Items.Sum(x => x.Quantity);

                if (canLinkDeleted)
                {
                    dto.DownPayment = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition($@"
SELECT CAST(ISNULL(SUM(x.DownPayment), 0) AS decimal(18,3))
FROM (
    SELECT v.CustomerSaleID, MAX(ISNULL(d.DownPayment, 0)) AS DownPayment
    FROM dbo.View_CustomersSales v
    INNER JOIN dbo.SalesDrafts d ON d.CustomerSaleId = v.CustomerSaleID
    WHERE v.DateCreate >= @From AND v.DateCreate < @To
    {exclude}
    GROUP BY v.CustomerSaleID
) x",
                        new { From = from, To = to },
                        commandTimeout: 20,
                        cancellationToken: ct));
                }

                if (await TableExistsAsync(connection, "SalesRequests", ct) == 1
                    && await ColumnExistsAsync(connection, "SalesRequests", "IsDeleted", ct) == 1
                    && await ColumnExistsAsync(connection, "SalesRequests", "CreatedAtUtc", ct) == 1)
                try
                {
                    var fromUtc = IraqTimeService.ToUtcFromIraq(from);
                    var toUtc = IraqTimeService.ToUtcFromIraq(to);
                    var pulse = await connection.QuerySingleAsync<PulseRow>(new CommandDefinition(@"
SELECT
  ISNULL(SUM(CASE WHEN CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc THEN 1 ELSE 0 END), 0) AS Added,
  ISNULL(SUM(CASE WHEN CompletedAtUtc >= @FromUtc AND CompletedAtUtc < @ToUtc THEN 1 ELSE 0 END), 0) AS Completed,
  ISNULL(SUM(CASE WHEN RejectedAtUtc >= @FromUtc AND RejectedAtUtc < @ToUtc THEN 1 ELSE 0 END), 0) AS Rejected
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0",
                        new { FromUtc = fromUtc, ToUtc = toUtc },
                        commandTimeout: 20,
                        cancellationToken: ct));
                    dto.OrdersAdded = pulse.Added;
                    dto.OrdersCompleted = pulse.Completed;
                    dto.OrdersRejected = pulse.Rejected;

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

                    dto.Creators = (await connection.QueryAsync<PersonRow>(new CommandDefinition(@"
SELECT CASE WHEN CreatedByName IS NULL OR LTRIM(RTRIM(CreatedByName)) = N'' THEN N'بدون اسم' ELSE LTRIM(RTRIM(CreatedByName)) END AS Name,
       ISNULL(CreatedByUserType, N'') AS Role,
       COUNT(*) AS [Count],
       CAST(0 AS decimal(18,3)) AS SalePrice
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
  AND CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc
GROUP BY
  CASE WHEN CreatedByName IS NULL OR LTRIM(RTRIM(CreatedByName)) = N'' THEN N'بدون اسم' ELSE LTRIM(RTRIM(CreatedByName)) END,
  ISNULL(CreatedByUserType, N'')",
                        new { FromUtc = fromUtc, ToUtc = toUtc },
                        commandTimeout: 20,
                        cancellationToken: ct)))
                        .Select(row => new SalesDayPersonDTO
                        {
                            Name = string.IsNullOrWhiteSpace(row.Name) ? "بدون اسم" : row.Name.Trim(),
                            Role = row.Role?.Trim() ?? string.Empty,
                            Count = row.Count
                        })
                        .OrderByDescending(x => x.Count)
                        .ThenBy(x => x.Name, StringComparer.Ordinal)
                        .ToList();

                    var agentRows = (await connection.QueryAsync<AgentRow>(new CommandDefinition(@"
SELECT CASE WHEN TargetEmployeeName IS NULL OR LTRIM(RTRIM(TargetEmployeeName)) = N'' THEN N'غير مسند' ELSE LTRIM(RTRIM(TargetEmployeeName)) END AS Name,
       ISNULL(Status, N'') AS Status,
       COUNT(*) AS [Count]
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
  AND CreatedAtUtc >= @FromUtc AND CreatedAtUtc < @ToUtc
GROUP BY
  CASE WHEN TargetEmployeeName IS NULL OR LTRIM(RTRIM(TargetEmployeeName)) = N'' THEN N'غير مسند' ELSE LTRIM(RTRIM(TargetEmployeeName)) END,
  ISNULL(Status, N'')",
                        new { FromUtc = fromUtc, ToUtc = toUtc },
                        commandTimeout: 20,
                        cancellationToken: ct))).ToList();
                    dto.Agents = agentRows
                        .GroupBy(row => string.IsNullOrWhiteSpace(row.Name) ? "غير مسند" : row.Name.Trim(), StringComparer.Ordinal)
                        .Select(group => new SalesDayAgentDTO
                        {
                            Name = group.Key,
                            Orders = group.Sum(x => x.Count),
                            Statuses = group
                                .Select(x => new SalesOverviewStatusDTO
                                {
                                    Status = x.Status ?? string.Empty,
                                    Count = x.Count
                                })
                                .OrderByDescending(x => x.Count)
                                .ToList()
                        })
                        .OrderByDescending(x => x.Orders)
                        .ThenBy(x => x.Name, StringComparer.Ordinal)
                        .ToList();
                }
                catch (Exception ex)
                {
                    dto.DayError = "تعذر حساب طلبات هذا اليوم.";
                    Console.Error.WriteLine($"sales day orders failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                dto.DayError = "تعذر حساب إحصاءات اليوم.";
                Console.Error.WriteLine($"sales day overview failed: {ex.Message}");
            }

            return dto;
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

        private sealed class TotalRow
        {
            public int SalesCount { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class PersonRow
        {
            public string? Name { get; set; }
            public string? Role { get; set; }
            public int Count { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class ItemRow
        {
            public string? ItemName { get; set; }
            public int Quantity { get; set; }
            public int SalesCount { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class PulseRow
        {
            public int Added { get; set; }
            public int Completed { get; set; }
            public int Rejected { get; set; }
        }

        private sealed class StatusRow
        {
            public string? Status { get; set; }
            public int Count { get; set; }
        }

        private sealed class AgentRow
        {
            public string? Name { get; set; }
            public string? Status { get; set; }
            public int Count { get; set; }
        }
    }
}
