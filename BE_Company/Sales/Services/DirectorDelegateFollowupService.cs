using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class DirectorDelegateFollowupListCardDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";
    /// <summary>Real (non-phantom) list rating average.</summary>
    public double RealRating { get; set; }
    public string RealRatingLabel { get; set; } = "";
    /// <summary>Compat alias — same as RealRating.</summary>
    public double PhantomRating
    {
        get => RealRating;
        set => RealRating = value;
    }
    public string PhantomRatingLabel
    {
        get => RealRatingLabel;
        set => RealRatingLabel = value;
    }
    public int NonZeroedCustomerCount { get; set; }
    /// <summary>Compat — non-zeroed count (active cohort removed from board).</summary>
    public int ActiveCustomerCount
    {
        get => NonZeroedCustomerCount;
        set => NonZeroedCustomerCount = value;
    }
    public DateTime? LastPaymentDate { get; set; }
}

public sealed class DirectorDelegateFollowupDayAmountDto
{
    public DateTime Date { get; set; }
    public double Amount { get; set; }
}

public sealed class DirectorDelegateFollowupDayRatingDto
{
    public DateTime Date { get; set; }
    public double Score { get; set; }
    public string Label { get; set; } = "";
    public int? BoardOperatorScore { get; set; }
    public string? BoardOperatorNote { get; set; }
}

public sealed class DirectorDelegateFollowupConversionFromDto
{
    public string FromLabel { get; set; } = "";
    public int Count { get; set; }
}

public sealed class DirectorDelegateFollowupConversionCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string FromLabel { get; set; } = "";
}

public sealed class DirectorDelegateFollowupConversionTargetDayDto
{
    public DateTime Date { get; set; }
    public int ArrivedCount { get; set; }
    public IReadOnlyList<DirectorDelegateFollowupConversionFromDto> FromBreakdown { get; set; } =
        Array.Empty<DirectorDelegateFollowupConversionFromDto>();
    public IReadOnlyList<DirectorDelegateFollowupConversionCustomerDto> Customers { get; set; } =
        Array.Empty<DirectorDelegateFollowupConversionCustomerDto>();
}

public sealed class DirectorDelegateFollowupConversionTargetDto
{
    public string TargetLabel { get; set; } = "";
    public int ArrivedCount { get; set; }
    public string DetailText { get; set; } = "";
    public IReadOnlyList<DirectorDelegateFollowupConversionFromDto> FromBreakdown { get; set; } =
        Array.Empty<DirectorDelegateFollowupConversionFromDto>();
    public IReadOnlyList<DirectorDelegateFollowupConversionCustomerDto> Customers { get; set; } =
        Array.Empty<DirectorDelegateFollowupConversionCustomerDto>();
    public IReadOnlyList<DirectorDelegateFollowupConversionTargetDayDto> Last30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupConversionTargetDayDto>();
}

public sealed class DirectorDelegateFollowupConversionDto
{
    public int Improved { get; set; }
    public int Declined { get; set; }
    public int Unchanged { get; set; }
    public string SummaryText { get; set; } = "";
    public IReadOnlyList<DirectorDelegateFollowupConversionTargetDto> Targets { get; set; } =
        Array.Empty<DirectorDelegateFollowupConversionTargetDto>();
}

public sealed class DirectorDelegateFollowupTierMoneyDto
{
    public string Label { get; set; } = "";
    /// <summary>Required due installment for this tier (non-zeroed excl. legal).</summary>
    public double RequiredDue { get; set; }
    public double CollectedYesterday { get; set; }
    public IReadOnlyList<DirectorDelegateFollowupDayAmountDto> CollectedLast30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayAmountDto>();
}

public sealed class DirectorDelegateFollowupBoardOperatorRatingDto
{
    public int ListId { get; set; }
    public DateTime RatingDate { get; set; }
    public int? Score { get; set; }
    public string? Note { get; set; }
    public string? RatedByUserName { get; set; }
    public bool HasRating => Score is >= 0;
}

public sealed class DirectorDelegateFollowupUpsertBoardOperatorRatingRequest
{
    public int Score { get; set; }
    public string? Note { get; set; }
}

