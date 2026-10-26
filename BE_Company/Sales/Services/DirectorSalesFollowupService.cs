using System.Data;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class DirectorSalesFollowupEmployeeCardDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public int CustomerCount { get; set; }
    public double AverageScore { get; set; }
    public string RatingLabel { get; set; } = "";
    public int? ManagerScore { get; set; }
    public string? ManagerNote { get; set; }
    public DateTime? ManagerRatedAt { get; set; }
}

public sealed class DirectorSalesFollowupRatingRowDto
{
    public int Score { get; set; }
    public string? Note { get; set; }
    public DateTime RatingDate { get; set; }
    public string? ListName { get; set; }
    public string? CustomerName { get; set; }
    public int? ListId { get; set; }
    public int? RequestId { get; set; }
    /// <summary>auth · shop · seldr · collector (مندوب الجباية).</summary>
    public string? Kind { get; set; }
    /// <summary>اسم مندوب الجباية عند التقييم الأول.</summary>
    public string? DelegateName { get; set; }
}

public sealed class DirectorSalesFollowupRatingBucketDto
{
    public IReadOnlyList<DirectorSalesFollowupRatingRowDto> High { get; set; } = [];
    public IReadOnlyList<DirectorSalesFollowupRatingRowDto> Low { get; set; } = [];
}

public sealed class DirectorSalesFollowupSystemDayAvgDto
{
    public DateTime RatingDate { get; set; }
    public double AverageScore { get; set; }
    public string RatingLabel { get; set; } = "";
    public int CustomerCount { get; set; }
}

public sealed class DirectorSalesFollowupDetailDto
{
    public DirectorSalesFollowupEmployeeCardDto Employee { get; set; } = new();
    public DirectorSalesFollowupRatingRowDto? ManagerRating { get; set; }
    public DirectorSalesFollowupRatingBucketDto SalesDelegateRatings { get; set; } = new();
    public DirectorSalesFollowupRatingBucketDto FollowerSaleAuthRatings { get; set; } = new();
    /// <summary>تقييم مندوب الجباية عند أول مبيع — لوحة المدير: ≤6 فقط بدون اسم زبون.</summary>
    public DirectorSalesFollowupRatingBucketDto CollectionDelegateRatings { get; set; } = new();
    /// <summary>آخر 30 متوسط يومي لتقييم النظام (Snapshot لكل يوم).</summary>
    public IReadOnlyList<DirectorSalesFollowupSystemDayAvgDto> SystemRatingHistory { get; set; } = [];
    /// <summary>زبائن سدّدوا بالكامل (باقي ≤ 0 وغير قانونية).</summary>
    public int ZeroedCustomerCount { get; set; }
    /// <summary>Customers embedded for one-shot FE load (same shape as متابعة المندوبين).</summary>
    public IReadOnlyList<DirectorDelegateFollowupCustomerDto> Customers { get; set; } = [];
    /// <summary>خط التحويلات — نفس منطق متابعة المندوبين على زبائن هذا البائع.</summary>
    public DirectorDelegateFollowupConversionDto Conversion { get; set; } = new();
    public string Marker { get; set; } = DirectorSalesFollowupRules.DeployMarker;
}

public sealed class DirectorSalesFollowupUpsertManagerRatingRequest
{
    public int Score { get; set; }
    public string? Note { get; set; }
}

public interface IDirectorSalesFollowupService
{
    Task<IReadOnlyList<DirectorSalesFollowupEmployeeCardDto>> ListEmployeesAsync(CancellationToken ct);
    Task<DirectorSalesFollowupDetailDto?> GetDetailAsync(int employeeId, CancellationToken ct);
    Task<IReadOnlyList<DirectorDelegateFollowupCustomerDto>> GetEmployeeCustomersAsync(
        int employeeId, CancellationToken ct);
    Task<(bool Ok, string? Error, DirectorSalesFollowupRatingRowDto? Row)> UpsertManagerRatingAsync(
        int employeeId,
        string? ratedByUserName,
        int score,
        string? note,
        CancellationToken ct);
}

