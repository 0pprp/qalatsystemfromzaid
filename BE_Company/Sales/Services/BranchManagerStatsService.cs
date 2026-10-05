using BE_Company.IRepository;
using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class BranchManagerTrendPointDto
    {
        public string Date { get; set; } = "";
        public double Value { get; set; }
    }

    public sealed class BranchManagerEntityOptionDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        /// <summary>For followers: DelegateIDs (lists) linked via FollowerUserLists.</summary>
        public IReadOnlyList<int> AssignedListIds { get; set; } = [];
    }

    public sealed class BranchManagerTrendDto
    {
        public string Scope { get; set; } = "";
        public int Id { get; set; }
        public string? Name { get; set; }
        public string MetricLabel { get; set; } = "";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public IReadOnlyList<BranchManagerTrendPointDto> Points { get; set; } = [];
        public double StartValue { get; set; }
        public double EndValue { get; set; }
        public double ChangeAbsolute { get; set; }
        public double ChangePercent { get; set; }
        public string Direction { get; set; } = "flat"; // up | down | flat
        /// <summary>
        /// For list/follower/branch: count of rating customers (paid within last 30 days).
        /// For employee: period sale-date cohort (legacy).
        /// </summary>
        public int? CustomerCount { get; set; }
        public string? RatingLabel { get; set; }
        public int? ExcellentCount { get; set; }
        public int? GoodCount { get; set; }
        public int? WeakCount { get; set; }
        public int? RejectedCount { get; set; }
        public int? LegalCount { get; set; }
        /// <summary>سيء count (200+ day band).</summary>
        public int? BadCount { get; set; }
        /// <summary>Deprecated: period always applies (sale date in [from,to]). Kept for older Flutter clients.</summary>
        public bool PeriodIgnored { get; set; }

        /// <summary>Sale-date period cohort — تقييم الفترة (chart end), independent of active rating.</summary>
        public int? PeriodCustomerCount { get; set; }
        public double? PeriodEndValue { get; set; }
        public string? PeriodRatingLabel { get; set; }
        public string? PeriodMetricLabel { get; set; }

        /// <summary>«التقييم الوهمي» — same cohort as EndValue with phantom sale-date math.</summary>
        public string? PhantomMetricLabel { get; set; }
        public double? PhantomEndValue { get; set; }
        public string? PhantomRatingLabel { get; set; }
        public int? PhantomCustomerCount { get; set; }
        public int? PhantomExcellentCount { get; set; }
        public int? PhantomGoodCount { get; set; }
        public int? PhantomWeakCount { get; set; }
        public int? PhantomRejectedCount { get; set; }
        public int? PhantomBadCount { get; set; }
        public double? PhantomPeriodEndValue { get; set; }
        public string? PhantomPeriodRatingLabel { get; set; }
    }

    public sealed class BranchManagerStatsCatalogDto
    {
        public IReadOnlyList<BranchManagerEntityOptionDto> Lists { get; set; } = [];
        public IReadOnlyList<BranchManagerEntityOptionDto> Followers { get; set; } = [];
        public IReadOnlyList<BranchManagerEntityOptionDto> Employees { get; set; } = [];
    }

    public sealed class EmployeeRatingCustomerCardDto
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = "";
        public string? Phone { get; set; }
        public string Level { get; set; } = "";
        public string RatingLabel { get; set; } = "";
        public int Score { get; set; }
        public DateTime? DateSale { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public double AmountPaid { get; set; }
        public double AmountRemaining { get; set; }
        public double AmountTotal { get; set; }
    }

    public interface IBranchManagerStatsService
    {
        Task<BranchManagerStatsCatalogDto> GetCatalogAsync(CancellationToken ct);
        Task<BranchManagerTrendDto> GetTrendAsync(
            string scope,
            int id,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct,
            bool fullHistory = false);

        /// <summary>Fill LatestRating / RatingLabel on employee rows (all-time snapshot).</summary>
        Task AttachEmployeeLatestRatingsAsync(
            IReadOnlyList<BE_Company.Sales.DTO.SalesManagerEmployeeDTO> rows,
            CancellationToken ct);

        /// <summary>Daily points from first sale date through today (no short period picker).</summary>
        Task<BranchManagerTrendDto> GetEmployeeLifetimeTrendAsync(int employeeId, CancellationToken ct);

        /// <summary>Customers under employee filtered by rating level (ممتاز/جيد/…).</summary>
        Task<IReadOnlyList<EmployeeRatingCustomerCardDto>> GetEmployeeRatingCustomersAsync(
            int employeeId,
            string? level,
            CancellationToken ct);

        /// <summary>Fill LatestRating / RatingLabel on follower rows (all-time snapshot across assigned lists).</summary>
        Task AttachFollowerLatestRatingsAsync(
            IReadOnlyList<BE_Company.Sales.DTO.SalesManagerFollowerDTO> rows,
            CancellationToken ct);

        /// <summary>Daily points from first sale date through today for a follower's assigned lists.</summary>
        Task<BranchManagerTrendDto> GetFollowerLifetimeTrendAsync(int followerUserId, CancellationToken ct);

        /// <summary>Customers under a follower's assigned lists filtered by rating level (ممتاز/جيد/…).</summary>
        Task<IReadOnlyList<EmployeeRatingCustomerCardDto>> GetFollowerRatingCustomersAsync(
            int followerUserId,
            string? level,
            CancellationToken ct);

        /// <summary>
        /// Customers for BM/SM/director تقييم drill-down (branch/list/follower/employee),
        /// filtered by sale-date period and rating level. Caps page size for ~100k production.
        /// </summary>
        Task<IReadOnlyList<EmployeeRatingCustomerCardDto>> GetRatingCustomersAsync(
            string scope,
            int id,
            string? level,
            DateTime? fromDate,
            DateTime? toDate,
            CancellationToken ct,
            int take = 500,
            int skip = 0,
            string? search = null);
    }

    /// <summary>
    /// Charts (points): customers whose DateSaleDevice is in [from,to] — تقييم الفترة.
    /// Headline EndValue / CustomerCount for list|follower|branch: active customers only
    /// Headline EndValue / CustomerCount for list|follower|branch: customers with payment
    /// in the last 30 days (exclude unpaid for a month), independent of period picker.
    /// </summary>
    public sealed class BranchManagerStatsService : IBranchManagerStatsService
    {
        private const string MetricLabel = "متوسط نقاط زبائن تاريخ البيع — التقييم حتى اليوم";
        private const string ActiveMetricLabel =
            "تقييم الزبائن المسددين (آخر تسديد خلال 30 يوماً) — مستقل عن الفترة";
        private const string PeriodMetricLabel =
            "تقييم الفترة — متوسط نقاط زبائن تاريخ البيع ضمن الفترة";
        private readonly SalesDevelopmentGuard _guard;
        private readonly IDelegatesRepository _delegates;
        private readonly IRatingDataSource _rating;
        private readonly IFollowerUserListsRepository _followerLists;

        public BranchManagerStatsService(
            SalesDevelopmentGuard guard,
            IDelegatesRepository delegates,
            IRatingDataSource rating,
            IFollowerUserListsRepository followerLists)
        {
            _guard = guard;
            _delegates = delegates;
            _rating = rating;
            _followerLists = followerLists;
        }

        public async Task<BranchManagerStatsCatalogDto> GetCatalogAsync(CancellationToken ct)
        {
            var active = await LoadActiveListsAsync(ct);
            var followers = await QueryFollowerOptionsAsync(ct);
            followers = await AttachFollowerAssignedListsAsync(followers, ct);
            var employees = await QueryEmployeeOptionsAsync(ct);
            return new BranchManagerStatsCatalogDto
            {
                Lists = active,
                Followers = followers,
                Employees = employees
            };
        }

        public async Task<BranchManagerTrendDto> GetTrendAsync(
            string scope,
            int id,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken ct,
            bool fullHistory = false)
        {
            fromDate = fromDate.Date;
            toDate = toDate.Date;
            if (toDate < fromDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            scope = (scope ?? "branch").Trim().ToLowerInvariant();
            if (fullHistory && (scope is "employee" or "salesemployee"))
            {
                return await GetEmployeeLifetimeTrendAsync(id, ct);
            }

            var today = DateTime.UtcNow.AddHours(3).Date;
            const int maxTrendDays = 366;
            // Cap sale-window length (who is in the cohort) — independent of chart length.
            if ((toDate - fromDate).TotalDays > maxTrendDays - 1)
            {
                fromDate = toDate.AddDays(-(maxTrendDays - 1));
            }

            // Chart/score runs from sale-from through today (payments keep updating after sale month).
            var chartTo = today < fromDate ? fromDate : today;
            var chartFrom = fromDate;
            if ((chartTo - chartFrom).TotalDays > maxTrendDays - 1)
            {
                chartFrom = chartTo.AddDays(-(maxTrendDays - 1));
            }

            return scope switch
            {
                "list" or "delegate" => await ListTrendAsync(id, fromDate, toDate, chartFrom, chartTo, ct),
                "follower" => await FollowerTrendAsync(id, fromDate, toDate, chartFrom, chartTo, ct),
                "employee" or "salesemployee" => await EmployeeTrendAsync(id, fromDate, toDate, chartFrom, chartTo, ct),
                _ => await BranchTrendAsync(fromDate, toDate, chartFrom, chartTo, ct)
            };
        }

        public async Task AttachEmployeeLatestRatingsAsync(
            IReadOnlyList<BE_Company.Sales.DTO.SalesManagerEmployeeDTO> rows,
            CancellationToken ct)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var asOf = DateTime.UtcNow.AddHours(3).Date;
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                if (row.EmployeeId <= 0)
                {
                    continue;
                }

                try
                {
                    var customerIds = await LoadEmployeeCustomerIdsAsync(row.EmployeeId, ct);
                    var facts = customerIds.Count == 0
                        ? Array.Empty<CustomerRatingFacts>()
                        : await _rating.GetFactsByCustomerIdsAsync(customerIds, ct);
                    var snap = EmployeeSalesRating.Snapshot(facts, asOf, usePhantomSaleDate: false);
                    var phantom = EmployeeSalesRating.Snapshot(facts, asOf, usePhantomSaleDate: true);
                    row.LatestRating = snap.AverageScore;
                    row.RatingLabel = snap.RatingLabel;
                    row.RatedCustomerCount = snap.CustomerCount;
                    row.PhantomLatestRating = phantom.AverageScore;
                    row.PhantomRatingLabel = phantom.RatingLabel;
                }
                catch
                {
                    // leave nulls — UI shows —
                }
            }
        }

        public async Task<BranchManagerTrendDto> GetEmployeeLifetimeTrendAsync(
            int employeeId,
            CancellationToken ct)
        {
            var today = DateTime.UtcNow.AddHours(3).Date;
            var name = await ResolveEmployeeNameAsync(employeeId, ct) ?? "موظف مبيعات";
            const string metric = "متوسط نقاط الزبائن من أول بيع";
            if (employeeId <= 0)
            {
                return BuildTrend("employee", employeeId, name, metric, today, today,
                    EmptySeries(today, today), EmptyAggregate());
            }

            try
            {
                var customerIds = await LoadEmployeeCustomerIdsAsync(employeeId, ct);
                IReadOnlyList<CustomerRatingFacts> facts = customerIds.Count == 0
                    ? Array.Empty<CustomerRatingFacts>()
                    : await _rating.GetFactsByCustomerIdsAsync(customerIds, ct);

                // Same cohort as the table column: every completed-sale customer under the employee.
                var endSnap = EmployeeSalesRating.Snapshot(facts, today, usePhantomSaleDate: false);
                var dated = facts.Where(f => f.DateSaleDevice is not null).ToList();
                var undated = facts.Where(f => f.DateSaleDevice is null).ToList();

                if (dated.Count == 0 && undated.Count == 0)
                {
                    return BuildTrend("employee", employeeId, name, metric, today, today,
                        EmptySeries(today, today), EmptyAggregate());
                }

                var fromDate = dated.Count > 0
                    ? dated.Min(f => f.DateSaleDevice!.Value.Date)
                    : today;
                var toDate = today;
                const int hardMaxDays = 3650;
                if ((toDate - fromDate).TotalDays > hardMaxDays - 1)
                {
                    fromDate = toDate.AddDays(-(hardMaxDays - 1));
                }

                // Daily series: sales dated up to each day + undated customers (counted from day one)
                // so the last point matches the table's all-customer average.
                var series = EmployeeDailyScoresIncludingUndated(dated, undated, fromDate, toDate);
                var dto = BuildTrend("employee", employeeId, name, PhantomSaleDate.RealMetricLabel, fromDate, toDate, series,
                    ToListRatingResult(endSnap));
                // Force end metrics from full Snapshot so table ↔ modal never diverge.
                dto.EndValue = endSnap.AverageScore;
                dto.CustomerCount = endSnap.CustomerCount;
                dto.RatingLabel = endSnap.RatingLabel;
                dto.ExcellentCount = endSnap.ExcellentCount;
                dto.GoodCount = endSnap.GoodCount;
                dto.WeakCount = endSnap.WeakCount;
                dto.RejectedCount = endSnap.RejectedCount;
                dto.LegalCount = endSnap.LegalCount;
                dto.BadCount = endSnap.BadCount;
                AttachPhantomHeadline(dto, facts, today);
                if (dto.Points.Count > 0)
                {
                    var last = dto.Points[^1];
                    last.Value = endSnap.AverageScore;
                }

                return dto;
            }
            catch
            {
                return BuildTrend("employee", employeeId, name, metric, today, today,
                    EmptySeries(today, today), EmptyAggregate());
            }
        }

        public async Task<IReadOnlyList<EmployeeRatingCustomerCardDto>> GetEmployeeRatingCustomersAsync(
            int employeeId,
            string? level,
            CancellationToken ct)
        {
            if (employeeId <= 0)
            {
                return [];
            }

            var asOf = DateTime.UtcNow.AddHours(3).Date;
            var wanted = ParseRatingLevel(level);
            var customerIds = await LoadEmployeeCustomerIdsAsync(employeeId, ct);
            if (customerIds.Count == 0)
            {
                return [];
            }

            var facts = await _rating.GetFactsByCustomerIdsAsync(customerIds, ct);
            facts = ListFollowerRatingCohort.OpenBalanceOnly(facts);
            var matched = new List<(CustomerRatingFacts Fact, CustomerRatingResult Rated)>();
            foreach (var fact in facts)
            {
                var rated = CustomerRatingCalculator.Evaluate(fact, asOf);
                if (rated.IsSettled || !ListFollowerRatingCohort.HasOpenBalance(fact))
                {
                    continue;
                }

                if (wanted is null || rated.Level == wanted)
                {
                    matched.Add((fact, rated));
                }
            }

            if (matched.Count == 0)
            {
                return [];
            }

            var names = await LoadCustomerNamesAsync(matched.Select(m => m.Fact.CustomerId).ToList(), ct);
            return matched
                .Select(m =>
                {
                    names.TryGetValue(m.Fact.CustomerId, out var info);
                    return new EmployeeRatingCustomerCardDto
                    {
                        CustomerId = m.Fact.CustomerId,
                        CustomerName = string.IsNullOrWhiteSpace(info.Name) ? $"زبون {m.Fact.CustomerId}" : info.Name!,
                        Phone = info.Phone,
                        Level = m.Rated.Level.ToString().ToLowerInvariant(),
                        RatingLabel = m.Rated.RatingLabel,
                        Score = m.Rated.Score,
                        DateSale = m.Fact.DateSaleDevice,
                        LastPaymentDate = m.Fact.LastPaymentDate,
                        AmountPaid = m.Fact.ReceiptsTotal,
                        AmountRemaining = m.Fact.AmountRemaining,
                        AmountTotal = m.Fact.AmountTotalSales
                    };
                })
                .OrderBy(c => c.CustomerName)
                .ToList();
        }

        public async Task AttachFollowerLatestRatingsAsync(
            IReadOnlyList<BE_Company.Sales.DTO.SalesManagerFollowerDTO> rows,
            CancellationToken ct)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var asOf = DateTime.UtcNow.AddHours(3).Date;
            await _followerLists.EnsureSchemaAsync(ct);
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                if (row.FollowerId <= 0)
                {
                    continue;
                }

                try
                {
                    var listIds = (await _followerLists.GetAssignedListIdsAsync(row.FollowerId, ct)).ToList();
                    if (listIds.Count == 0)
                    {
                        continue;
                    }

                    var facts = ListFollowerRatingCohort.OpenBalanceOnly(
                        await _rating.GetFactsByListIdsAsync(listIds, ct));
                    var active = facts
                        .Where(f => ActiveCustomerRules.IsActive(f.LastPaymentDate, asOf))
                        .ToList();
                    var snap = EmployeeSalesRating.Snapshot(active, asOf, usePhantomSaleDate: false);
                    var phantom = EmployeeSalesRating.Snapshot(active, asOf, usePhantomSaleDate: true);
                    row.LatestRating = snap.AverageScore;
                    row.RatingLabel = snap.RatingLabel;
                    row.RatedCustomerCount = snap.CustomerCount;
                    row.PhantomLatestRating = phantom.AverageScore;
                    row.PhantomRatingLabel = phantom.RatingLabel;
                }
                catch
                {
                    // leave nulls — UI shows —
                }
            }
        }

        /// <summary>
        /// Mirrors <see cref="GetEmployeeLifetimeTrendAsync"/> but the customer pool comes from the
        /// follower's assigned lists (GetFactsByListIdsAsync), not sales-draft/request ownership.
        /// </summary>
        public async Task<BranchManagerTrendDto> GetFollowerLifetimeTrendAsync(
            int followerUserId,
            CancellationToken ct)
        {
            var today = DateTime.UtcNow.AddHours(3).Date;
            var name = await ResolveFollowerNameAsync(followerUserId, ct) ?? "متابع";
            const string metric = "متوسط نقاط الزبائن من أول بيع";
            if (followerUserId <= 0)
            {
                return BuildTrend("follower", followerUserId, name, metric, today, today,
                    EmptySeries(today, today), EmptyAggregate());
            }

            try
            {
                await _followerLists.EnsureSchemaAsync(ct);
                var listIds = (await _followerLists.GetAssignedListIdsAsync(followerUserId, ct)).ToList();
                IReadOnlyList<CustomerRatingFacts> facts = listIds.Count == 0
                    ? Array.Empty<CustomerRatingFacts>()
                    : ListFollowerRatingCohort.OpenBalanceOnly(
                        await _rating.GetFactsByListIdsAsync(listIds, ct));

                // Same cohort as the table column: open-balance customers under the follower's lists.
                var endSnap = EmployeeSalesRating.Snapshot(facts, today, usePhantomSaleDate: false);
                var dated = facts.Where(f => f.DateSaleDevice is not null).ToList();
                var undated = facts.Where(f => f.DateSaleDevice is null).ToList();

                if (dated.Count == 0 && undated.Count == 0)
                {
                    return BuildTrend("follower", followerUserId, name, metric, today, today,
                        EmptySeries(today, today), EmptyAggregate());
                }

                var fromDate = dated.Count > 0
                    ? dated.Min(f => f.DateSaleDevice!.Value.Date)
                    : today;
                var toDate = today;
                const int hardMaxDays = 3650;
                if ((toDate - fromDate).TotalDays > hardMaxDays - 1)
                {
                    fromDate = toDate.AddDays(-(hardMaxDays - 1));
                }

                var series = EmployeeDailyScoresIncludingUndated(dated, undated, fromDate, toDate);
                var dto = BuildTrend("follower", followerUserId, name, PhantomSaleDate.RealMetricLabel, fromDate, toDate, series,
                    ToListRatingResult(endSnap));
                // Force end metrics from full Snapshot so table ↔ modal never diverge.
                dto.EndValue = endSnap.AverageScore;
                dto.CustomerCount = endSnap.CustomerCount;
                dto.RatingLabel = endSnap.RatingLabel;
                dto.ExcellentCount = endSnap.ExcellentCount;
                dto.GoodCount = endSnap.GoodCount;
                dto.WeakCount = endSnap.WeakCount;
                dto.RejectedCount = endSnap.RejectedCount;
                dto.LegalCount = endSnap.LegalCount;
                dto.BadCount = endSnap.BadCount;
                AttachPhantomHeadline(dto, facts, today);
                if (dto.Points.Count > 0)
                {
                    var last = dto.Points[^1];
                    last.Value = endSnap.AverageScore;
                }

                return dto;
            }
            catch
            {
                return BuildTrend("follower", followerUserId, name, metric, today, today,
                    EmptySeries(today, today), EmptyAggregate());
            }
        }

        public async Task<IReadOnlyList<EmployeeRatingCustomerCardDto>> GetFollowerRatingCustomersAsync(
            int followerUserId,
            string? level,
            CancellationToken ct)
        {
            if (followerUserId <= 0)
            {
                return [];
            }

            var asOf = DateTime.UtcNow.AddHours(3).Date;
            var wanted = ParseRatingLevel(level);
            await _followerLists.EnsureSchemaAsync(ct);
            var listIds = (await _followerLists.GetAssignedListIdsAsync(followerUserId, ct)).ToList();
            if (listIds.Count == 0)
            {
                return [];
            }

            var facts = ListFollowerRatingCohort.OpenBalanceOnly(
                await _rating.GetFactsByListIdsAsync(listIds, ct));
            var matched = new List<(CustomerRatingFacts Fact, CustomerRatingResult Rated)>();
            foreach (var fact in facts)
            {
                var rated = CustomerRatingCalculator.Evaluate(fact, asOf);
                if (rated.IsSettled || !ListFollowerRatingCohort.HasOpenBalance(fact))
                {
                    continue;
                }

                if (wanted is null || rated.Level == wanted)
                {
                    matched.Add((fact, rated));
                }
            }

            if (matched.Count == 0)
            {
                return [];
            }

            var names = await LoadCustomerNamesAsync(matched.Select(m => m.Fact.CustomerId).ToList(), ct);
            return matched
                .Select(m =>
                {
                    names.TryGetValue(m.Fact.CustomerId, out var info);
                    return new EmployeeRatingCustomerCardDto
                    {
                        CustomerId = m.Fact.CustomerId,
                        CustomerName = string.IsNullOrWhiteSpace(info.Name) ? $"زبون {m.Fact.CustomerId}" : info.Name!,
                        Phone = info.Phone,
                        Level = m.Rated.Level.ToString().ToLowerInvariant(),
                        RatingLabel = m.Rated.RatingLabel,
                        Score = m.Rated.Score,
                        DateSale = m.Fact.DateSaleDevice,
                        LastPaymentDate = m.Fact.LastPaymentDate,
                        AmountPaid = m.Fact.ReceiptsTotal,
                        AmountRemaining = m.Fact.AmountRemaining,
                        AmountTotal = m.Fact.AmountTotalSales
                    };
                })
                .OrderBy(c => c.CustomerName)
                .ToList();
        }

        public async Task<IReadOnlyList<EmployeeRatingCustomerCardDto>> GetRatingCustomersAsync(
            string scope,
            int id,
            string? level,
            DateTime? fromDate,
            DateTime? toDate,
            CancellationToken ct,
            int take = 500,
            int skip = 0,
            string? search = null)
        {
            const int maxTake = 1_000;
            const int nameResolveCap = 2_000;
            take = Math.Clamp(take <= 0 ? 500 : take, 1, maxTake);
            skip = Math.Max(0, skip);

            scope = (scope ?? "branch").Trim().ToLowerInvariant();
            var today = DateTime.UtcNow.AddHours(3).Date;
            var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (toDate ?? today).Date;
            if (to < from)
            {
                (from, to) = (to, from);
            }

            // Match trend end score: sale window = [from,to], levels evaluated as of today.
            var asOf = today;

            var wanted = ParseRatingLevel(level);
            IReadOnlyList<CustomerRatingFacts> facts;
            try
            {
                facts = scope switch
                {
                    "list" or "delegate" => await LoadListFactsAsync(id, ct),
                    "follower" => await LoadFollowerFactsAsync(id, ct),
                    "employee" or "salesemployee" => await LoadEmployeeFactsAsync(id, ct),
                    _ => await LoadBranchFactsAsync(ct)
                };
            }
            catch
            {
                return [];
            }

            // مصفرون (الباقي ≤ 0 بعد تقريب الآلاف): لا يدخلون قائمة التقييم عند مدير الفرع.
            facts = ListFollowerRatingCohort.OpenBalanceOnly(facts);

            // list/follower/branch: same ActiveRatingCohort as headline ExcellentCount/…
            // Employee keeps sale-date period cohort (unchanged).
            List<(CustomerRatingFacts Fact, CustomerRatingResult Rated)> matched;
            var useActive = scope is "list" or "delegate" or "follower" or "branch";
            if (useActive)
            {
                matched = ActiveRatingCohort.Build(facts, asOf)
                    .Where(m => wanted is null || m.Rated.Level == wanted)
                    .ToList();
            }
            else
            {
                // Match PeriodAggregate: only customers whose sale date falls in the window.
                var cohort = facts
                    .Where(f => f.DateSaleDevice is DateTime sale
                        && sale.Date >= from
                        && sale.Date <= to)
                    .ToList();
                if (cohort.Count == 0)
                {
                    return [];
                }

                var rated = CustomerRatingCalculator.EvaluateMany(cohort, asOf);
                matched = new List<(CustomerRatingFacts Fact, CustomerRatingResult Rated)>(cohort.Count);
                for (var i = 0; i < cohort.Count; i++)
                {
                    if (rated[i].IsSettled || !ListFollowerRatingCohort.HasOpenBalance(cohort[i]))
                    {
                        continue;
                    }

                    if (wanted is null || rated[i].Level == wanted)
                    {
                        matched.Add((cohort[i], rated[i]));
                    }
                }
            }

            if (matched.Count == 0)
            {
                return [];
            }

            matched.Sort((a, b) => a.Fact.CustomerId.CompareTo(b.Fact.CustomerId));

            var q = (search ?? "").Trim();
            List<(CustomerRatingFacts Fact, CustomerRatingResult Rated)> pageFacts;
            Dictionary<int, (string? Name, string? Phone)> names;

            if (q.Length > 0)
            {
                // Resolve names for a bounded cohort then filter — avoids loading 100k name rows.
                var resolve = matched.Count > nameResolveCap
                    ? matched.GetRange(0, nameResolveCap)
                    : matched;
                names = await LoadCustomerNamesAsync(resolve.Select(m => m.Fact.CustomerId).ToList(), ct);
                var filtered = resolve
                    .Where(m =>
                    {
                        names.TryGetValue(m.Fact.CustomerId, out var info);
                        var name = info.Name ?? "";
                        var phone = info.Phone ?? "";
                        return name.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || phone.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || m.Fact.CustomerId.ToString().Contains(q, StringComparison.Ordinal);
                    })
                    .OrderBy(m =>
                    {
                        names.TryGetValue(m.Fact.CustomerId, out var info);
                        return info.Name ?? $"زبون {m.Fact.CustomerId}";
                    }, StringComparer.CurrentCultureIgnoreCase)
                    .Skip(skip)
                    .Take(take)
                    .ToList();
                pageFacts = filtered;
            }
            else
            {
                pageFacts = matched.Skip(skip).Take(take).ToList();
                names = await LoadCustomerNamesAsync(pageFacts.Select(m => m.Fact.CustomerId).ToList(), ct);
            }

            return pageFacts
                .Where(m => ListFollowerRatingCohort.HasOpenBalance(m.Fact))
                .Select(m =>
                {
                    names.TryGetValue(m.Fact.CustomerId, out var info);
                    return new EmployeeRatingCustomerCardDto
                    {
                        CustomerId = m.Fact.CustomerId,
                        CustomerName = string.IsNullOrWhiteSpace(info.Name) ? $"زبون {m.Fact.CustomerId}" : info.Name!,
                        Phone = info.Phone,
                        Level = m.Rated.Level.ToString().ToLowerInvariant(),
                        RatingLabel = m.Rated.RatingLabel,
                        Score = m.Rated.Score,
                        DateSale = m.Fact.DateSaleDevice,
                        LastPaymentDate = m.Fact.LastPaymentDate,
                        AmountPaid = m.Fact.ReceiptsTotal,
                        AmountRemaining = m.Fact.AmountRemaining,
                        AmountTotal = m.Fact.AmountTotalSales
                    };
                })
                .Where(c => c.AmountRemaining > 0 && c.AmountTotal > 0)
                .OrderBy(c => c.CustomerName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private async Task<IReadOnlyList<CustomerRatingFacts>> LoadListFactsAsync(int listId, CancellationToken ct)
        {
            if (listId <= 0)
            {
                return [];
            }

            return ListFollowerRatingCohort.OpenBalanceOnly(
                await _rating.GetFactsByListIdsAsync([listId], ct));
        }

        private async Task<IReadOnlyList<CustomerRatingFacts>> LoadFollowerFactsAsync(
            int followerUserId,
            CancellationToken ct)
        {
            if (followerUserId <= 0)
            {
                return [];
            }

            await _followerLists.EnsureSchemaAsync(ct);
            var listIds = (await _followerLists.GetAssignedListIdsAsync(followerUserId, ct)).ToList();
            if (listIds.Count == 0)
            {
                return [];
            }

            return ListFollowerRatingCohort.OpenBalanceOnly(
                await _rating.GetFactsByListIdsAsync(listIds, ct));
        }

        private async Task<IReadOnlyList<CustomerRatingFacts>> LoadBranchFactsAsync(CancellationToken ct)
        {
            var active = await LoadActiveListsAsync(ct);
            var listIds = active.Select(a => a.Id).ToList();
            if (listIds.Count == 0)
            {
                return [];
            }

            return ListFollowerRatingCohort.OpenBalanceOnly(
                await _rating.GetFactsByListIdsAsync(listIds, ct));
        }

        private async Task<IReadOnlyList<CustomerRatingFacts>> LoadEmployeeFactsAsync(
            int employeeId,
            CancellationToken ct)
        {
            if (employeeId <= 0)
            {
                return [];
            }

            var customerIds = await LoadEmployeeCustomerIdsAsync(employeeId, ct);
            if (customerIds.Count == 0)
            {
                return [];
            }

            return ListFollowerRatingCohort.OpenBalanceOnly(
                await _rating.GetFactsByCustomerIdsAsync(customerIds, ct));
        }

        private static CustomerRatingLevel? ParseRatingLevel(string? level)
        {
            if (string.IsNullOrWhiteSpace(level))
            {
                return null;
            }

            var key = level.Trim().ToLowerInvariant();
            return key switch
            {
                "excellent" or "ممتاز" => CustomerRatingLevel.Excellent,
                "good" or "جيد" => CustomerRatingLevel.Good,
                "weak" or "ضعيف" => CustomerRatingLevel.Weak,
                "rejected" or "مرفوض" or "متلكئ" => CustomerRatingLevel.Rejected,
                "legal" or "قانونية" => CustomerRatingLevel.Legal,
                "bad" or "سيء" => CustomerRatingLevel.Bad,
                _ => Enum.TryParse<CustomerRatingLevel>(level, true, out var parsed) ? parsed : null
            };
        }

        private async Task<Dictionary<int, (string? Name, string? Phone)>> LoadCustomerNamesAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct)
        {
            var map = new Dictionary<int, (string? Name, string? Phone)>();
            if (customerIds.Count == 0)
            {
                return map;
            }

            try
            {
                var cs = _guard.GetSalesConnectionString();
                if (string.IsNullOrWhiteSpace(cs))
                {
                    return map;
                }

                await using var connection = new SqlConnection(cs);
                foreach (var chunk in SqlInClauseBatch.Chunk(customerIds))
                {
                    var rows = await connection.QueryAsync<(int CustomerId, string? Name, string? Phone)>(new CommandDefinition(@"
SELECT c.CustomerID AS CustomerId,
       ISNULL(c.CustomerName, N'') AS Name,
       ISNULL(c.PhoneNumber, N'') AS Phone
FROM dbo.Customers c
WHERE c.CustomerID IN @Ids;",
                        new { Ids = chunk }, cancellationToken: ct));
                    foreach (var row in rows)
                    {
                        map[row.CustomerId] = (row.Name, string.IsNullOrWhiteSpace(row.Phone) ? null : row.Phone);
                    }
                }
            }
            catch
            {
                // Names optional — cards still work with id fallback.
            }

            return map;
        }

        private async Task<EmployeeSalesRatingSnapshot> LoadEmployeeRatingGlanceAsync(
            int employeeId,
            DateTime asOf,
            CancellationToken ct)
        {
            var customerIds = await LoadEmployeeCustomerIdsAsync(employeeId, ct);
            if (customerIds.Count == 0)
            {
                return EmployeeSalesRating.Snapshot([], asOf);
            }

            var facts = await _rating.GetFactsByCustomerIdsAsync(customerIds, ct);
            return EmployeeSalesRating.Snapshot(facts, asOf);
        }

        /// <summary>
        /// Undated sales (no DateSaleDevice) still belong to the employee — include them from the
        /// first chart day so end score / customer count match the all-time Snapshot.
        /// </summary>
        public static IReadOnlyDictionary<DateTime, double> EmployeeDailyScoresIncludingUndated(
            IReadOnlyList<CustomerRatingFacts> dated,
            IReadOnlyList<CustomerRatingFacts> undated,
            DateTime fromDate,
            DateTime toDate)
        {
            fromDate = fromDate.Date;
            toDate = toDate.Date;
            var result = new Dictionary<DateTime, double>();
            for (var d = fromDate; d <= toDate; d = d.AddDays(1))
            {
                var cohort = ListRatingTrendCalculator.FactsSoldInPeriod(dated, fromDate, toDate, d)
                    .Concat(undated)
                    .ToList();
                var agg = ListRatingAggregator.Aggregate(
                    CustomerRatingCalculator.EvaluateMany(cohort, d));
                result[d] = Math.Round(agg.AverageScore, 2, MidpointRounding.AwayFromZero);
            }

            return result;
        }

        private static ListRatingResult ToListRatingResult(EmployeeSalesRatingSnapshot snap) =>
            new(
                snap.CustomerCount,
                snap.ExcellentCount,
                snap.GoodCount,
                snap.WeakCount,
                snap.RejectedCount,
                snap.LegalCount,
                snap.BadCount,
                0,
                snap.AverageScore,
                0,
                ListRiskIndicator.None,
                ListRatingAggregator.ClassifyAverage(snap.AverageScore).Level,
                snap.RatingLabel);

        private async Task<BranchManagerTrendDto> ListTrendAsync(
            int listId,
            DateTime saleFrom,
            DateTime saleTo,
            DateTime chartFrom,
            DateTime chartTo,
            CancellationToken ct)
        {
            var name = await ResolveDelegateNameAsync(listId);
            if (listId <= 0)
            {
                return BuildTrend("list", listId, name, ActiveMetricLabel, chartFrom, chartTo,
                    EmptySeries(chartFrom, chartTo), EmptyAggregate());
            }

            var facts = await _rating.GetFactsByListIdsAsync([listId], ct);
            facts = ListFollowerRatingCohort.OpenBalanceOnly(facts);
            var series = ListRatingTrendCalculator.DailyListScores(
                facts, saleFrom, saleTo, chartTo, chartFrom);
            var periodEnd = ListRatingTrendCalculator.PeriodAggregate(facts, saleFrom, saleTo, chartTo);
            var dto = BuildTrend("list", listId, name, ActiveMetricLabel, chartFrom, chartTo, series, periodEnd);
            await ApplyActiveCustomerHeadlineAsync(dto, facts, chartTo, periodEnd, saleFrom, saleTo, ct);
            return dto;
        }

        private async Task<BranchManagerTrendDto> FollowerTrendAsync(
            int followerUserId,
            DateTime saleFrom,
            DateTime saleTo,
            DateTime chartFrom,
            DateTime chartTo,
            CancellationToken ct)
        {
            var name = await ResolveFollowerNameAsync(followerUserId, ct) ?? "متابع";
            if (followerUserId <= 0)
            {
                return BuildTrend("follower", followerUserId, name, ActiveMetricLabel, chartFrom, chartTo,
                    EmptySeries(chartFrom, chartTo), EmptyAggregate());
            }

            await _followerLists.EnsureSchemaAsync(ct);
            var listIds = (await _followerLists.GetAssignedListIdsAsync(followerUserId, ct)).ToList();
            if (listIds.Count == 0)
            {
                return BuildTrend("follower", followerUserId, name, ActiveMetricLabel, chartFrom, chartTo,
                    EmptySeries(chartFrom, chartTo), EmptyAggregate());
            }

            var (series, periodEnd) = await AverageAcrossListIdsWithAggregateAsync(
                listIds, saleFrom, saleTo, chartFrom, chartTo, ct);
            var dto = BuildTrend("follower", followerUserId, name, ActiveMetricLabel, chartFrom, chartTo, series, periodEnd);
            var facts = await LoadFollowerFactsAsync(followerUserId, ct);
            await ApplyActiveCustomerHeadlineAsync(dto, facts, chartTo, periodEnd, saleFrom, saleTo, ct);
            return dto;
        }

        private async Task<BranchManagerTrendDto> BranchTrendAsync(
            DateTime saleFrom,
            DateTime saleTo,
            DateTime chartFrom,
            DateTime chartTo,
            CancellationToken ct)
        {
            var active = await LoadActiveListsAsync(ct);
            var listIds = active.Select(a => a.Id).ToList();
            if (listIds.Count == 0)
            {
                return BuildTrend("branch", 0, "أداء الفرع", ActiveMetricLabel, chartFrom, chartTo,
                    EmptySeries(chartFrom, chartTo), EmptyAggregate());
            }

            var (series, periodEnd) = await AverageAcrossListIdsWithAggregateAsync(
                listIds, saleFrom, saleTo, chartFrom, chartTo, ct);
            var dto = BuildTrend("branch", 0, "أداء الفرع", ActiveMetricLabel, chartFrom, chartTo, series, periodEnd);
            var facts = await LoadBranchFactsAsync(ct);
            await ApplyActiveCustomerHeadlineAsync(dto, facts, chartTo, periodEnd, saleFrom, saleTo, ct);
            return dto;
        }

        /// <summary>
        /// Headline score = average of active customers only; period metrics kept for تقييم الفترة.
        /// Also fills «التقييم الوهمي» (phantom sale-date math) for managers.
        /// </summary>
        private Task ApplyActiveCustomerHeadlineAsync(
            BranchManagerTrendDto dto,
            IReadOnlyList<CustomerRatingFacts> openFacts,
            DateTime asOf,
            ListRatingResult periodEnd,
            DateTime saleFrom,
            DateTime saleTo,
            CancellationToken ct)
        {
            dto.PeriodCustomerCount = periodEnd.CustomerCount;
            dto.PeriodEndValue = Math.Round(periodEnd.AverageScore, 2, MidpointRounding.AwayFromZero);
            dto.PeriodRatingLabel = periodEnd.FinalRatingLabel;
            dto.PeriodMetricLabel = PeriodMetricLabel;

            // Real headline (التقييم الحقيقي) — same filter as GetRatingCustomersAsync.
            var rated = ActiveRatingCohort.RatedResults(openFacts, asOf, usePhantomSaleDate: false);
            var agg = ListRatingAggregator.Aggregate(rated);

            dto.MetricLabel = PhantomSaleDate.RealMetricLabel;
            dto.EndValue = Math.Round(agg.AverageScore, 2, MidpointRounding.AwayFromZero);
            dto.CustomerCount = agg.CustomerCount;
            dto.RatingLabel = agg.FinalRatingLabel;
            dto.ExcellentCount = agg.ExcellentCount;
            dto.GoodCount = agg.GoodCount;
            dto.WeakCount = agg.WeakCount;
            dto.RejectedCount = agg.RejectedCount;
            dto.LegalCount = agg.LegalCount;
            dto.BadCount = agg.BadCount;
            dto.PeriodIgnored = true;

            // Phantom headline (التقييم الوهمي) — same active cohort, adjusted sale dates for math only.
            var phantomRated = ActiveRatingCohort.RatedResults(openFacts, asOf, usePhantomSaleDate: true);
            var phantomAgg = ListRatingAggregator.Aggregate(phantomRated);
            dto.PhantomMetricLabel = PhantomSaleDate.MetricLabel;
            dto.PhantomEndValue = Math.Round(phantomAgg.AverageScore, 2, MidpointRounding.AwayFromZero);
            dto.PhantomCustomerCount = phantomAgg.CustomerCount;
            dto.PhantomRatingLabel = phantomAgg.FinalRatingLabel;
            dto.PhantomExcellentCount = phantomAgg.ExcellentCount;
            dto.PhantomGoodCount = phantomAgg.GoodCount;
            dto.PhantomWeakCount = phantomAgg.WeakCount;
            dto.PhantomRejectedCount = phantomAgg.RejectedCount;
            dto.PhantomBadCount = phantomAgg.BadCount;

            var phantomPeriod = ListRatingTrendCalculator.PeriodAggregate(
                openFacts, saleFrom, saleTo, asOf, usePhantomSaleDate: true);
            dto.PhantomPeriodEndValue = Math.Round(phantomPeriod.AverageScore, 2, MidpointRounding.AwayFromZero);
            dto.PhantomPeriodRatingLabel = phantomPeriod.FinalRatingLabel;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Employee rating: cohort = sales under their name with DateSaleDevice in [saleFrom, saleTo].
        /// Daily points and end score evaluate payment levels through chartTo (today).
        /// </summary>
        private async Task<BranchManagerTrendDto> EmployeeTrendAsync(
            int employeeId,
            DateTime saleFrom,
            DateTime saleTo,
            DateTime chartFrom,
            DateTime chartTo,
            CancellationToken ct)
        {
            var name = await ResolveEmployeeNameAsync(employeeId, ct) ?? "موظف مبيعات";
            if (employeeId <= 0)
            {
                return BuildTrend("employee", employeeId, name, MetricLabel, chartFrom, chartTo,
                    EmptySeries(chartFrom, chartTo), EmptyAggregate());
            }

            try
            {
                var customerIds = await LoadEmployeeCustomerIdsAsync(employeeId, ct);
                IReadOnlyList<CustomerRatingFacts> facts = customerIds.Count == 0
                    ? Array.Empty<CustomerRatingFacts>()
                    : ListFollowerRatingCohort.OpenBalanceOnly(
                        await _rating.GetFactsByCustomerIdsAsync(customerIds, ct));
                // Only customers sold inside the sale window — keeps daily loop cheap.
                var inWindow = facts
                    .Where(f => f.DateSaleDevice is DateTime sale
                        && sale.Date >= saleFrom
                        && sale.Date <= saleTo)
                    .ToList();
                var series = ListRatingTrendCalculator.DailyListScores(
                    inWindow, saleFrom, saleTo, chartTo, chartFrom);
                var end = ListRatingTrendCalculator.PeriodAggregate(inWindow, saleFrom, saleTo, chartTo);
                var dto = BuildTrend("employee", employeeId, name, PhantomSaleDate.RealMetricLabel, chartFrom, chartTo, series, end);
                var phantomEnd = ListRatingTrendCalculator.PeriodAggregate(
                    inWindow, saleFrom, saleTo, chartTo, usePhantomSaleDate: true);
                dto.PhantomMetricLabel = PhantomSaleDate.MetricLabel;
                dto.PhantomEndValue = Math.Round(phantomEnd.AverageScore, 2, MidpointRounding.AwayFromZero);
                dto.PhantomCustomerCount = phantomEnd.CustomerCount;
                dto.PhantomRatingLabel = phantomEnd.FinalRatingLabel;
                dto.PhantomExcellentCount = phantomEnd.ExcellentCount;
                dto.PhantomGoodCount = phantomEnd.GoodCount;
                dto.PhantomWeakCount = phantomEnd.WeakCount;
                dto.PhantomRejectedCount = phantomEnd.RejectedCount;
                dto.PhantomBadCount = phantomEnd.BadCount;
                return dto;
            }
            catch (Exception)
            {
                // Never 500 the director/BM UI for one employee — return empty series.
                return BuildTrend("employee", employeeId, name, MetricLabel, chartFrom, chartTo,
                    EmptySeries(chartFrom, chartTo), EmptyAggregate());
            }
        }

        private static void AttachPhantomHeadline(
            BranchManagerTrendDto dto,
            IReadOnlyList<CustomerRatingFacts> facts,
            DateTime asOf)
        {
            var phantom = EmployeeSalesRating.Snapshot(facts, asOf, usePhantomSaleDate: true);
            dto.PhantomMetricLabel = PhantomSaleDate.MetricLabel;
            dto.PhantomEndValue = phantom.AverageScore;
            dto.PhantomCustomerCount = phantom.CustomerCount;
            dto.PhantomRatingLabel = phantom.RatingLabel;
            dto.PhantomExcellentCount = phantom.ExcellentCount;
            dto.PhantomGoodCount = phantom.GoodCount;
            dto.PhantomWeakCount = phantom.WeakCount;
            dto.PhantomRejectedCount = phantom.RejectedCount;
            dto.PhantomBadCount = phantom.BadCount;
        }

        private async Task<IReadOnlyList<int>> LoadEmployeeCustomerIdsAsync(int employeeId, CancellationToken ct)
        {
            try
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
                    foreach (var id in fromDrafts)
                    {
                        ids.Add(id);
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
                    foreach (var id in fromRequests)
                    {
                        ids.Add(id);
                    }
                }

                return ids.ToList();
            }
            catch
            {
                return [];
            }
        }

        private async Task<IReadOnlyList<BranchManagerEntityOptionDto>> QueryEmployeeOptionsAsync(
            CancellationToken ct)
        {
            try
            {
                var cs = _guard.GetSalesConnectionString();
                if (string.IsNullOrWhiteSpace(cs)) return [];
                await using var connection = new SqlConnection(cs);
                var rows = await connection.QueryAsync<BranchManagerEntityOptionDto>(new CommandDefinition(@"
SELECT u.UserID AS Id, ISNULL(u.UserName, N'موظف مبيعات') AS Name
FROM dbo.Users u
WHERE u.UserType = N'موظف مبيعات'
  AND ISNULL(u.UserState, 1) = 1
ORDER BY ISNULL(u.UserName, N'موظف مبيعات');",
                    cancellationToken: ct));
                return rows.Where(r => r.Id > 0).ToList();
            }
            catch
            {
                return [];
            }
        }

        private async Task<string?> ResolveEmployeeNameAsync(int employeeId, CancellationToken ct)
        {
            try
            {
                var cs = _guard.GetSalesConnectionString();
                if (string.IsNullOrWhiteSpace(cs) || employeeId <= 0) return null;
                await using var connection = new SqlConnection(cs);
                return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 ISNULL(UserName, N'موظف مبيعات') FROM dbo.Users WHERE UserID = @Id",
                    new { Id = employeeId }, cancellationToken: ct));
            }
            catch
            {
                return null;
            }
        }

        private async Task<(IReadOnlyDictionary<DateTime, double> Series, ListRatingResult End)>
            AverageAcrossListIdsWithAggregateAsync(
                IReadOnlyList<int> listIds,
                DateTime saleFrom,
                DateTime saleTo,
                DateTime chartFrom,
                DateTime chartTo,
                CancellationToken ct)
        {
            var allFacts = ListFollowerRatingCohort.OpenBalanceOnly(
                await _rating.GetFactsByListIdsAsync(listIds, ct));
            var byList = listIds.ToDictionary(
                id => id,
                id => (IReadOnlyList<CustomerRatingFacts>)allFacts
                    .Where(f => f.DelegateId == id)
                    .ToList());
            var series = ListRatingTrendCalculator.DailyAverageAcrossLists(
                listIds, byList, saleFrom, saleTo, chartTo, chartFrom);
            var end = ListRatingTrendCalculator.PeriodAggregate(allFacts, saleFrom, saleTo, chartTo);
            return (series, end);
        }

        private async Task<IReadOnlyList<BranchManagerEntityOptionDto>> LoadActiveListsAsync(
            CancellationToken ct)
        {
            try
            {
                var cs = _guard.GetSalesConnectionString();
                if (string.IsNullOrWhiteSpace(cs))
                {
                    // Fallback: all delegates from repository (no state filter available on DTO).
                    var all = (await _delegates.Delegates_GetDataAll())?.ToList() ?? [];
                    return all
                        .Where(d => d.DelegateID is > 0)
                        .Select(d => new BranchManagerEntityOptionDto
                        {
                            Id = d.DelegateID!.Value,
                            Name = d.DelegateName
                        })
                        .OrderBy(x => x.Name)
                        .ToList();
                }

                await using var connection = new SqlConnection(cs);
                // قوائم شغالة = DelegateState = 1 (null treated as inactive).
                var rows = await connection.QueryAsync<BranchManagerEntityOptionDto>(new CommandDefinition(@"
SELECT d.DelegateID AS Id, d.DelegateName AS Name
FROM dbo.Delegates d
WHERE ISNULL(d.DelegateState, 0) = 1
ORDER BY d.DelegateName;",
                    cancellationToken: ct));
                var list = rows.Where(r => r.Id > 0).ToList();
                if (list.Count > 0) return list;

                // If none marked active, fall back to all named delegates so chart still works.
                var fallback = await connection.QueryAsync<BranchManagerEntityOptionDto>(new CommandDefinition(@"
SELECT d.DelegateID AS Id, d.DelegateName AS Name
FROM dbo.Delegates d
WHERE d.DelegateID IS NOT NULL
ORDER BY d.DelegateName;",
                    cancellationToken: ct));
                return fallback.Where(r => r.Id > 0).ToList();
            }
            catch
            {
                return [];
            }
        }

        private async Task<IReadOnlyList<BranchManagerEntityOptionDto>> AttachFollowerAssignedListsAsync(
            IReadOnlyList<BranchManagerEntityOptionDto> followers,
            CancellationToken ct)
        {
            if (followers.Count == 0)
                return followers;

            try
            {
                await _followerLists.EnsureSchemaAsync(ct);
                var map = new Dictionary<int, List<int>>();
                foreach (var f in followers)
                {
                    if (f.Id <= 0)
                        continue;
                    var ids = (await _followerLists.GetAssignedListIdsAsync(f.Id, ct)).ToList();
                    map[f.Id] = ids;
                }

                return followers.Select(f => new BranchManagerEntityOptionDto
                {
                    Id = f.Id,
                    Name = f.Name,
                    AssignedListIds = map.TryGetValue(f.Id, out var ids) ? ids : [],
                }).ToList();
            }
            catch
            {
                return followers;
            }
        }

        private async Task<IReadOnlyList<BranchManagerEntityOptionDto>> QueryFollowerOptionsAsync(
            CancellationToken ct)
        {
            try
            {
                var cs = _guard.GetSalesConnectionString();
                if (string.IsNullOrWhiteSpace(cs)) return [];
                await using var connection = new SqlConnection(cs);
                await connection.OpenAsync(ct);

                // Prefer Users of type متابع; fall back to auth table names.
                var usersExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.Users', N'U') IS NULL THEN 0 ELSE 1 END",
                    cancellationToken: ct));
                if (usersExists == 1)
                {
                    var fromUsers = (await connection.QueryAsync<BranchManagerEntityOptionDto>(
                        new CommandDefinition(@"
SELECT u.UserID AS Id, ISNULL(u.UserName, N'متابع') AS Name
FROM dbo.Users u
WHERE u.UserType = N'متابع'
  AND ISNULL(u.UserState, 1) = 1
ORDER BY ISNULL(u.UserName, N'متابع');",
                            cancellationToken: ct))).Where(r => r.Id > 0).ToList();
                    if (fromUsers.Count > 0) return fromUsers;
                }

                var authExists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerSaleAuthentications', N'U') IS NULL THEN 0 ELSE 1 END",
                    cancellationToken: ct));
                if (authExists == 0) return [];

                var rows = await connection.QueryAsync<BranchManagerEntityOptionDto>(new CommandDefinition(@"
SELECT FollowerUserId AS Id, MAX(ISNULL(FollowerName, N'متابع')) AS Name
FROM dbo.FollowerSaleAuthentications
GROUP BY FollowerUserId
ORDER BY MAX(ISNULL(FollowerName, N'متابع'));",
                    cancellationToken: ct));
                return rows.Where(r => r.Id > 0).ToList();
            }
            catch
            {
                return [];
            }
        }

        private async Task<string?> ResolveFollowerNameAsync(int userId, CancellationToken ct)
        {
            try
            {
                var cs = _guard.GetSalesConnectionString();
                if (string.IsNullOrWhiteSpace(cs) || userId <= 0) return null;
                await using var connection = new SqlConnection(cs);
                var name = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 ISNULL(UserName, N'متابع') FROM dbo.Users WHERE UserID = @Id",
                    new { Id = userId }, cancellationToken: ct));
                if (!string.IsNullOrWhiteSpace(name)) return name;

                return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 ISNULL(FollowerName, N'متابع')