public sealed class DirectorDelegateFollowupListSummaryDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";

    public double RealRating { get; set; }
    public string RealRatingLabel { get; set; } = "";
    public double RealRatingWeekAgo { get; set; }
    public string RatingTrend { get; set; } = DirectorFollowerOpsTrendRules.Flat;

    /// <summary>Compat aliases for older FE — map to real rating.</summary>
    public double PhantomRating
    {
        get => RealRating;
        set => RealRating = value;
    }
    public string PhantomRatingLabel
    {
        get => RealRatingLabel;
        set => RealRatingLabel = value;
    }
    public double PhantomRatingWeekAgo
    {
        get => RealRatingWeekAgo;
        set => RealRatingWeekAgo = value;
    }

    public int NonZeroedCustomerCount { get; set; }
    public int LegalCustomerCount { get; set; }

    /// <summary>Non-zeroed (open balance) tier counts — real rating.</summary>
    public int ExcellentCount { get; set; }
    public int GoodCount { get; set; }
    public int WeakCount { get; set; }
    public int LaggingCount { get; set; }
    public int BadCount { get; set; }

    public int RemainingLt100Count { get; set; }
    public int Remaining100To200Count { get; set; }
    public int RemainingGe100Count { get; set; }

    /// <summary>Sum of day installments for non-zeroed excl. legal.</summary>
    public double RequiredDueToday { get; set; }
    public double CollectedYesterday { get; set; }
    public double CollectedLastFriday { get; set; }
    public DateTime LastFridayDate { get; set; }
    public IReadOnlyList<DirectorDelegateFollowupDayAmountDto> CollectedLast30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayAmountDto>();
    public IReadOnlyList<DirectorDelegateFollowupDayAmountDto> CollectedLast30Fridays { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayAmountDto>();
    public IReadOnlyList<DirectorDelegateFollowupTierMoneyDto> TierMoney { get; set; } =
        Array.Empty<DirectorDelegateFollowupTierMoneyDto>();

    public IReadOnlyList<DirectorDelegateFollowupDayRatingDto> RealRatingLast30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayRatingDto>();
    public IReadOnlyList<DirectorDelegateFollowupDayRatingDto> BoardOperatorLast30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayRatingDto>();
    public IReadOnlyList<DirectorDelegateFollowupDayRatingDto> FollowerCommitteeLast30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayRatingDto>();
    public IReadOnlyList<DirectorDelegateFollowupDayRatingDto> SupervisionCommitteeLast30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayRatingDto>();
    public IReadOnlyList<DirectorDelegateFollowupDayRatingDto> SalesDelegateLast30Days { get; set; } =
        Array.Empty<DirectorDelegateFollowupDayRatingDto>();

    public DirectorDelegateFollowupConversionDto Conversion { get; set; } = new();

    public DirectorDelegateFollowupBoardOperatorRatingDto? BoardOperatorRating { get; set; }
    /// <summary>Latest follower day rating (0–10) after إنهاء الجباية.</summary>
    public DirectorDelegateFollowupBoardOperatorRatingDto? FollowerCommitteeRating { get; set; }
    /// <summary>Latest BM visit rating (0–10) before مغادرة الفرع.</summary>
    public DirectorDelegateFollowupBoardOperatorRatingDto? SupervisionCommitteeRating { get; set; }
    /// <summary>Latest sales-employee shop rating mapped onto the list (scale 1–5).</summary>
    public DirectorDelegateFollowupBoardOperatorRatingDto? SalesDelegateRating { get; set; }

    // Legacy unused by new board (kept so older clients don't break JSON shape).
    public int ActiveCustomerCount { get; set; }
    public int ActiveCustomerCountWeekAgo { get; set; }
    public string ActiveCountTrend { get; set; } = DirectorFollowerOpsTrendRules.Flat;
}

/// <summary>
/// Open-balance customer row for follow-up table, enriched with phantom rating band + active flag
/// so the FE can filter cards client-side without a second load.
/// </summary>
public sealed class DirectorDelegateFollowupCustomerDto
{
    public int? CustomerID { get; set; }
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
    /// <summary>Amount of the latest payment (same date as <see cref="LastPaymentDate"/>).</summary>
    public double? LastPaymentAmount { get; set; }
    public int? CountReceiptDevice { get; set; }
    public int? NumberOfDayDevice { get; set; }
    public int? ReceiptRateDevice { get; set; }
    /// <summary>Real rating label: ممتاز / جيد / ضعيف / متلكئ / سيء.</summary>
    public string PhantomRatingLabel { get; set; } = "";
    public string RealRatingLabel
    {
        get => PhantomRatingLabel;
        set => PhantomRatingLabel = value;
    }
    public bool IsLegal { get; set; }
    /// <summary>Deprecated on board — kept for row compatibility.</summary>
    public bool IsActiveRating { get; set; }
}

public interface IDirectorDelegateFollowupService
{
    Task<IReadOnlyList<DirectorDelegateFollowupListCardDto>> GetActiveListsAsync(CancellationToken ct);
    Task<DirectorDelegateFollowupListSummaryDto?> GetListSummaryAsync(int listId, CancellationToken ct);
    Task<IReadOnlyList<DirectorDelegateFollowupCustomerDto>> GetListCustomersAsync(int listId, CancellationToken ct);
    Task<DirectorDelegateFollowupBoardOperatorRatingDto?> GetBoardOperatorRatingAsync(
        int listId, DateTime? ratingDate, CancellationToken ct);
    Task<(bool Ok, string? Error, DirectorDelegateFollowupBoardOperatorRatingDto? Row)> UpsertBoardOperatorRatingAsync(
        int listId, int score, string? note, string? ratedByUserName, CancellationToken ct);

    /// <summary>BM visit ratings mirrored into company for the board card.</summary>
    Task<(bool Ok, string? Error)> UpsertSupervisionRatingsAsync(
        IReadOnlyList<(int ListId, int Score, string? Note, string? RatedBy)> ratings,
        DateTime? ratingDate,
        CancellationToken ct);
}