public sealed class DirectorSalesFollowupService : IDirectorSalesFollowupService
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly IBranchManagerStatsService _stats;
    private readonly IRatingDataSource _rating;

    public DirectorSalesFollowupService(
        SalesDevelopmentGuard guard,
        IBranchManagerStatsService stats,
        IRatingDataSource rating)
    {
        _guard = guard;
        _stats = stats;
        _rating = rating;
    }

    public async Task<IReadOnlyList<DirectorSalesFollowupEmployeeCardDto>> ListEmployeesAsync(CancellationToken ct)
    {
        await EnsureManagerRatingsTableAsync(ct);
        var catalog = await _stats.GetCatalogAsync(ct);
        var employees = (catalog.Employees ?? [])
            .Where(e => e.Id > 0)
            .ToList();
        if (employees.Count == 0) return [];

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var empIds = employees.Select(e => e.Id).ToList();
        var customerMap = await LoadEmployeeCustomerMapAsync(empIds, ct);
        var allCustomerIds = customerMap.Values.SelectMany(x => x).Distinct().ToList();
        var allFacts = allCustomerIds.Count == 0
            ? Array.Empty<CustomerRatingFacts>()
            : await _rating.GetFactsByCustomerIdsAsync(allCustomerIds, ct);
        var factById = allFacts.ToDictionary(f => f.CustomerId);
        var managerByEmp = await LoadLatestManagerRatingsAsync(ct);

        var cards = new List<DirectorSalesFollowupEmployeeCardDto>(employees.Count);
        foreach (var emp in employees)
        {
            ct.ThrowIfCancellationRequested();
            customerMap.TryGetValue(emp.Id, out var ids);
            ids ??= [];
            var facts = ids
                .Select(id => factById.TryGetValue(id, out var f) ? f : null)
                .Where(f => f is not null)
                .Cast<CustomerRatingFacts>()
                .ToList();
            var snap = EmployeeSalesRating.Snapshot(facts, asOf, usePhantomSaleDate: false);
            managerByEmp.TryGetValue(emp.Id, out var mgr);
            cards.Add(new DirectorSalesFollowupEmployeeCardDto
            {
                EmployeeId = emp.Id,
                EmployeeName = string.IsNullOrWhiteSpace(emp.Name) ? $"موظف #{emp.Id}" : emp.Name.Trim(),
                CustomerCount = snap.CustomerCount,
                AverageScore = snap.AverageScore,
                RatingLabel = snap.RatingLabel,
                ManagerScore = mgr?.Score,
                ManagerNote = mgr?.Note,
                ManagerRatedAt = mgr?.RatingDate,
            });
        }

        return cards
            .OrderBy(c => c.AverageScore)
            .ThenBy(c => c.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<DirectorSalesFollowupDetailDto?> GetDetailAsync(int employeeId, CancellationToken ct)
    {
        if (employeeId <= 0) return null;
        await EnsureManagerRatingsTableAsync(ct);

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var customerIds = await LoadEmployeeCustomerIdsAsync(employeeId, ct);
        var facts = customerIds.Count == 0
            ? Array.Empty<CustomerRatingFacts>()
            : await _rating.GetFactsByCustomerIdsAsync(customerIds, ct);
        var snap = EmployeeSalesRating.Snapshot(facts, asOf, usePhantomSaleDate: false);

        var emp = new DirectorSalesFollowupEmployeeCardDto
        {
            EmployeeId = employeeId,
            EmployeeName = await ResolveEmployeeNameAsync(employeeId, ct) ?? $"موظف #{employeeId}",
            CustomerCount = snap.CustomerCount,
            AverageScore = snap.AverageScore,
            RatingLabel = snap.RatingLabel,
        };
        var manager = await GetManagerRatingAsync(employeeId, ct);
        emp.ManagerScore = manager?.Score;
        emp.ManagerNote = manager?.Note;
        emp.ManagerRatedAt = manager?.RatingDate;

        // Parallel: auth/shop + مندوب الجباية + customers (SELDR card removed — skip load)
        var authTask = LoadFollowerSaleAuthHistoryAsync(employeeId, customerIds, ct);
        var collectorTask = LoadCollectionDelegateHistoryAsync(customerIds, ct);
        var custTask = BuildCustomerRowsAsync(customerIds, facts, asOf, ct);
        await Task.WhenAll(authTask, collectorTask, custTask);

        var customers = await custTask;
        var factsList = facts as IReadOnlyList<CustomerRatingFacts> ?? facts.ToList();
        // Board: ≤6 only, anonymized (score + note + date). High left empty.
        var folLow = DirectorSalesFollowupRules.AnonymizeLowOnly(await authTask);
        var colLow = DirectorSalesFollowupRules.AnonymizeLowOnly(await collectorTask);
        var systemHistory = DirectorSalesFollowupRules.BuildSystemAverageHistory(factsList, asOf);
        var zeroedCount = DirectorSalesFollowupRules.CountZeroedCustomers(factsList);

        // Same cohort as متابعة المندوبين: remaining > 0 (legal included for التحويل/قانونية)
        var openBalance = factsList.Where(f => f.AmountRemaining > 0).ToList();
        var nameById = customers
            .Where(c => c.CustomerID is > 0)
            .GroupBy(c => c.CustomerID!.Value)
            .ToDictionary(g => g.Key, g => g.First().CustomerName ?? $"#{g.Key}");
        var conversion = BuildConversion(openBalance, nameById, asOf);

        return new DirectorSalesFollowupDetailDto
        {
            Employee = emp,
            ManagerRating = manager,
            SalesDelegateRatings = new DirectorSalesFollowupRatingBucketDto(),
            FollowerSaleAuthRatings = new DirectorSalesFollowupRatingBucketDto { High = [], Low = folLow },
            CollectionDelegateRatings = new DirectorSalesFollowupRatingBucketDto { High = [], Low = colLow },
            SystemRatingHistory = systemHistory,
            ZeroedCustomerCount = zeroedCount,
            Customers = customers,
            Conversion = conversion,
            Marker = DirectorSalesFollowupRules.DeployMarker,
        };
    }

    private static DirectorDelegateFollowupConversionDto BuildConversion(
        IReadOnlyList<CustomerRatingFacts> openBalance,
        IReadOnlyDictionary<int, string> customerNames,
        DateTime asOf)
    {
        if (openBalance.Count == 0)
        {
            return new DirectorDelegateFollowupConversionDto
            {
                SummaryText = "ازداد 0 · نقص 0 · مساوي 0",
            };
        }

        var historyDays = Enumerable.Range(0, 30)
            .Select(i => asOf.AddDays(-i))
            .ToList();
        var labelByDay = new Dictionary<DateTime, Dictionary<int, string>>();

        void EnsureLabels(DateTime day)
        {
            var key = day.Date;
            if (labelByDay.ContainsKey(key))
                return;
            labelByDay[key] = CustomerRatingCalculator
                .EvaluateMany(openBalance, key, usePhantomSaleDate: false)
                .GroupBy(r => r.CustomerId)
                .ToDictionary(g => g.Key, g => CustomerRatingLabels.FromLevel(g.First().Level));
        }

        EnsureLabels(asOf);
        foreach (var d in historyDays)
        {
            EnsureLabels(d);
            EnsureLabels(d.AddDays(-1));
        }

        string StatusFor(CustomerRatingFacts f, DateTime day)
        {
            if (f.IsLegal)
                return DirectorDelegateFollowupRules.StatusLegal;
            EnsureLabels(day);
            return labelByDay[day.Date].TryGetValue(f.CustomerId, out var lb)
                ? DirectorDelegateFollowupRules.NormalizeStatusLabel(lb)
                : DirectorDelegateFollowupRules.StatusUnknown;
        }

        IEnumerable<(string? Prev, string? Cur)> PairsForDay(DateTime day) =>
            openBalance.Select(f => (
                (string?)StatusFor(f, day.AddDays(-1)),
                (string?)StatusFor(f, day)));

        var conversionTargets = new[]
        {
            CustomerRatingLabels.Excellent,
            CustomerRatingLabels.Good,
            CustomerRatingLabels.Weak,
            CustomerRatingLabels.Rejected,
            CustomerRatingLabels.Bad,
            DirectorDelegateFollowupRules.StatusLegal,
        };

        List<DirectorDelegateFollowupConversionCustomerDto> ArrivalCustomers(DateTime day, string target)
        {
            var list = new List<DirectorDelegateFollowupConversionCustomerDto>();
            foreach (var f in openBalance)
            {
                var prev = StatusFor(f, day.AddDays(-1));
                var cur = StatusFor(f, day);
                if (cur != target || prev == cur)
                    continue;
                customerNames.TryGetValue(f.CustomerId, out var name);
                list.Add(new DirectorDelegateFollowupConversionCustomerDto
                {
                    CustomerId = f.CustomerId,
                    CustomerName = string.IsNullOrWhiteSpace(name) ? $"#{f.CustomerId}" : name!,
                    FromLabel = prev,
                });
            }

            return list
                .OrderBy(x => x.CustomerName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        var todayConv = DirectorDelegateFollowupRules.CountConversions(PairsForDay(asOf));
        var todayArrivals = DirectorDelegateFollowupRules.BuildArrivalBreakdown(
            PairsForDay(asOf), conversionTargets);

        static string FormatArrivalDetail(IReadOnlyList<(string From, int Count)> breakdown) =>
            breakdown.Count == 0
                ? "لا متحولين اليوم"
                : string.Join(" · ", breakdown.Select(x => $"من {x.From}: {x.Count}"));

        var conversionTargetDtos = new List<DirectorDelegateFollowupConversionTargetDto>(conversionTargets.Length);
        foreach (var target in conversionTargets)
        {
            todayArrivals.TryGetValue(target, out var todayFrom);
            todayFrom ??= [];
            var todayCustomers = ArrivalCustomers(asOf, target);
            var targetHistory = historyDays.Select(d =>
            {
                var dayMap = DirectorDelegateFollowupRules.BuildArrivalBreakdown(
                    PairsForDay(d), conversionTargets);
                dayMap.TryGetValue(target, out var from);
                from ??= [];
                var dayCustomers = ArrivalCustomers(d, target);
                return new DirectorDelegateFollowupConversionTargetDayDto
                {
                    Date = d,
                    ArrivedCount = dayCustomers.Count,
                    FromBreakdown = from
                        .Select(x => new DirectorDelegateFollowupConversionFromDto
                        {
                            FromLabel = x.From,
                            Count = x.Count,
                        })
                        .ToList(),
                    Customers = dayCustomers,
                };
            }).ToList();

            conversionTargetDtos.Add(new DirectorDelegateFollowupConversionTargetDto
            {
                TargetLabel = target,
                ArrivedCount = todayCustomers.Count,
                DetailText = FormatArrivalDetail(todayFrom),
                FromBreakdown = todayFrom
                    .Select(x => new DirectorDelegateFollowupConversionFromDto
                    {
                        FromLabel = x.From,
                        Count = x.Count,
                    })
                    .ToList(),
                Customers = todayCustomers,
                Last30Days = targetHistory,
            });
        }

        return new DirectorDelegateFollowupConversionDto
        {
            Improved = todayConv.Improved,
            Declined = todayConv.Declined,
            Unchanged = todayConv.Unchanged,
            SummaryText =
                $"ازداد {todayConv.Improved} · نقص {todayConv.Declined} · مساوي {todayConv.Unchanged}",
            Targets = conversionTargetDtos,
        };
    }

    public async Task<IReadOnlyList<DirectorDelegateFollowupCustomerDto>> GetEmployeeCustomersAsync(
        int employeeId,
        CancellationToken ct)
    {
        if (employeeId <= 0) return [];
        var customerIds = await LoadEmployeeCustomerIdsAsync(employeeId, ct);
        if (customerIds.Count == 0) return [];
        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var facts = await _rating.GetFactsByCustomerIdsAsync(customerIds, ct);
        return await BuildCustomerRowsAsync(customerIds, facts, asOf, ct);
    }

    public async Task<(bool Ok, string? Error, DirectorSalesFollowupRatingRowDto? Row)> UpsertManagerRatingAsync(
        int employeeId,
        string? ratedByUserName,
        int score,
        string? note,
        CancellationToken ct)
    {
        if (employeeId <= 0) return (false, "معرّف الموظف غير صالح.", null);
        var (ok, error) = SalesEmployeeListDayRatingRules.ValidateScore(score);
        if (!ok) return (false, error, null);

        await EnsureManagerRatingsTableAsync(ct);
        var iraqToday = SalesEmployeeListDayRatingRules.IraqToday(DateTime.UtcNow);
        var noteTrim = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var by = string.IsNullOrWhiteSpace(ratedByUserName) ? null : ratedByUserName.Trim();

        await using var c = Open();
        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.DirectorSalesEmployeeBoardRatings AS t
USING (SELECT @EmployeeId AS EmployeeId, @IraqDate AS RatingDate) AS s
ON t.EmployeeId = s.EmployeeId AND t.RatingDate = s.RatingDate
WHEN MATCHED THEN UPDATE SET
    Score = @Score,
    Note = @Note,
    RatedByUserName = @RatedBy,
    UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (EmployeeId, RatingDate, Score, Note, RatedByUserName, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@EmployeeId, @IraqDate, @Score, @Note, @RatedBy, SYSUTCDATETIME(), SYSUTCDATETIME());",
            new
            {
                EmployeeId = employeeId,
                IraqDate = iraqToday.ToDateTime(TimeOnly.MinValue),
                Score = score,
                Note = noteTrim,
                RatedBy = by,
            },
            cancellationToken: ct));

        var row = await GetManagerRatingAsync(employeeId, ct);
        return (true, null, row);
    }

    private async Task<IReadOnlyList<DirectorDelegateFollowupCustomerDto>> BuildCustomerRowsAsync(
        IReadOnlyList<int> customerIds,
        IReadOnlyList<CustomerRatingFacts> facts,
        DateTime asOf,
        CancellationToken ct)
    {
        if (customerIds.Count == 0) return [];

        var factById = facts.ToDictionary(f => f.CustomerId);
        var ratedById = CustomerRatingCalculator
            .EvaluateMany(facts, asOf, usePhantomSaleDate: false)
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.First());

        var profiles = await LoadCustomerProfilesAsync(customerIds, ct);
        var lastPay = await LoadLastPaymentAmountsAsync(customerIds, ct);

        var rows = new List<DirectorDelegateFollowupCustomerDto>(customerIds.Count);
        foreach (var id in customerIds)
        {
            profiles.TryGetValue(id, out var p);
            factById.TryGetValue(id, out var fact);
            ratedById.TryGetValue(id, out var rated);
            lastPay.TryGetValue(id, out var lastAmt);

            var remaining = fact?.AmountRemaining
                ?? p?.AmountRemaining
                ?? 0;
            // Show same cohort spirit as DFU board: prefer open-balance; still show settled if few open.
            rows.Add(new DirectorDelegateFollowupCustomerDto
            {
                CustomerID = id,
                DelegateID = fact?.DelegateId ?? p?.DelegateID,
                CustomerName = p?.CustomerName,
                Address = p?.Address,
                Notes = p?.Notes,
                PhoneNumber = p?.PhoneNumber,
                ShopName = p?.ShopName,
                SaleName = p?.SaleName,
                ReceiptName = p?.ReceiptName,
                DelegateName = p?.DelegateName,
                DateSaleDevice = fact?.DateSaleDevice ?? p?.DateSaleDevice,
                AmountTotalSales = fact?.AmountTotalSales ?? p?.AmountTotalSales,
                AmountDaySales = p?.AmountDaySales,
                ReceiptsTotal = fact?.ReceiptsTotal ?? p?.ReceiptsTotal,
                AmountRemaining = remaining,
                ItemsNames = p?.ItemsNames,
                LastPaymentDate = fact?.LastPaymentDate ?? p?.LastPaymentDate,
                LastPaymentAmount = lastAmt > 0 ? lastAmt : null,
                CountReceiptDevice = fact?.ReceiptCount ?? p?.CountReceiptDevice,
                NumberOfDayDevice = p?.NumberOfDayDevice,
                ReceiptRateDevice = p?.ReceiptRateDevice,
                PhantomRatingLabel = fact?.IsLegal == true
                    ? CustomerRatingLabels.Legal
                    : rated is null ? "" : CustomerRatingLabels.FromLevel(rated.Level),
                IsLegal = fact?.IsLegal == true,
                IsActiveRating = ActiveCustomerRules.IsActive(
                    fact?.LastPaymentDate ?? p?.LastPaymentDate, asOf),
            });
        }

        // Prefer open-balance first (DFU-like), then by last payment
        return rows
            .OrderByDescending(r => (r.AmountRemaining ?? 0) > 0)
            .ThenByDescending(r => r.LastPaymentDate ?? DateTime.MinValue)
            .ThenBy(r => r.CustomerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class CustomerProfileRow
    {
        public int CustomerID { get; set; }
        public int? DelegateID { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ShopName { get; set; }
        public string? SaleName { get; set; }
        public string? ReceiptName { get; set; }
        public string? DelegateName { get; set; }
        public DateTime? DateSaleDevice { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? AmountDaySales { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
        public string? ItemsNames { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public int? CountReceiptDevice { get; set; }
        public int? NumberOfDayDevice { get; set; }
        public int? ReceiptRateDevice { get; set; }
    }

    private async Task<Dictionary<int, CustomerProfileRow>> LoadCustomerProfilesAsync(
        IReadOnlyList<int> customerIds,
        CancellationToken ct)
    {
        var map = new Dictionary<int, CustomerProfileRow>();
        if (customerIds.Count == 0) return map;

        await using var c = Open();
        foreach (var chunk in SqlInClauseBatch.Chunk(customerIds))
        {
            try
            {
                var rows = await c.QueryAsync<CustomerProfileRow>(new CommandDefinition(@"
SELECT
    c.CustomerID,
    c.DelegateID,
    c.CustomerName,
    c.Address,
    c.Notes,
    c.PhoneNumber,
    c.ShopName,
    c.SaleName,
    c.ReceiptName,
    d.DelegateName,
    vc.DateSaleDevice,
    CAST(vc.AmountTotalSales AS float) AS AmountTotalSales,
    CAST(vc.AmountDaySales AS float) AS AmountDaySales,
    CAST(vc.ReceiptsTotal AS float) AS ReceiptsTotal,
    CAST(vc.AmountRemaining AS float) AS AmountRemaining,
    vc.ItemsNames,
    vc.LastPaymentDate,
    vc.CountReceiptDevice,
    vc.NumberOfDayDevice,
    vc.ReceiptRateDevice
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.CustomerID IN @Ids;",
                    new { Ids = chunk }, cancellationToken: ct));
                foreach (var r in rows)
                    map[r.CustomerID] = r;
            }
            catch
            {
                // Fallback without View_Customers
                var rows = await c.QueryAsync<CustomerProfileRow>(new CommandDefinition(@"
SELECT
    c.CustomerID,
    c.DelegateID,
    c.CustomerName,
    c.Address,
    c.Notes,
    c.PhoneNumber,
    c.ShopName,
    c.SaleName,
    c.ReceiptName,
    d.DelegateName
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE c.CustomerID IN @Ids;",
                    new { Ids = chunk }, cancellationToken: ct));
                foreach (var r in rows)
                    map[r.CustomerID] = r;
            }
        }

        return map;
    }

    private async Task<Dictionary<int, double>> LoadLastPaymentAmountsAsync(
        IReadOnlyList<int> customerIds,
        CancellationToken ct)
    {
        var map = new Dictionary<int, double>();
        if (customerIds.Count == 0) return map;
        await using var c = Open();
        foreach (var chunk in SqlInClauseBatch.Chunk(customerIds))
        {
            try
            {
                var rows = await c.QueryAsync<(int CustomerId, double Amount)>(new CommandDefinition(@"
;WITH Ranked AS (
    SELECT CustomerID AS CustomerId,
           CAST(ISNULL(AmountDenar, 0) AS float) AS Amount,
           ROW_NUMBER() OVER (PARTITION BY CustomerID ORDER BY PaymentDate DESC, CustomerPaymentID DESC) AS rn
    FROM dbo.View_CustomersPaymentsDelegate
    WHERE CustomerID IN @Ids
)
SELECT CustomerId, Amount FROM Ranked WHERE rn = 1;",
                    new { Ids = chunk }, cancellationToken: ct));
                foreach (var r in rows)
                    map[r.CustomerId] = r.Amount;
            }
            catch
            {
                /* ignore */
            }
        }

        return map;
    }

    private async Task<DirectorSalesFollowupRatingRowDto?> GetManagerRatingAsync(int employeeId, CancellationToken ct)
    {
        await using var c = Open();
        return await c.QueryFirstOrDefaultAsync<DirectorSalesFollowupRatingRowDto>(new CommandDefinition(@"
SELECT TOP 1 Score, Note, RatingDate, N'manager' AS Kind
FROM dbo.DirectorSalesEmployeeBoardRatings
WHERE EmployeeId = @EmployeeId
ORDER BY RatingDate DESC, UpdatedAtUtc DESC;",
            new { EmployeeId = employeeId }, cancellationToken: ct));
    }

    private async Task<Dictionary<int, DirectorSalesFollowupRatingRowDto>> LoadLatestManagerRatingsAsync(CancellationToken ct)
    {
        await using var c = Open();
        var rows = (await c.QueryAsync<dynamic>(new CommandDefinition(@"
SELECT EmployeeId, Score, Note, RatingDate
FROM (
    SELECT EmployeeId, Score, Note, RatingDate,
           ROW_NUMBER() OVER (PARTITION BY EmployeeId ORDER BY RatingDate DESC, UpdatedAtUtc DESC) AS rn
    FROM dbo.DirectorSalesEmployeeBoardRatings
) x WHERE rn = 1;", cancellationToken: ct))).ToList();

        var map = new Dictionary<int, DirectorSalesFollowupRatingRowDto>();
        foreach (var r in rows)
        {
            int id = (int)r.EmployeeId;
            map[id] = new DirectorSalesFollowupRatingRowDto
            {
                Score = (int)r.Score,
                Note = r.Note as string,
                RatingDate = (DateTime)r.RatingDate,
                Kind = "manager",
            };
        }
        return map;
    }

    private async Task<List<DirectorSalesFollowupRatingRowDto>> LoadSeldrHistoryAsync(int employeeId, CancellationToken ct)
    {
        await using var c = Open();
        var rows = await c.QueryAsync<DirectorSalesFollowupRatingRowDto>(new CommandDefinition(@"
SELECT TOP (60)
    r.Score,
    r.Note,
    r.IraqDate AS RatingDate,
    d.DelegateName AS ListName,
    r.ListId,
    N'seldr' AS Kind
FROM dbo.SalesEmployeeListDayRating r
LEFT JOIN dbo.Delegates d ON d.DelegateID = r.ListId
WHERE r.EmployeeId = @EmployeeId
ORDER BY r.IraqDate DESC, r.Id DESC;",
            new { EmployeeId = employeeId }, cancellationToken: ct));
        return rows.AsList();
    }

    /// <summary>
    /// تقييم المبيعات الجديدة والمصادقة: AuthRating من المتابع + ShopRating / تقييم أول جباية.
    /// </summary>
    private async Task<List<DirectorSalesFollowupRatingRowDto>> LoadFollowerSaleAuthHistoryAsync(
        int employeeId,
        IReadOnlyList<int> customerIds,
        CancellationToken ct)
    {
        await using var c = Open();
        var bag = new List<DirectorSalesFollowupRatingRowDto>();

        // 1) مصادقة المتابع على مبيعات هذا الموظف (مسودات + زبائن)
        try
        {
            var authRows = await c.QueryAsync<DirectorSalesFollowupRatingRowDto>(new CommandDefinition(@"
SELECT TOP (60)
    CAST(a.Rating AS int) AS Score,
    a.Note,
    CAST(a.AuthenticatedAtUtc AS date) AS RatingDate,
    ISNULL(c.CustomerName, N'') AS CustomerName,
    a.ListId,
    d.DelegateName AS ListName,
    N'auth' AS Kind
FROM dbo.FollowerSaleAuthentications a
LEFT JOIN dbo.Customers c ON c.CustomerID = a.CustomerId
LEFT JOIN dbo.Delegates d ON d.DelegateID = a.ListId
LEFT JOIN dbo.SalesDrafts sd ON sd.SaleId = a.SaleDraftId
WHERE a.Rating IS NOT NULL
  AND a.Rating >= 0
  AND (
        sd.EmployeeId = @EmployeeId
     OR EXISTS (
            SELECT 1 FROM dbo.SalesRequests sr
            WHERE sr.TargetEmployeeId = @EmployeeId
              AND sr.ExistingCustomerId = a.CustomerId
              AND sr.Status = N'Completed')
     OR (@HasIds = 1 AND a.CustomerId IN @CustomerIds)
  )
ORDER BY a.AuthenticatedAtUtc DESC, a.Id DESC;",
                new
                {
                    EmployeeId = employeeId,
                    CustomerIds = customerIds.Count > 0 ? customerIds : new[] { -1 },
                    HasIds = customerIds.Count > 0 ? 1 : 0,
                },
                cancellationToken: ct));
            bag.AddRange(authRows);
        }
        catch
        {
            /* table may be missing */
        }

        // 2) تقييم المبيعات الجديدة (ShopRating على الطلبات)
        try
        {
            var hasCol = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN COL_LENGTH(N'dbo.SalesRequests', N'ShopRating') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (hasCol == 1)
            {
                var shopRows = await c.QueryAsync<DirectorSalesFollowupRatingRowDto>(new CommandDefinition(@"
SELECT TOP (60)
    CAST(sr.ShopRating AS int) AS Score,
    sr.ShopRatingNote AS Note,
    CAST(COALESCE(sr.CompletedAtUtc, sr.AuthenticatedAtUtc, sr.UpdatedAtUtc, sr.CreatedAtUtc) AS date) AS RatingDate,
    sr.CustomerName,
    sr.Id AS RequestId,
    N'shop' AS Kind
FROM dbo.SalesRequests sr
WHERE sr.ShopRating IS NOT NULL
  AND sr.ShopRating >= 0
  AND sr.TargetEmployeeId = @EmployeeId
ORDER BY COALESCE(sr.CompletedAtUtc, sr.AuthenticatedAtUtc, sr.UpdatedAtUtc, sr.CreatedAtUtc) DESC;",
                    new { EmployeeId = employeeId }, cancellationToken: ct));
                bag.AddRange(shopRows);
            }
        }
        catch
        {
            /* ignore */
        }

        return bag
            .OrderByDescending(r => r.RatingDate)
            .ThenByDescending(r => r.Score)
            .Take(60)
            .ToList();
    }

    /// <summary>
    /// تقييم مندوب الجباية عند وصول مبيع جديد — يعرض هنا التقييم والملاحظة فقط
    /// (الوارد والقيمة تُحفظ في التطبيق ولا تُعرض على هذه اللوحة).
    /// </summary>
    private async Task<List<DirectorSalesFollowupRatingRowDto>> LoadCollectionDelegateHistoryAsync(
        IReadOnlyList<int> customerIds,
        CancellationToken ct)
    {
        if (customerIds.Count == 0) return [];

        await using var c = Open();
        var bag = new List<DirectorSalesFollowupRatingRowDto>();
        try
        {
            foreach (var chunk in SqlInClauseBatch.Chunk(customerIds))
            {
                var rows = await c.QueryAsync<DirectorSalesFollowupRatingRowDto>(new CommandDefinition(@"
SELECT TOP (60)
    CAST(e.Rating AS int) AS Score,
    e.Note,
    CAST(e.CreatedAtUtc AS date) AS RatingDate,
    ISNULL(c.CustomerName, N'') AS CustomerName,
    e.ListId,
    COALESCE(NULLIF(e.DelegateName, N''), d.DelegateName) AS ListName,
    COALESCE(NULLIF(e.DelegateName, N''), d.DelegateName) AS DelegateName,
    N'collector' AS Kind
FROM dbo.DelegateFirstCollectionEvaluations e
LEFT JOIN dbo.Customers c ON c.CustomerID = e.CustomerId
LEFT JOIN dbo.Delegates d ON d.DelegateID = COALESCE(NULLIF(e.ListId, 0), NULLIF(e.DelegateId, 0))
WHERE e.CustomerId IN @Ids
  AND e.Rating IS NOT NULL
  AND e.Rating >= 0
ORDER BY e.CreatedAtUtc DESC;",
                    new { Ids = chunk }, cancellationToken: ct));
                bag.AddRange(rows);
            }
        }
        catch
        {
            return [];
        }

        return bag
            .OrderByDescending(r => r.RatingDate)
            .ThenByDescending(r => r.Score)
            .Take(60)
            .ToList();
    }

    private async Task EnsureManagerRatingsTableAsync(CancellationToken ct)
    {
        await using var c = Open();
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.DirectorSalesEmployeeBoardRatings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DirectorSalesEmployeeBoardRatings (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DirectorSalesEmployeeBoardRatings PRIMARY KEY,
        EmployeeId INT NOT NULL,
        RatingDate DATE NOT NULL,
        Score INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        RatedByUserName NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DSEBR_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DSEBR_Updated DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX UX_DSEBR_EmpDate ON dbo.DirectorSalesEmployeeBoardRatings(EmployeeId, RatingDate);
END", cancellationToken: ct));
    }

    private async Task EnsureSeldrSchemaAsync(CancellationToken ct)
    {
        await using var c = Open();
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesEmployeeListDayRating', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesEmployeeListDayRating (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesEmployeeListDayRating PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ListId INT NOT NULL,
        IraqDate DATE NOT NULL,
        Score INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SELDR_Created DEFAULT SYSUTCDATETIME()
    );
END", cancellationToken: ct));
    }

    private async Task<IReadOnlyList<int>> LoadEmployeeCustomerIdsAsync(int employeeId, CancellationToken ct)
    {
        var map = await LoadEmployeeCustomerMapAsync([employeeId], ct);
        return map.TryGetValue(employeeId, out var ids) ? ids : [];
    }

    private async Task<Dictionary<int, List<int>>> LoadEmployeeCustomerMapAsync(
        IReadOnlyList<int> employeeIds,
        CancellationToken ct)
    {
        var map = employeeIds.ToDictionary(id => id, _ => new List<int>());
        if (employeeIds.Count == 0) return map;

        await using var c = Open();
        foreach (var chunk in SqlInClauseBatch.Chunk(employeeIds))
        {
            try
            {
                var fromDrafts = await c.QueryAsync<(int EmployeeId, int CustomerId)>(new CommandDefinition(@"
SELECT DISTINCT EmployeeId, CustomerId
FROM dbo.SalesDrafts
WHERE EmployeeId IN @Ids
  AND Status = N'Completed'
  AND CustomerId IS NOT NULL AND CustomerId > 0;",
                    new { Ids = chunk }, cancellationToken: ct));
                foreach (var row in fromDrafts)
                {
                    if (map.TryGetValue(row.EmployeeId, out var list))
                        list.Add(row.CustomerId);
                }
            }
            catch { /* table may be missing */ }

            try
            {
                var fromRequests = await c.QueryAsync<(int EmployeeId, int CustomerId)>(new CommandDefinition(@"
SELECT DISTINCT TargetEmployeeId AS EmployeeId, ExistingCustomerId AS CustomerId
FROM dbo.SalesRequests
WHERE TargetEmployeeId IN @Ids
  AND Status = N'Completed'
  AND ExistingCustomerId IS NOT NULL AND ExistingCustomerId > 0;",
                    new { Ids = chunk }, cancellationToken: ct));
                foreach (var row in fromRequests)
                {
                    if (map.TryGetValue(row.EmployeeId, out var list))
                        list.Add(row.CustomerId);
                }
            }
            catch { /* ignore */ }
        }

        foreach (var key in map.Keys.ToList())
            map[key] = map[key].Distinct().ToList();

        return map;
    }

    private async Task<string?> ResolveEmployeeNameAsync(int employeeId, CancellationToken ct)
    {
        await using var c = Open();
        try
        {
            return await c.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 ISNULL(UserName, N'موظف مبيعات') FROM dbo.Users WHERE UserID = @Id;",
                new { Id = employeeId }, cancellationToken: ct));
        }
        catch
        {
            return null;
        }
    }

    private SqlConnection Open()
    {
        var cs = _guard.GetSalesConnectionString()
                 ?? throw new InvalidOperationException("Sales connection missing.");
        return new SqlConnection(cs);
    }
}