FROM dbo.FollowerSaleAuthentications WHERE FollowerUserId = @Id",
                    new { Id = userId }, cancellationToken: ct));
            }
            catch
            {
                return null;
            }
        }

        private static IReadOnlyDictionary<DateTime, double> EmptySeries(DateTime from, DateTime to)
        {
            var map = new Dictionary<DateTime, double>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                map[d] = 0;
            }

            return map;
        }

        private static ListRatingResult EmptyAggregate() =>
            ListRatingAggregator.Aggregate([]);

        private static BranchManagerTrendDto BuildTrend(
            string scope,
            int id,
            string? name,
            string metricLabel,
            DateTime fromDate,
            DateTime toDate,
            IReadOnlyDictionary<DateTime, double> daily,
            ListRatingResult? periodEnd = null)
        {
            // Ascending dates → index 0 is oldest (chart left in LTR).
            var points = new List<BranchManagerTrendPointDto>();
            for (var d = fromDate; d <= toDate; d = d.AddDays(1))
            {
                daily.TryGetValue(d, out var value);
                points.Add(new BranchManagerTrendPointDto
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Value = Math.Round(value, 2)
                });
            }

            var start = points.FirstOrDefault()?.Value ?? 0;
            var end = points.LastOrDefault()?.Value ?? 0;
            var abs = end - start;
            double pct;
            if (Math.Abs(start) < 0.0001)
            {
                pct = end == 0 ? 0 : 100;
            }
            else
            {
                pct = abs * 100.0 / Math.Abs(start);
            }

            // Score scale is roughly -10..10; 0.05 is a meaningful tick.
            var direction = abs > 0.05 ? "up" : abs < -0.05 ? "down" : "flat";
            var dto = new BranchManagerTrendDto
            {
                Scope = scope,
                Id = id,
                Name = name,
                MetricLabel = metricLabel,
                FromDate = fromDate,
                ToDate = toDate,
                Points = points,
                StartValue = Math.Round(start, 2),
                EndValue = Math.Round(end, 2),
                ChangeAbsolute = Math.Round(abs, 2),
                ChangePercent = Math.Round(pct, 2),
                Direction = direction,
                PeriodIgnored = false
            };

            if (periodEnd is not null)
            {
                dto.CustomerCount = periodEnd.CustomerCount;
                dto.RatingLabel = periodEnd.FinalRatingLabel;
                dto.ExcellentCount = periodEnd.ExcellentCount;
                dto.GoodCount = periodEnd.GoodCount;
                dto.WeakCount = periodEnd.WeakCount;
                dto.RejectedCount = periodEnd.RejectedCount;
                dto.LegalCount = periodEnd.LegalCount;
                dto.BadCount = periodEnd.BadCount;
            }

            return dto;
        }

        private async Task<string?> ResolveDelegateNameAsync(int id)
        {
            if (id <= 0) return "قائمة";
            var delegates = (await _delegates.Delegates_GetDataAll())?.ToList() ?? [];
            return delegates.FirstOrDefault(d => d.DelegateID == id)?.DelegateName ?? $"قائمة {id}";
        }
    }
}