/// <summary>
/// Director/monitor «متابعة المندوبين»: active lists (40-day payment), phantom rating, customers.
/// </summary>
public sealed class DirectorDelegateFollowupService : IDirectorDelegateFollowupService
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly IRatingDataSource _rating;
    private readonly ICustomersRepository _customers;

    public DirectorDelegateFollowupService(
        SalesDevelopmentGuard guard,
        IRatingDataSource rating,
        ICustomersRepository customers)
    {
        _guard = guard;
        _rating = rating;
        _customers = customers;
    }

    public async Task<IReadOnlyList<DirectorDelegateFollowupListCardDto>> GetActiveListsAsync(CancellationToken ct)
    {
        var asOf = IraqToday();
        var activeLists = await LoadListsWithRecentPaymentAsync(asOf, ct);
        if (activeLists.Count == 0)
        {
            return [];
        }

        var listIds = activeLists.Select(x => x.ListId).ToList();
        var openBalance = await LoadOpenBalanceFactsAsync(listIds, ct);
        var byList = openBalance
            .GroupBy(f => f.DelegateId ?? 0)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CustomerRatingFacts>)g.ToList());

        var cards = new List<DirectorDelegateFollowupListCardDto>(activeLists.Count);
        foreach (var row in activeLists)
        {
            byList.TryGetValue(row.ListId, out var facts);
            facts ??= Array.Empty<CustomerRatingFacts>();
            var real = EmployeeSalesRating.Snapshot(facts, asOf, usePhantomSaleDate: false);
            cards.Add(new DirectorDelegateFollowupListCardDto
            {
                ListId = row.ListId,
                ListName = row.ListName,
                RealRating = real.AverageScore,
                RealRatingLabel = real.RatingLabel,
                NonZeroedCustomerCount = facts.Count,
                LastPaymentDate = row.LastPaymentDate
            });
        }

        return cards
            .OrderByDescending(c => c.LastPaymentDate)
            .ThenBy(c => c.ListName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<DirectorDelegateFollowupListSummaryDto?> GetListSummaryAsync(int listId, CancellationToken ct)
    {
        if (listId <= 0)
        {
            return null;
        }

        var asOf = IraqToday();
        var weekAgo = DirectorFollowerOpsTrendRules.WeekAgoDate(asOf);
        var lastFriday = DirectorDelegateFollowupRules.LastFridayBefore(asOf);
        var historyDays = DirectorDelegateFollowupRules.LastNCalendarDaysEndingYesterday(
            asOf, DirectorDelegateFollowupRules.CollectedHistoryDays);
        var name = await LoadListNameAsync(listId, ct);
        if (name is null)
        {
            return null;
        }

        var openBalance = await LoadOpenBalanceFactsAsync([listId], ct);
        var daySales = await LoadAmountDaySalesForListAsync(listId, ct);
        var real = EmployeeSalesRating.Snapshot(openBalance, asOf, usePhantomSaleDate: false);
        var weekReal = EmployeeSalesRating.Snapshot(openBalance, weekAgo, usePhantomSaleDate: false);
        var remainingBands = DirectorDelegateFollowupRules.CountRemainingBands(
            openBalance.Select(f => f.AmountRemaining));

        var rated = CustomerRatingCalculator
            .EvaluateMany(openBalance, asOf, usePhantomSaleDate: false)
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.First());

        var dueRows = openBalance.Select(f =>
        {
            daySales.TryGetValue(f.CustomerId, out var day);
            return (day, f.AmountRemaining, f.IsLegal);
        });
        var requiredDue = DirectorDelegateFollowupRules.SumRequiredDueInstallment(dueRows);

        var fridayHistory = DirectorDelegateFollowupRules.LastNFridays(
            asOf, DirectorDelegateFollowupRules.CollectedHistoryDays);
        var historyFrom = historyDays.Count > 0 ? historyDays[^1] : asOf.AddDays(-1);
        var historyTo = historyDays.Count > 0 ? historyDays[0] : asOf.AddDays(-1);
        var fridayFrom = fridayHistory.Count > 0 ? fridayHistory[^1] : lastFriday;
        var rangeFrom = fridayFrom < historyFrom ? fridayFrom : historyFrom;
        // Need day-before oldest history day for conversion pairs.
        var ratingFrom = historyFrom.AddDays(-1);
        if (ratingFrom < rangeFrom)
            rangeFrom = ratingFrom;

        var paymentRows = await LoadCollectedPaymentRowsAsync(listId, rangeFrom, historyTo, ct);

        var paymentsByDay = paymentRows
            .GroupBy(p => p.PaymentDate.Date)
            .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Amount), 0, MidpointRounding.AwayFromZero));

        double DayAmount(DateTime d) =>
            paymentsByDay.TryGetValue(d.Date, out var a) ? a : 0;

        var last30 = historyDays
            .Select(d => new DirectorDelegateFollowupDayAmountDto
            {
                Date = d,
                Amount = DayAmount(d)
            })
            .ToList();

        var last30Fridays = fridayHistory
            .Select(d => new DirectorDelegateFollowupDayAmountDto
            {
                Date = d,
                Amount = DayAmount(d)
            })
            .ToList();

        var labelByCustomer = rated.ToDictionary(
            kv => kv.Key,
            kv => CustomerRatingLabels.FromLevel(kv.Value.Level));

        // Labels as-of each day (incl. today + day before history) for conversion.
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

        var realRatingLast30 = historyDays.Select(d =>
        {
            var snap = EmployeeSalesRating.Snapshot(openBalance, d, usePhantomSaleDate: false);
            return new DirectorDelegateFollowupDayRatingDto
            {
                Date = d,
                Score = snap.AverageScore,
                Label = snap.RatingLabel
            };
        }).ToList();

        var boardOpByDay = await LoadBoardOperatorRatingsRangeAsync(listId, historyFrom, asOf, ct);
        var boardOpLast30 = historyDays.Select(d =>
        {
            boardOpByDay.TryGetValue(d.Date, out var row);
            return new DirectorDelegateFollowupDayRatingDto
            {
                Date = d,
                Score = row?.Score ?? 0,
                Label = row is null ? "" : $"{row.Score}/10",
                BoardOperatorScore = row?.Score,
                BoardOperatorNote = row?.Note
            };
        }).ToList();

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

        var customerNames = await LoadCustomerNamesForListAsync(listId, ct);

        List<DirectorDelegateFollowupConversionCustomerDto> ArrivalCustomers(
            DateTime day, string target)
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
                    FromLabel = prev
                });
            }

            return list
                .OrderBy(x => x.CustomerName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        var todayConv = DirectorDelegateFollowupRules.CountConversions(PairsForDay(asOf));
        var todayArrivals = DirectorDelegateFollowupRules.BuildArrivalBreakdown(
            PairsForDay(asOf), conversionTargets);

        static string FormatArrivalDetail(
            IReadOnlyList<(string From, int Count)> breakdown)
        {
            if (breakdown.Count == 0)
                return "لا متحولين اليوم";
            return string.Join(" · ",
                breakdown.Select(x => $"من {x.From}: {x.Count}"));
        }

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
                            Count = x.Count
                        })
                        .ToList(),
                    Customers = dayCustomers
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
                        Count = x.Count
                    })
                    .ToList(),
                Customers = todayCustomers,
                Last30Days = targetHistory
            });
        }

        var conversion = new DirectorDelegateFollowupConversionDto
        {
            Improved = todayConv.Improved,
            Declined = todayConv.Declined,
            Unchanged = todayConv.Unchanged,
            SummaryText =
                $"ازداد {todayConv.Improved} · نقص {todayConv.Declined} · مساوي {todayConv.Unchanged}",
            Targets = conversionTargetDtos
        };

        var tierLabels = new[]
        {
            CustomerRatingLabels.Excellent,
            CustomerRatingLabels.Good,
            CustomerRatingLabels.Weak,
            CustomerRatingLabels.Rejected,
            CustomerRatingLabels.Bad,
        };
        var tierMoney = new List<DirectorDelegateFollowupTierMoneyDto>(tierLabels.Length);
        foreach (var label in tierLabels)
        {
            var tierFacts = openBalance.Where(f =>
                labelByCustomer.TryGetValue(f.CustomerId, out var lb) && lb == label).ToList();

            var tierDue = DirectorDelegateFollowupRules.SumRequiredDueInstallment(
                tierFacts.Select(f =>
                {
                    daySales.TryGetValue(f.CustomerId, out var day);
                    return (day, f.AmountRemaining, f.IsLegal);
                }));

            var tierIds = tierFacts.Select(f => f.CustomerId).ToHashSet();
            var tierByDay = paymentRows
                .Where(p => tierIds.Contains(p.CustomerId))
                .GroupBy(p => p.PaymentDate.Date)
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(x => x.Amount), 0, MidpointRounding.AwayFromZero));

            double TierDay(DateTime d) =>
                tierByDay.TryGetValue(d.Date, out var a) ? a : 0;

            tierMoney.Add(new DirectorDelegateFollowupTierMoneyDto
            {
                Label = label,
                RequiredDue = tierDue,
                CollectedYesterday = TierDay(asOf.AddDays(-1)),
                CollectedLast30Days = historyDays
                    .Select(d => new DirectorDelegateFollowupDayAmountDto { Date = d, Amount = TierDay(d) })
                    .ToList()
            });
        }

        var boardOp = await GetBoardOperatorRatingAsync(listId, asOf, ct);

        var followerRows = await LoadFollowerDayRatingsAsync(listId, historyFrom, asOf, ct);
        var supervisionRows = await LoadSupervisionDayRatingsAsync(listId, historyFrom, asOf, ct);
        var salesRows = await LoadSalesDelegateRatingsAsync(listId, historyFrom, asOf, ct);

        var followerToday = DirectorInputRatingSlots.PickLatest(followerRows, asOf);
        if (followerToday != null) followerToday.ListId = listId;
        var supervisionToday = DirectorInputRatingSlots.PickLatest(supervisionRows, asOf);
        if (supervisionToday != null) supervisionToday.ListId = listId;
        var salesToday = DirectorInputRatingSlots.PickLatest(salesRows, asOf);
        if (salesToday != null) salesToday.ListId = listId;

        return new DirectorDelegateFollowupListSummaryDto
        {
            ListId = listId,
            ListName = name,
            RealRating = real.AverageScore,
            RealRatingLabel = real.RatingLabel,
            RealRatingWeekAgo = weekReal.AverageScore,
            RatingTrend = DirectorFollowerOpsTrendRules.ComputeScoreTrend(
                real.AverageScore, weekReal.AverageScore),
            NonZeroedCustomerCount = openBalance.Count,
            LegalCustomerCount = openBalance.Count(f => f.IsLegal),
            ExcellentCount = real.ExcellentCount,
            GoodCount = real.GoodCount,
            WeakCount = real.WeakCount,
            LaggingCount = real.RejectedCount,
            BadCount = real.BadCount,
            RemainingLt100Count = remainingBands.Lt100k,
            Remaining100To200Count = remainingBands.From100To200k,
            RemainingGe100Count = remainingBands.Ge100k,
            RequiredDueToday = requiredDue,
            CollectedYesterday = DayAmount(asOf.AddDays(-1)),
            CollectedLastFriday = DayAmount(lastFriday),
            LastFridayDate = lastFriday,
            CollectedLast30Days = last30,
            CollectedLast30Fridays = last30Fridays,
            TierMoney = tierMoney,
            RealRatingLast30Days = realRatingLast30,
            BoardOperatorLast30Days = boardOpLast30,
            FollowerCommitteeLast30Days = DirectorInputRatingSlots.BuildLast30Days(followerRows, historyDays),
            SupervisionCommitteeLast30Days = DirectorInputRatingSlots.BuildLast30Days(supervisionRows, historyDays),
            SalesDelegateLast30Days = DirectorInputRatingSlots.BuildLast30Days(salesRows, historyDays),
            Conversion = conversion,
            BoardOperatorRating = boardOp,
            FollowerCommitteeRating = followerToday,
            SupervisionCommitteeRating = supervisionToday,
            SalesDelegateRating = salesToday,
            ActiveCustomerCount = openBalance.Count,
            ActiveCustomerCountWeekAgo = 0,
            ActiveCountTrend = DirectorFollowerOpsTrendRules.Flat
        };
    }

    public async Task<IReadOnlyList<DirectorDelegateFollowupCustomerDto>> GetListCustomersAsync(
        int listId, CancellationToken ct)
    {
        if (listId <= 0)
        {
            return [];
        }

        // Parallel: customers SP + rating facts + last-payment amounts (view has AmountDenar).
        var asOf = IraqToday();
        var rowsTask = _customers.Customers_GetAll(listId, null, "الغير مصفرين");
        var factsTask = LoadOpenBalanceFactsAsync([listId], ct);
        var lastPayTask = LoadLastPaymentAmountsForListAsync(listId, ct);
        await Task.WhenAll(rowsTask, factsTask, lastPayTask);

        var rows = (await rowsTask ?? []).ToList();
        var openBalance = await factsTask;
        var lastPayAmounts = await lastPayTask;
        var factsById = openBalance.ToDictionary(f => f.CustomerId, f => f);
        var ratedById = CustomerRatingCalculator
            .EvaluateMany(openBalance, asOf, usePhantomSaleDate: false)
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<DirectorDelegateFollowupCustomerDto>(rows.Count);
        foreach (var r in rows)
        {
            var id = r.CustomerID ?? 0;
            ratedById.TryGetValue(id, out var rated);
            factsById.TryGetValue(id, out var fact);
            lastPayAmounts.TryGetValue(id, out var lastAmt);
            result.Add(new DirectorDelegateFollowupCustomerDto
            {
                CustomerID = r.CustomerID,
                DelegateID = r.DelegateID,
                CustomerName = r.CustomerName,
                Address = r.Address,
                Notes = r.Notes,
                PhoneNumber = r.PhoneNumber,
                ShopName = r.ShopName,
                SaleName = r.SaleName,
                ReceiptName = r.ReceiptName,
                DelegateName = r.DelegateName,
                DateSaleDevice = r.DateSaleDevice,
                AmountTotalSales = r.AmountTotalSales,
                AmountDaySales = r.AmountDaySales,
                ReceiptsTotal = r.ReceiptsTotal,
                AmountRemaining = r.AmountRemaining,
                ItemsNames = r.ItemsNames,
                LastPaymentDate = r.LastPaymentDate,
                LastPaymentAmount = lastAmt > 0 ? lastAmt : null,
                CountReceiptDevice = r.CountReceiptDevice,
                NumberOfDayDevice = r.NumberOfDayDevice,
                ReceiptRateDevice = r.ReceiptRateDevice,
                PhantomRatingLabel = rated is null
                    ? ""
                    : CustomerRatingLabels.FromLevel(rated.Level),
                IsLegal = fact?.IsLegal == true,
                IsActiveRating = ActiveCustomerRules.IsActive(r.LastPaymentDate, asOf),
            });
        }

        return result;
    }

    public async Task<DirectorDelegateFollowupBoardOperatorRatingDto?> GetBoardOperatorRatingAsync(
        int listId, DateTime? ratingDate, CancellationToken ct)
    {
        if (listId <= 0)
            return null;

        var day = (ratingDate ?? IraqToday()).Date;
        await EnsureBoardOperatorRatingsTableAsync(ct);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var row = await connection.QueryFirstOrDefaultAsync<BoardOpRow>(
            new CommandDefinition(@"
SELECT ListId, RatingDate, Score, Note, RatedByUserName
FROM dbo.DirectorBoardOperatorListRatings
WHERE ListId = @ListId AND RatingDate = @RatingDate;",
                new { ListId = listId, RatingDate = day },
                cancellationToken: ct));

        if (row is null)
        {
            return new DirectorDelegateFollowupBoardOperatorRatingDto
            {
                ListId = listId,
                RatingDate = day,
                Score = null,
                Note = null
            };
        }

        return new DirectorDelegateFollowupBoardOperatorRatingDto
        {
            ListId = row.ListId,
            RatingDate = row.RatingDate.Date,
            Score = row.Score,
            Note = row.Note,
            RatedByUserName = row.RatedByUserName
        };
    }

    private sealed class BoardOpRow
    {
        public int ListId { get; init; }
        public DateTime RatingDate { get; init; }
        public int Score { get; init; }
        public string? Note { get; init; }
        public string? RatedByUserName { get; init; }
    }

    public async Task<(bool Ok, string? Error, DirectorDelegateFollowupBoardOperatorRatingDto? Row)>
        UpsertBoardOperatorRatingAsync(
            int listId, int score, string? note, string? ratedByUserName, CancellationToken ct)
    {
        if (listId <= 0)
            return (false, "معرّف القائمة غير صالح", null);
        if (!DirectorDelegateFollowupRules.IsValidBoardOperatorScore(score))
            return (false, "التقييم يجب أن يكون بين 0 و 10", null);

        var day = IraqToday();
        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleanNote is { Length: > 500 })
            cleanNote = cleanNote[..500];

        await EnsureBoardOperatorRatingsTableAsync(ct);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.DirectorBoardOperatorListRatings AS t
