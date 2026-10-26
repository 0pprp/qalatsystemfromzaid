using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class BranchMonthBucketDto
    {
        public int YearMonth { get; set; }
        public int NewCustomersCount { get; set; }
        public decimal NewCustomersSaleSum { get; set; }
        public decimal NewCustomersDaySalesSum { get; set; }
        public int ZeroedCustomersCount { get; set; }
        public decimal ZeroedCustomersSaleSum { get; set; }
        public decimal ZeroedCustomersDaySalesSum { get; set; }
        public int NetCustomersCount { get; set; }
        public decimal NetSaleSum { get; set; }
        public decimal NetDaySalesSum { get; set; }
    }

    public sealed class BranchMonthStatsDto
    {
        public int CurrentYearMonth { get; set; }
        public int PreviousYearMonth { get; set; }
        public BranchMonthBucketDto Current { get; set; } = new();
        public BranchMonthBucketDto Previous { get; set; } = new();
        public string Scope { get; set; } = "branch";
        public int Id { get; set; }
    }

    public interface IBranchMonthStatsService
    {
        Task<BranchMonthStatsDto> GetAsync(CancellationToken ct);
        Task<BranchMonthStatsDto> GetAsync(string? scope, int id, CancellationToken ct);
    }

    /// <summary>
    /// Branch month stats aligned with director/BM rating cohort for the calendar month:
    /// "new" = open-balance customers whose sale date (official view or completed draft) falls in the month
    /// — same basis as «كل الزبائن ضمن الفترة» when the chart filter is that month.
    /// Zeroed = remaining cleared that month.
    /// Optional scope filters: branch (all), list / follower (Customers.DelegateID), employee (customer ids).
    /// </summary>
    public sealed class BranchMonthStatsService : IBranchMonthStatsService
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly IFollowerUserListsRepository _followerLists;

        public BranchMonthStatsService(
            SalesDevelopmentGuard guard,
            IFollowerUserListsRepository followerLists)
        {
            _guard = guard;
            _followerLists = followerLists;
        }

        public Task<BranchMonthStatsDto> GetAsync(CancellationToken ct)
            => GetAsync("branch", 0, ct);

        public async Task<BranchMonthStatsDto> GetAsync(string? scope, int id, CancellationToken ct)
        {
            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales connection string missing.");
            var now = DateTime.UtcNow.AddHours(3);
            var currentYm = ToYearMonth(now);
            var previousYm = PreviousYearMonth(currentYm);
            var normalized = (scope ?? "branch").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = "branch";
            }

            var filter = await ResolveFilterAsync(normalized, id, ct);
            await using var connection = new SqlConnection(cs);
            var current = await LoadBucketAsync(connection, currentYm, filter, ct);
            var previous = await LoadBucketAsync(connection, previousYm, filter, ct);
            return new BranchMonthStatsDto
            {
                CurrentYearMonth = currentYm,
                PreviousYearMonth = previousYm,
                Current = current,
                Previous = previous,
                Scope = normalized,
                Id = id
            };
        }

        private async Task<MonthScopeFilter> ResolveFilterAsync(string scope, int id, CancellationToken ct)
        {
            switch (scope)
            {
                case "list":
                    if (id <= 0)
                    {
                        return MonthScopeFilter.Empty;
                    }

                    return MonthScopeFilter.ByLists([id]);

                case "follower":
                    if (id <= 0)
                    {
                        return MonthScopeFilter.Empty;
                    }

                    await _followerLists.EnsureSchemaAsync(ct);
                    var listIds = (await _followerLists.GetAssignedListIdsAsync(id, ct)).ToList();
                    return listIds.Count == 0
                        ? MonthScopeFilter.Empty
                        : MonthScopeFilter.ByLists(listIds);

                case "employee":
                    if (id <= 0)
                    {
                        return MonthScopeFilter.Empty;
                    }

                    var customerIds = await LoadEmployeeCustomerIdsAsync(id, ct);
                    return customerIds.Count == 0
                        ? MonthScopeFilter.Empty
                        : MonthScopeFilter.ByCustomers(customerIds);

                default:
                    return MonthScopeFilter.All;
            }
        }

        private async Task<IReadOnlyList<int>> LoadEmployeeCustomerIdsAsync(int employeeId, CancellationToken ct)
        {
            var cs = _guard.GetSalesConnectionString();
            if (string.IsNullOrWhiteSpace(cs) || employeeId <= 0)
            {
                return [];
            }

            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            var ids = new HashSet<int>();

            var draftsExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (draftsExists == 1)
            {
                var fromDrafts = await connection.QueryAsync<int>(new CommandDefinition(@"
SELECT DISTINCT CustomerId
FROM dbo.SalesDrafts
WHERE EmployeeId = @EmployeeId
  AND Status = N'Completed'
  AND CustomerId IS NOT NULL
  AND CustomerId > 0;",
                    new { EmployeeId = employeeId }, cancellationToken: ct));
                foreach (var cid in fromDrafts)
                {
                    ids.Add(cid);
                }
            }

            var requestsExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesRequests', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (requestsExists == 1)
            {
                var fromRequests = await connection.QueryAsync<int>(new CommandDefinition(@"
SELECT DISTINCT ExistingCustomerId
FROM dbo.SalesRequests
WHERE TargetEmployeeId = @EmployeeId
  AND Status = N'Completed'
  AND ExistingCustomerId IS NOT NULL
  AND ExistingCustomerId > 0;",
                    new { EmployeeId = employeeId }, cancellationToken: ct));
                foreach (var cid in fromRequests)
                {
                    ids.Add(cid);
                }
            }

            return ids.ToList();
        }

        private static async Task<BranchMonthBucketDto> LoadBucketAsync(
            SqlConnection connection,
            int yearMonth,
            MonthScopeFilter filter,
            CancellationToken ct)
        {
            if (filter.IsEmpty)
            {
                return new BranchMonthBucketDto { YearMonth = yearMonth };
            }

            var (monthStart, monthEnd) = MonthBounds(yearMonth);
            var neu = await LoadNewMonthAsync(connection, monthStart, monthEnd, filter, ct);
            var zeroed = await LoadZeroedMonthAsync(connection, monthStart, monthEnd, filter, ct);
            return new BranchMonthBucketDto
            {
                YearMonth = yearMonth,
                NewCustomersCount = neu.Count,
                NewCustomersSaleSum = neu.SaleSum,
                NewCustomersDaySalesSum = neu.DaySum,
                ZeroedCustomersCount = zeroed.Count,
                ZeroedCustomersSaleSum = zeroed.SaleSum,
                ZeroedCustomersDaySalesSum = zeroed.DaySum,
                NetCustomersCount = neu.Count - zeroed.Count,
                NetSaleSum = neu.SaleSum - zeroed.SaleSum,
                NetDaySalesSum = neu.DaySum - zeroed.DaySum
            };
        }

        private static string ScopeWhereClause(MonthScopeFilter filter) =>
            filter.Mode switch
            {
                MonthFilterMode.Lists => " AND c.DelegateID IN @ListIds",
                MonthFilterMode.Customers => " AND c.CustomerID IN @CustomerIds",
                _ => string.Empty
            };

        private static object ScopeParams(
            DateTime monthStart,
            DateTime monthEnd,
            MonthScopeFilter filter) =>
            filter.Mode switch
            {
                MonthFilterMode.Lists => new
                {
                    MonthStart = monthStart.Date,
                    MonthEnd = monthEnd.Date,
                    ListIds = filter.Ids
                },
                MonthFilterMode.Customers => new
                {
                    MonthStart = monthStart.Date,
                    MonthEnd = monthEnd.Date,
                    CustomerIds = filter.Ids
                },
                _ => new { MonthStart = monthStart.Date, MonthEnd = monthEnd.Date }
            };

        /// <summary>
        /// Same sale-date + open-balance rules as BranchManagerStats PeriodAggregate (incl. draft enrich).
        /// </summary>
        private static async Task<(int Count, decimal SaleSum, decimal DaySum)> LoadNewMonthAsync(
            SqlConnection connection,
            DateTime monthStart,
            DateTime monthEnd,
            MonthScopeFilter filter,
            CancellationToken ct)
        {
            var draftsExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));

            var scopeWhere = ScopeWhereClause(filter);
            var sql = draftsExists == 1
                ? $"""
;WITH Sale AS (
    SELECT CustomerID,
           MAX(DateCreate) AS DateSaleDevice,
           ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), -3) AS AmountTotalSales
    FROM dbo.View_CustomersSalesDelegate
    GROUP BY CustomerID
),
Pay AS (
    SELECT CustomerID,
           ROUND(ISNULL(SUM(AmountDenar), 0), -3) AS ReceiptsTotal
    FROM dbo.View_CustomersPaymentsDelegate
    GROUP BY CustomerID
),
Draft AS (
    SELECT CustomerId AS CustomerID,
           MAX(COALESCE(CompletedAt, CreatedAt)) AS SaleDate,
           CAST(SUM(FinalSalePrice) AS float) AS AmountTotal,
           CAST(SUM(CASE WHEN DownPayment > 0 THEN DownPayment ELSE 0 END) AS float) AS DownPayment
    FROM dbo.SalesDrafts
    WHERE CustomerId IS NOT NULL AND CustomerId > 0
      AND Status IN (N'Completed', N'DocumentsReady', N'DocumentsPending')
      AND FinalSalePrice > 0
    GROUP BY CustomerId
),
Facts AS (
    SELECT c.CustomerID,
           COALESCE(s.DateSaleDevice, d.SaleDate) AS DateSaleDevice,
           CASE
               WHEN ISNULL(s.AmountTotalSales, 0) > 0 THEN ISNULL(s.AmountTotalSales, 0)
               ELSE ISNULL(d.AmountTotal, 0)
           END AS AmountTotalSales,
           CASE
               WHEN ISNULL(s.AmountTotalSales, 0) > 0
                   THEN ROUND(ISNULL(s.AmountTotalSales, 0) - ISNULL(p.ReceiptsTotal, 0), -3)
               ELSE ROUND(ISNULL(d.AmountTotal, 0) - ISNULL(d.DownPayment, 0), -3)
           END AS AmountRemaining
    FROM dbo.Customers c
    LEFT JOIN Sale s ON s.CustomerID = c.CustomerID
    LEFT JOIN Pay p ON p.CustomerID = c.CustomerID
    LEFT JOIN Draft d ON d.CustomerID = c.CustomerID
    WHERE 1 = 1{scopeWhere}
)
SELECT
    COUNT(1) AS Count,
    CAST(ISNULL(SUM(ISNULL(f.AmountTotalSales, 0)), 0) AS DECIMAL(18,2)) AS SaleSum,
    CAST(ISNULL(SUM(ISNULL(vc.AmountDaySales, 0)), 0) AS DECIMAL(18,2)) AS DaySum
FROM Facts f
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = f.CustomerID
WHERE f.AmountRemaining > 0
  AND f.DateSaleDevice IS NOT NULL
  AND CONVERT(date, f.DateSaleDevice) >= @MonthStart
  AND CONVERT(date, f.DateSaleDevice) < @MonthEnd;
"""
                : $"""
;WITH Sale AS (
    SELECT CustomerID,
           MAX(DateCreate) AS DateSaleDevice,
           ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), -3) AS AmountTotalSales
    FROM dbo.View_CustomersSalesDelegate
    GROUP BY CustomerID
),
Pay AS (
    SELECT CustomerID,
           ROUND(ISNULL(SUM(AmountDenar), 0), -3) AS ReceiptsTotal
    FROM dbo.View_CustomersPaymentsDelegate
    GROUP BY CustomerID
),
Facts AS (
    SELECT c.CustomerID,
           s.DateSaleDevice,
           ISNULL(s.AmountTotalSales, 0) AS AmountTotalSales,
           ROUND(ISNULL(s.AmountTotalSales, 0) - ISNULL(p.ReceiptsTotal, 0), -3) AS AmountRemaining
    FROM dbo.Customers c
    LEFT JOIN Sale s ON s.CustomerID = c.CustomerID
    LEFT JOIN Pay p ON p.CustomerID = c.CustomerID
    WHERE 1 = 1{scopeWhere}
)
SELECT
    COUNT(1) AS Count,
    CAST(ISNULL(SUM(ISNULL(f.AmountTotalSales, 0)), 0) AS DECIMAL(18,2)) AS SaleSum,
    CAST(ISNULL(SUM(ISNULL(vc.AmountDaySales, 0)), 0) AS DECIMAL(18,2)) AS DaySum
FROM Facts f
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = f.CustomerID
WHERE f.AmountRemaining > 0
  AND f.DateSaleDevice IS NOT NULL
  AND CONVERT(date, f.DateSaleDevice) >= @MonthStart
  AND CONVERT(date, f.DateSaleDevice) < @MonthEnd;
""";

            return await connection.QueryFirstAsync<(int Count, decimal SaleSum, decimal DaySum)>(
                new CommandDefinition(
                    sql,
                    ScopeParams(monthStart, monthEnd, filter),
                    cancellationToken: ct));
        }

        private static async Task<(int Count, decimal SaleSum, decimal DaySum)> LoadZeroedMonthAsync(
            SqlConnection connection,
            DateTime monthStart,
            DateTime monthEnd,
            MonthScopeFilter filter,
            CancellationToken ct)
        {
            var scopeWhere = ScopeWhereClause(filter);
            return await connection.QueryFirstAsync<(int Count, decimal SaleSum, decimal DaySum)>(
                new CommandDefinition($@"
SELECT
    COUNT(1) AS Count,
    CAST(ISNULL(SUM(ISNULL(vc.AmountTotalSales, 0)), 0) AS DECIMAL(18,2)) AS SaleSum,
    CAST(ISNULL(SUM(ISNULL(vc.AmountDaySales, 0)), 0) AS DECIMAL(18,2)) AS DaySum
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE ISNULL(vc.AmountRemaining, 0) = 0
  AND vc.LastPaymentDate IS NOT NULL
  AND CONVERT(date, vc.LastPaymentDate) >= @MonthStart
  AND CONVERT(date, vc.LastPaymentDate) < @MonthEnd
{scopeWhere};",
                    ScopeParams(monthStart, monthEnd, filter),
                    cancellationToken: ct));
        }

        public static int ToYearMonth(DateTime date) => date.Year * 100 + date.Month;

        public static int PreviousYearMonth(int yearMonth)
        {
            var y = yearMonth / 100;
            var m = yearMonth % 100;
            if (m <= 1)
            {
                return (y - 1) * 100 + 12;
            }

            return y * 100 + (m - 1);
        }

        /// <summary>Decline threshold used by BM rating alerts (≥ 0.3 over ~7 days).</summary>
        public const double DeclineAlertThreshold = 0.3;

        private static (DateTime MonthStart, DateTime MonthEndExclusive) MonthBounds(int yearMonth)
        {
            var y = yearMonth / 100;
            var m = yearMonth % 100;
            var start = new DateTime(y, m, 1);
            return (start, start.AddMonths(1));
        }

        private enum MonthFilterMode
        {
            All,
            Lists,
            Customers,
            Empty
        }

        private sealed class MonthScopeFilter
        {
            public MonthFilterMode Mode { get; init; }
            public IReadOnlyList<int> Ids { get; init; } = [];

            public bool IsEmpty => Mode == MonthFilterMode.Empty;

            public static MonthScopeFilter All { get; } = new() { Mode = MonthFilterMode.All };

            public static MonthScopeFilter Empty { get; } = new() { Mode = MonthFilterMode.Empty };

            public static MonthScopeFilter ByLists(IReadOnlyList<int> listIds) =>
                new() { Mode = MonthFilterMode.Lists, Ids = listIds };

            public static MonthScopeFilter ByCustomers(IReadOnlyList<int> customerIds) =>
                new() { Mode = MonthFilterMode.Customers, Ids = customerIds };
        }
    }
}