USING (SELECT @ListId AS ListId, @RatingDate AS RatingDate) AS s
ON t.ListId = s.ListId AND t.RatingDate = s.RatingDate
WHEN MATCHED THEN UPDATE SET
  Score = @Score,
  Note = @Note,
  RatedByUserName = @RatedByUserName,
  CreatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (ListId, RatingDate, Score, Note, RatedByUserName, CreatedAtUtc)
VALUES (@ListId, @RatingDate, @Score, @Note, @RatedByUserName, SYSUTCDATETIME());",
            new
            {
                ListId = listId,
                RatingDate = day,
                Score = score,
                Note = cleanNote,
                RatedByUserName = string.IsNullOrWhiteSpace(ratedByUserName) ? null : ratedByUserName.Trim()
            },
            cancellationToken: ct));

        var row = await GetBoardOperatorRatingAsync(listId, day, ct);
        return (true, null, row);
    }

    /// <summary>
    /// Latest payment amount per customer on a list.
    /// Uses View_CustomersPaymentsDelegate (AmountDenar) — base CustomersPayments has no amount column.
    /// </summary>
    private async Task<Dictionary<int, double>> LoadLastPaymentAmountsForListAsync(
        int listId, CancellationToken ct)
    {
        if (listId <= 0)
        {
            return new Dictionary<int, double>();
        }

        try
        {
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<(int CustomerId, double Amount)>(
                new CommandDefinition(@"
SELECT
  v.CustomerID AS CustomerId,
  CAST(ISNULL(v.AmountDenar, 0) AS float) AS Amount
FROM dbo.View_CustomersPaymentsDelegate v
INNER JOIN (
  SELECT CustomerID, MAX(PaymentDate) AS LastPaymentDate
  FROM dbo.View_CustomersPaymentsDelegate
  WHERE DelegateID = @ListId
  GROUP BY CustomerID
) latest
  ON latest.CustomerID = v.CustomerID
 AND v.PaymentDate = latest.LastPaymentDate
WHERE v.DelegateID = @ListId;",
                    new { ListId = listId },
                    cancellationToken: ct));

            var map = new Dictionary<int, double>();
            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.CustomerId, out var prev) || row.Amount > prev)
                {
                    map[row.CustomerId] = row.Amount;
                }
            }

            return map;
        }
        catch
        {
            // Non-fatal: table still loads without last-payment amount.
            return new Dictionary<int, double>();
        }
    }

    private async Task<List<(int ListId, string ListName, DateTime? LastPaymentDate)>> LoadListsWithRecentPaymentAsync(
        DateTime asOf, CancellationToken ct)
    {
        var cs = RequireCs();
        var cutoff = asOf.Date.AddDays(-DirectorDelegateFollowupRules.MaxDaysListActivity);
        await using var connection = new SqlConnection(cs);
        var rows = await connection.QueryAsync<(int ListId, string? ListName, DateTime? LastPaymentDate)>(
            new CommandDefinition(@"
SELECT
  d.DelegateID AS ListId,
  LTRIM(RTRIM(ISNULL(d.DelegateName, N''))) AS ListName,
  MAX(cp.PaymentDate) AS LastPaymentDate
FROM dbo.Delegates d
INNER JOIN dbo.Customers c ON c.DelegateID = d.DelegateID
INNER JOIN dbo.CustomersPayments cp ON cp.CustomerID = c.CustomerID
WHERE ISNULL(d.DelegateState, 1) = 1
GROUP BY d.DelegateID, d.DelegateName
HAVING MAX(cp.PaymentDate) IS NOT NULL
   AND CONVERT(date, MAX(cp.PaymentDate)) >= @Cutoff
ORDER BY MAX(cp.PaymentDate) DESC;",
                new { Cutoff = cutoff },
                cancellationToken: ct));

        return rows
            .Where(r => DirectorDelegateFollowupRules.IsListActive(r.LastPaymentDate, asOf))
            .Select(r => (
                r.ListId,
                string.IsNullOrWhiteSpace(r.ListName) ? $"قائمة {r.ListId}" : r.ListName!.Trim(),
                r.LastPaymentDate))
            .ToList();
    }

    private async Task<string?> LoadListNameAsync(int listId, CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var name = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 LTRIM(RTRIM(ISNULL(DelegateName, N'')))
FROM dbo.Delegates WHERE DelegateID = @ListId;",
            new { ListId = listId }, cancellationToken: ct));
        if (name is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(name) ? $"قائمة {listId}" : name;
    }

    private async Task<List<CustomerRatingFacts>> LoadOpenBalanceFactsAsync(
        IReadOnlyList<int> listIds, CancellationToken ct)
    {
        if (listIds.Count == 0)
        {
            return [];
        }

        var facts = await _rating.GetFactsByListIdsAsync(listIds, ct);
        return facts
            .Where(f => f.AmountRemaining > 0)
            .Select(f => f with { RatingEquationFactor = 1.0 })
            .ToList();
    }

    private async Task<Dictionary<int, double>> LoadAmountDaySalesForListAsync(int listId, CancellationToken ct)
    {
        if (listId <= 0)
            return new Dictionary<int, double>();

        try
        {
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<(int CustomerId, double AmountDaySales)>(
                new CommandDefinition(@"
SELECT c.CustomerID AS CustomerId,
       CAST(ISNULL(vc.AmountDaySales, 0) AS float) AS AmountDaySales
FROM dbo.Customers c
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
WHERE c.DelegateID = @ListId
  AND ISNULL(vc.AmountRemaining, 0) > 0;",
                    new { ListId = listId },
                    cancellationToken: ct));
            return rows.ToDictionary(r => r.CustomerId, r => r.AmountDaySales);
        }
        catch
        {
            return new Dictionary<int, double>();
        }
    }

    private async Task<Dictionary<int, string>> LoadCustomerNamesForListAsync(int listId, CancellationToken ct)
    {
        if (listId <= 0)
            return new Dictionary<int, string>();

        try
        {
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<(int CustomerId, string? Name)>(
                new CommandDefinition(@"
SELECT CustomerID AS CustomerId,
       LTRIM(RTRIM(ISNULL(CustomerName, N''))) AS Name
FROM dbo.Customers
WHERE DelegateID = @ListId;",
                    new { ListId = listId },
                    cancellationToken: ct));
            return rows
                .GroupBy(r => r.CustomerId)
                .ToDictionary(g => g.Key, g => g.First().Name ?? "");
        }
        catch
        {
            return new Dictionary<int, string>();
        }
    }

    private async Task<List<(int CustomerId, DateTime PaymentDate, double Amount)>> LoadCollectedPaymentRowsAsync(
        int listId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        if (listId <= 0)
            return [];

        var from = fromDate.Date;
        var to = toDate.Date;
        if (to < from)
            (from, to) = (to, from);

        try
        {
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            try
            {
                var rows = await connection.QueryAsync<(int CustomerId, DateTime PaymentDate, double Amount)>(
                    new CommandDefinition(@"
SELECT CustomerID AS CustomerId,
       CONVERT(date, PaymentDate) AS PaymentDate,
       CAST(ISNULL(AmountDenar, 0) AS float) AS Amount
FROM dbo.View_CustomersPayments
WHERE DelegateID = @ListId
  AND CONVERT(date, PaymentDate) >= @FromDate
  AND CONVERT(date, PaymentDate) <= @ToDate;",
                        new { ListId = listId, FromDate = from, ToDate = to },
                        cancellationToken: ct));
                return rows.ToList();
            }
            catch (SqlException)
            {
                var rows = await connection.QueryAsync<(int CustomerId, DateTime PaymentDate, double Amount)>(
                    new CommandDefinition(@"
SELECT r.CustomerID AS CustomerId,
       CONVERT(date, r.PaymentDate) AS PaymentDate,
       CAST(ISNULL(r.AmountDenar, 0) AS float) AS Amount
FROM dbo.View_ReceiptCustomerDate r
INNER JOIN dbo.Customers c ON c.CustomerID = r.CustomerID
WHERE c.DelegateID = @ListId
  AND CONVERT(date, r.PaymentDate) >= @FromDate
  AND CONVERT(date, r.PaymentDate) <= @ToDate;",
                        new { ListId = listId, FromDate = from, ToDate = to },
                        cancellationToken: ct));
                return rows.ToList();
            }
        }
        catch
        {
            return [];
        }
    }

    public async Task<(bool Ok, string? Error)> UpsertSupervisionRatingsAsync(
        IReadOnlyList<(int ListId, int Score, string? Note, string? RatedBy)> ratings,
        DateTime? ratingDate,
        CancellationToken ct)
    {
        if (ratings.Count == 0)
            return (true, null);

        var day = (ratingDate ?? IraqToday()).Date;
        try
        {
            await EnsureSupervisionRatingsTableAsync(ct);
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            foreach (var r in ratings)
            {
                if (r.ListId <= 0 || r.Score is < 0 or > 10)
                    continue;
                await connection.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.ListSupervisionDayRatings AS t
USING (SELECT @ListId AS ListId, @RatingDate AS RatingDate) AS s
ON t.ListId = s.ListId AND t.RatingDate = s.RatingDate
WHEN MATCHED THEN UPDATE SET
  Score = @Score,
  Note = @Note,
  RatedByUserName = @RatedBy,
  CreatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (ListId, RatingDate, Score, Note, RatedByUserName, CreatedAtUtc)
VALUES (@ListId, @RatingDate, @Score, @Note, @RatedBy, SYSUTCDATETIME());",
                    new
                    {
                        ListId = r.ListId,
                        RatingDate = day,
                        Score = r.Score,
                        Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim(),
                        RatedBy = string.IsNullOrWhiteSpace(r.RatedBy) ? null : r.RatedBy.Trim(),
                    },
                    cancellationToken: ct));
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task EnsureFollowerDayRatingTableAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerDelegateDayRating', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerDelegateDayRating (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerDelegateDayRating_DFU PRIMARY KEY,
        FollowerId INT NOT NULL,
        ListId INT NOT NULL,
        CityValue NVARCHAR(100) NULL,
        IraqDate DATE NOT NULL,
        Score INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FDDR_DFU_At DEFAULT (SYSUTCDATETIME())
    );
END", cancellationToken: ct));
    }

    private async Task EnsureSupervisionRatingsTableAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.ListSupervisionDayRatings', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.ListSupervisionDayRatings (
    ListId INT NOT NULL,
    RatingDate DATE NOT NULL,
    Score INT NOT NULL,
    Note NVARCHAR(1000) NULL,
    RatedByUserName NVARCHAR(100) NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_ListSupervision_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ListSupervisionDayRatings PRIMARY KEY (ListId, RatingDate)
  );
END", cancellationToken: ct));
    }

    private async Task EnsureSalesEmployeeListDayRatingTableAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesEmployeeListDayRating', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.SalesEmployeeListDayRating (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesEmployeeListDayRating_DFU PRIMARY KEY,
    EmployeeId INT NOT NULL,
    ListId INT NOT NULL,
    IraqDate DATE NOT NULL,
    Score INT NOT NULL,
    Note NVARCHAR(1000) NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SELDR_DFU_At DEFAULT SYSUTCDATETIME()
  );
END
IF OBJECT_ID(N'dbo.SalesEmployeeListDayRating', N'U') IS NOT NULL
AND NOT EXISTS (
  SELECT 1 FROM sys.indexes
  WHERE name = N'UX_SELDR_Employee_List_Day'
    AND object_id = OBJECT_ID(N'dbo.SalesEmployeeListDayRating'))
BEGIN
  EXEC(N'CREATE UNIQUE INDEX UX_SELDR_Employee_List_Day
      ON dbo.SalesEmployeeListDayRating (EmployeeId, ListId, IraqDate)');
END", cancellationToken: ct));
    }

    private async Task<IReadOnlyList<DirectorInputRatingRow>> LoadFollowerDayRatingsAsync(
        int listId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        try
        {
            await EnsureFollowerDayRatingTableAsync(ct);
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<(int Score, string? Note, DateTime IraqDate, int FollowerId)>(
                new CommandDefinition(@"
SELECT Score, Note, IraqDate, FollowerId
FROM dbo.FollowerDelegateDayRating
WHERE ListId = @ListId
  AND IraqDate >= @FromDate
  AND IraqDate <= @ToDate;",
                    new { ListId = listId, FromDate = fromDate.Date, ToDate = toDate.Date },
                    cancellationToken: ct));
            return rows
                .Select(r => new DirectorInputRatingRow(r.Score, r.Note, r.IraqDate.Date, $"متابع {r.FollowerId}"))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<DirectorInputRatingRow>> LoadSupervisionDayRatingsAsync(
        int listId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        try
        {
            await EnsureSupervisionRatingsTableAsync(ct);
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<(int Score, string? Note, DateTime RatingDate, string? RatedBy)>(
                new CommandDefinition(@"
SELECT Score, Note, RatingDate, RatedByUserName AS RatedBy
FROM dbo.ListSupervisionDayRatings
WHERE ListId = @ListId
  AND RatingDate >= @FromDate
  AND RatingDate <= @ToDate;",
                    new { ListId = listId, FromDate = fromDate.Date, ToDate = toDate.Date },
                    cancellationToken: ct));
            return rows
                .Select(r => new DirectorInputRatingRow(r.Score, r.Note, r.RatingDate.Date, r.RatedBy))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task<IReadOnlyList<DirectorInputRatingRow>> LoadSalesDelegateRatingsAsync(
        int listId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        try
        {
            // ONLY path for «تقييم مندوب المبيعات»: SalesEmployeeListDayRating (0–10 after sale complete).
            _ = DirectorInputRatingSlots.SalesCardOnlyDayRatingMarker;
            await EnsureSalesEmployeeListDayRatingTableAsync(ct);
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<(int Score, string? Note, DateTime Day, string? ByName)>(
                new CommandDefinition(@"
SELECT
  CAST(ROUND(AVG(CAST(r.Score AS float)), 0) AS int) AS Score,
  MAX(r.Note) AS Note,
  r.IraqDate AS Day,
  CAST(NULL AS nvarchar(100)) AS ByName
FROM dbo.SalesEmployeeListDayRating r
WHERE r.ListId = @ListId
  AND r.IraqDate >= @FromDate
  AND r.IraqDate <= @ToDate
  AND r.Score BETWEEN 0 AND 10
GROUP BY r.IraqDate;",
                    new { ListId = listId, FromDate = fromDate.Date, ToDate = toDate.Date },
                    cancellationToken: ct));
            return rows
                .Where(r => r.Score is >= 0 and <= 10)
                .Select(r => new DirectorInputRatingRow(r.Score, r.Note, r.Day.Date, r.ByName))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private async Task EnsureBoardOperatorRatingsTableAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.DirectorBoardOperatorListRatings', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.DirectorBoardOperatorListRatings (
    ListId INT NOT NULL,
    RatingDate DATE NOT NULL,
    Score INT NOT NULL,
    Note NVARCHAR(500) NULL,
    RatedByUserName NVARCHAR(100) NULL,
    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DirectorBoardOp_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DirectorBoardOperatorListRatings PRIMARY KEY (ListId, RatingDate)
  );
END", cancellationToken: ct));
    }

    private async Task<Dictionary<DateTime, BoardOpRow>> LoadBoardOperatorRatingsRangeAsync(
        int listId, DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        var map = new Dictionary<DateTime, BoardOpRow>();
        if (listId <= 0)
            return map;

        try
        {
            await EnsureBoardOperatorRatingsTableAsync(ct);
            var cs = RequireCs();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<BoardOpRow>(new CommandDefinition(@"
SELECT ListId, RatingDate, Score, Note, RatedByUserName
FROM dbo.DirectorBoardOperatorListRatings
WHERE ListId = @ListId
  AND RatingDate >= @FromDate
  AND RatingDate <= @ToDate;",
                new { ListId = listId, FromDate = fromDate.Date, ToDate = toDate.Date },
                cancellationToken: ct));
            foreach (var row in rows)
                map[row.RatingDate.Date] = row;
        }
        catch
        {
            // leave empty
        }

        return map;
    }

    private static DateTime IraqToday() => DateTime.UtcNow.AddHours(3).Date;

    private string RequireCs()
    {
        var cs = _guard.GetSalesConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("لا يوجد اتصال بقاعدة بيانات الفرع.");
        }

        return cs;
    }
}
