using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class DirectorFollowerOpsSummaryDto
{
    public int FollowerId { get; set; }
    public string FollowerName { get; set; } = "";
    public int SalesAuthCount { get; set; }
    public int PendingSalesCount { get; set; }
    public int DecliningCount { get; set; }
    public int DecliningNotedCount { get; set; }
    public int NotesCount { get; set; }
    public int FieldFollowupPendingCount { get; set; }
    public int FieldFollowupDoneCount { get; set; }
    public int DelegateNotesCount { get; set; }
    public int CollectionPaymentCount { get; set; }
    public double CollectionPaymentSum { get; set; }
    public int CollectionEnabledLists { get; set; }
    public bool IsWorking { get; set; }
}

public sealed class DirectorFollowerSaleAuthRowDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    /// <summary>SalesDrafts.SaleId when known — used to dedupe one card per sale.</summary>
    public int? SaleDraftId { get; set; }
    /// <summary>CustomersSales.CustomerSaleID when known.</summary>
    public int? CustomerSaleId { get; set; }
    public bool Authenticated { get; set; }
    public DateTime? AuthenticatedAtUtc { get; set; }
    public int? AuthRating { get; set; }
    public string? AuthNote { get; set; }
    public int? ShopRating { get; set; }
    public string? ShopRatingNote { get; set; }
    /// <summary>قيمة بضاعة المحل من استمارة المتابع (مصادقة البيع).</summary>
    public double? ShopStockEstimatedValue { get; set; }
    /// <summary>قيمة الوارد اليومي من استمارة المتابع (مصادقة البيع).</summary>
    public double? EstimatedDailyRevenue { get; set; }
    /// <summary>قيمة بضاعة المحل من استمارة المندوب (أول جباية / طلب بيع).</summary>
    public double? DelegateShopStockEstimatedValue { get; set; }
    /// <summary>قيمة الوارد اليومي من استمارة المندوب.</summary>
    public double? DelegateEstimatedDailyRevenue { get; set; }
    /// <summary>قيمة الموجودات من استمارة موظف المبيعات (بروفايل المحل على المسودة).</summary>
    public double? EmployeeShopStockEstimatedValue { get; set; }
    /// <summary>الإيراد اليومي من استمارة موظف المبيعات.</summary>
    public double? EmployeeEstimatedDailyRevenue { get; set; }
    public string? CurrentRatingLabel { get; set; }
    public double? SalePrice { get; set; }
    public DateTime? SaleDate { get; set; }
    public double? DailyInstallment { get; set; }
    public string? SellerName { get; set; }
    public double? PaidAmount { get; set; }
    public double? RemainingAmount { get; set; }
}

public sealed class DirectorFollowerDecliningRowDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public string FromLabel { get; set; } = "";
    public string ToLabel { get; set; } = "";
    public string? RatingLabel { get; set; }
    public int FromScore { get; set; }
    public int ToScore { get; set; }
    public int DaysBetween { get; set; }
    public bool HasNote { get; set; }
    public DateTime? LastNoteAtUtc { get; set; }
    public bool HasSaleAuth { get; set; }
}

public sealed class DirectorFollowerNoteRowDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public string NoteText { get; set; } = "";
    public string? AuthorName { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public string Source { get; set; } = "customer";
}

public sealed class DirectorFollowerFieldFollowupRowDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public string Status { get; set; } = "";
    public string? CompletionNote { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool IsDone { get; set; }
}

/// <summary>زبائن قديمة = قضايا قانونية يرسلها مدير المبيعات للمتابع (LegalFollowupCases).</summary>
public sealed class DirectorFollowerLegalFollowupRowDto
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public int? SalesRequestId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Status { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string? Note { get; set; }
    public string? CompletionNote { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool IsDone { get; set; }
}

public sealed class DirectorFollowerDelegateNoteRowDto
{
    public int Id { get; set; }
    public int DelegateId { get; set; }
    public int? ListId { get; set; }
    public string? ListName { get; set; }
    public string NoteText { get; set; } = "";
    public string? AuthorName { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
}

public sealed class DirectorFollowerCollectionPaymentDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public double Amount { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string Source { get; set; } = "follower";
}

public sealed class DirectorFollowerCollectionListDto
{
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public bool FollowerCollectionEnabled { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? LastFollowerPaymentAt { get; set; }
    public double? LastFollowerPaymentAmount { get; set; }
    public DateTime? LastDelegatePaymentAt { get; set; }
    public double? LastDelegatePaymentAmount { get; set; }
}

public sealed class DirectorFollowerCollectionBundleDto
{
    public IReadOnlyList<DirectorFollowerCollectionListDto> Lists { get; set; } = [];
    public IReadOnlyList<DirectorFollowerCollectionPaymentDto> Payments { get; set; } = [];
}

public sealed class DirectorFollowerCardDto
{
    public int FollowerId { get; set; }
    public string FollowerName { get; set; } = "";
    public double? PhantomRating { get; set; }
    public string? PhantomRatingLabel { get; set; }
    public double? PhantomRatingWeekAgo { get; set; }
    public string RatingTrend { get; set; } = DirectorFollowerOpsTrendRules.Flat;
    public int RatedCustomerCount { get; set; }
}

public sealed class DirectorFollowerListCardDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";
    public double? PhantomRating { get; set; }
    public string? PhantomRatingLabel { get; set; }
    public double? PhantomRatingWeekAgo { get; set; }
    public string RatingTrend { get; set; } = DirectorFollowerOpsTrendRules.Flat;
    public int RatedCustomerCount { get; set; }
}

public sealed class DirectorFollowerRemainingCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? Phone { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public double AmountRemaining { get; set; }
    public string? RatingLabel { get; set; }
}

public sealed class DirectorFollowerRatingBucketDto
{
    public string LevelKey { get; set; } = "";
    public string Label { get; set; } = "";
    public int CustomerCount { get; set; }
    public int? CustomerCountYesterday { get; set; }
    public string CountTrend { get; set; } = DirectorFollowerOpsTrendRules.Flat;
}

public sealed class DirectorFollowerWorkBundleDto
{
    public IReadOnlyList<DirectorFollowerListCardDto> ListCards { get; set; } = [];
    public IReadOnlyList<DirectorFollowerRatingBucketDto> RatingBuckets { get; set; } = [];
    public int RatedCustomerCount { get; set; }
    public int RemainingLe100Count { get; set; }
    public int RemainingLt200Count { get; set; }
    public int? RemainingLe100CountYesterday { get; set; }
    public int? RemainingLt200CountYesterday { get; set; }
    public string RemainingLe100Trend { get; set; } = DirectorFollowerOpsTrendRules.Flat;
    public string RemainingLt200Trend { get; set; } = DirectorFollowerOpsTrendRules.Flat;
}

public sealed class DirectorFollowerSalesWorkspaceDto
{
    public IReadOnlyList<DirectorFollowerSaleAuthRowDto> SalesAuth { get; set; } = [];
    public DirectorFollowerWorkBundleDto Bundle { get; set; } = new();
}

public interface IDirectorFollowerOpsService
{
    Task<IReadOnlyList<DirectorFollowerOpsSummaryDto>> GetSummariesAsync(CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerCardDto>> GetFollowerCardsAsync(CancellationToken ct);
    Task<DirectorFollowerWorkBundleDto> GetWorkBundleAsync(int followerId, CancellationToken ct);
    Task<DirectorFollowerSalesWorkspaceDto> GetSalesWorkspaceAsync(int followerId, CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerRemainingCustomerDto>> GetRemainingCustomersAsync(
        int followerId, string bucket, CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerSaleAuthRowDto>> GetSalesAuthAsync(int followerId, CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerDecliningRowDto>> GetDecliningAsync(int followerId, CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerNoteRowDto>> GetNotesAsync(int followerId, CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerFieldFollowupRowDto>> GetFieldFollowupsAsync(int followerId, CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerLegalFollowupRowDto>> GetLegalFollowupsAsync(int followerId, CancellationToken ct);
    Task<IReadOnlyList<DirectorFollowerDelegateNoteRowDto>> GetDelegateNotesAsync(int followerId, CancellationToken ct);
    Task<DirectorFollowerCollectionBundleDto> GetCollectionAsync(int followerId, CancellationToken ct);
    Task<bool> SetCollectionEnabledAsync(int listId, bool enabled, int? userId, CancellationToken ct);
}

/// <summary>
/// Read/ops for director «عمل المتابعين»: sale auth, declining, notes, field follow-ups, delegate notes, collection.
/// No UI date windows — live operational queues.
/// </summary>
public sealed class DirectorFollowerOpsService : IDirectorFollowerOpsService
{
    public const string CardUiDeployMarker = "FollowerOpsTrendsCacheV1";

    /// <summary>Match follower app remainder bands (IQD): [0, 100000) and [100000, 200000).</summary>
    public const double RemainingBand100k = 100_000;
    public const double RemainingBand200k = 200_000;

    private readonly SalesDevelopmentGuard _guard;
    private readonly IFollowerUserListsRepository _followerLists;
    private readonly IFollowerDirectoryService _followers;
    private readonly IDelegatesRepository _delegates;
    private readonly IRatingDataSource _rating;
    private readonly ILegalFollowupService _legalFollowups;

    public DirectorFollowerOpsService(
        SalesDevelopmentGuard guard,
        IFollowerUserListsRepository followerLists,
        IFollowerDirectoryService followers,
        IDelegatesRepository delegates,
        IRatingDataSource rating,
        ILegalFollowupService legalFollowups)
    {
        _guard = guard;
        _followerLists = followerLists;
        _followers = followers;
        _delegates = delegates;
        _rating = rating;
        _legalFollowups = legalFollowups;
    }

    public async Task<IReadOnlyList<DirectorFollowerOpsSummaryDto>> GetSummariesAsync(CancellationToken ct)
    {
        var followers = (await _followers.ListActiveFollowersAsync(ct)).ToList();
        if (followers.Count == 0)
        {
            return [];
        }

        await EnsureSaleAuthTableAsync(ct);
        await EnsureFieldFollowupsTableAsync(ct);
        await EnsureDelegateNotesTableAsync(ct);
        await _delegates.EnsureFollowerCollectionColumnAsync(ct);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync(ct);

        await EnsureRatingSnapshotsAsync(ct);

        var result = new List<DirectorFollowerOpsSummaryDto>();
        foreach (var f in followers)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var listIds = (await _followerLists.GetAssignedListIdsAsync(f.FollowerId, ct)).ToList();
                // Distinct customers — same sale authenticated on multiple lists counts once.
                var authCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(DISTINCT CustomerId)
FROM dbo.FollowerSaleAuthentications
WHERE FollowerUserId = @Fid;",
                    new { Fid = f.FollowerId }, cancellationToken: ct));

                var pending = 0;
                if (listIds.Count > 0)
                {
                    pending = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.Customers c
WHERE c.DelegateID IN @ListIds
  AND ISNULL(c.CustomerState, 1) = 1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.FollowerSaleAuthentications a
      WHERE a.CustomerId = c.CustomerID
  )
  AND EXISTS (
      SELECT 1 FROM dbo.SalesDrafts sd
      WHERE sd.CustomerId = c.CustomerID
        AND sd.UserType = N'موظف مبيعات'
        AND sd.Status = N'Completed'
        AND ISNULL(sd.PostingStatus, N'') = N'Posted'
  );",
                        new { ListIds = listIds }, cancellationToken: ct));
                }

                var (declining, noted) = await CountDecliningFromSnapshotsAsync(connection, listIds, ct);
                var notesCount = await CountNotesAsync(connection, f.FollowerId, listIds, ct);
                var (ffPending, ffDone) = await CountFieldFollowupsAsync(connection, f.FollowerId, ct);
                var delegateNotes = await CountDelegateNotesAsync(connection, f.FollowerId, listIds, ct);
                var (payCount, paySum) = await SumFollowerPaymentsAsync(connection, listIds, ct);
                var enabledLists = 0;
                if (listIds.Count > 0)
                {
                    enabledLists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.Delegates
WHERE DelegateID IN @ListIds AND ISNULL(FollowerCollectionEnabled, 0) = 1;",
                        new { ListIds = listIds }, cancellationToken: ct));
                }

                result.Add(new DirectorFollowerOpsSummaryDto
                {
                    FollowerId = f.FollowerId,
                    FollowerName = string.IsNullOrWhiteSpace(f.FollowerName) ? $"متابع {f.FollowerId}" : f.FollowerName,
                    SalesAuthCount = authCount,
                    PendingSalesCount = pending,
                    DecliningCount = declining,
                    DecliningNotedCount = noted,
                    NotesCount = notesCount,
                    FieldFollowupPendingCount = ffPending,
                    FieldFollowupDoneCount = ffDone,
                    DelegateNotesCount = delegateNotes,
                    CollectionPaymentCount = payCount,
                    CollectionPaymentSum = paySum,
                    CollectionEnabledLists = enabledLists,
                    IsWorking = authCount > 0 || noted > 0 || payCount > 0 || ffPending > 0 || ffDone > 0 || notesCount > 0 || delegateNotes > 0
                });
            }
            catch
            {
                result.Add(new DirectorFollowerOpsSummaryDto
                {
                    FollowerId = f.FollowerId,
                    FollowerName = string.IsNullOrWhiteSpace(f.FollowerName) ? $"متابع {f.FollowerId}" : f.FollowerName
                });
            }
        }

        return result.OrderBy(x => x.FollowerName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<DirectorFollowerCardDto>> GetFollowerCardsAsync(CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(CardUiDeployMarker);
        await _followerLists.EnsureSchemaAsync(ct);
        var followers = (await _followers.ListActiveFollowersAsync(ct)).ToList();
        if (followers.Count == 0)
        {
            return [];
        }

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var weekAgo = DirectorFollowerOpsTrendRules.WeekAgoDate(asOf);
        var followerIds = followers.Select(f => f.FollowerId).ToList();
        // One assignment query + one facts query for the whole governorate.
        var listMap = await LoadFollowerListMapAsync(followerIds, ct);
        var allListIds = listMap.Values.SelectMany(x => x).Distinct().ToList();
        var factsByListToday = new Dictionary<int, List<CustomerRatingFacts>>();
        var factsByListWeekAgo = new Dictionary<int, List<CustomerRatingFacts>>();
        if (allListIds.Count > 0)
        {
            var openBalance = await LoadOpenBalanceRatingFactsAsync(allListIds, ct);
            foreach (var g in FilterActiveRatingFacts(openBalance, asOf).GroupBy(f => f.DelegateId ?? 0))
            {
                factsByListToday[g.Key] = g.ToList();
            }

            foreach (var g in FilterActiveRatingFacts(openBalance, weekAgo).GroupBy(f => f.DelegateId ?? 0))
            {
                factsByListWeekAgo[g.Key] = g.ToList();
            }
        }

        var result = new List<DirectorFollowerCardDto>(followers.Count);
        foreach (var f in followers)
        {
            var dto = new DirectorFollowerCardDto
            {
                FollowerId = f.FollowerId,
                FollowerName = string.IsNullOrWhiteSpace(f.FollowerName) ? $"متابع {f.FollowerId}" : f.FollowerName
            };
            if (listMap.TryGetValue(f.FollowerId, out var listIds) && listIds.Count > 0)
            {
                var listScores = new List<double>(listIds.Count);
                var weekScores = new List<double>(listIds.Count);
                var ratedCount = 0;
                foreach (var listId in listIds)
                {
                    factsByListToday.TryGetValue(listId, out var active);
                    active ??= [];
                    var phantom = EmployeeSalesRating.Snapshot(active, asOf, usePhantomSaleDate: true);
                    listScores.Add(phantom.AverageScore);
                    ratedCount += phantom.CustomerCount;

                    factsByListWeekAgo.TryGetValue(listId, out var weekActive);
                    weekActive ??= [];
                    var weekPhantom = EmployeeSalesRating.Snapshot(weekActive, weekAgo, usePhantomSaleDate: true);
                    weekScores.Add(weekPhantom.AverageScore);
                }

                var avg = listScores.Average();
                var weekAvg = weekScores.Average();
                dto.PhantomRating = Math.Round(avg, 2, MidpointRounding.AwayFromZero);
                dto.PhantomRatingLabel = ListRatingAggregator.ClassifyAverage(avg).Label;
                dto.PhantomRatingWeekAgo = Math.Round(weekAvg, 2, MidpointRounding.AwayFromZero);
                dto.RatingTrend = DirectorFollowerOpsTrendRules.ComputeScoreTrend(avg, weekAvg);
                dto.RatedCustomerCount = ratedCount;
            }

            result.Add(dto);
        }

        return result.OrderBy(x => x.FollowerName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<DirectorFollowerWorkBundleDto> GetWorkBundleAsync(int followerId, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return new DirectorFollowerWorkBundleDto();
        }

        var listIds = (await _followerLists.GetAssignedListIdsAsync(followerId, ct)).ToList();
        if (listIds.Count == 0)
        {
            return new DirectorFollowerWorkBundleDto();
        }

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var weekAgo = DirectorFollowerOpsTrendRules.WeekAgoDate(asOf);
        var yesterday = DirectorFollowerOpsTrendRules.YesterdayDate(asOf);
        var listNames = await LoadListNamesAsync(listIds, ct);
        // Single facts pull for all lists, then split by as-of cohort in memory.
        var openBalance = await LoadOpenBalanceRatingFactsAsync(listIds, ct);
        var allActive = FilterActiveRatingFacts(openBalance, asOf);
        var weekActive = FilterActiveRatingFacts(openBalance, weekAgo);
        var yesterdayActive = FilterActiveRatingFacts(openBalance, yesterday);
        var byList = allActive.GroupBy(f => f.DelegateId ?? 0)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CustomerRatingFacts>)g.ToList());
        var byListWeek = weekActive.GroupBy(f => f.DelegateId ?? 0)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CustomerRatingFacts>)g.ToList());
        var listCards = new List<DirectorFollowerListCardDto>(listIds.Count);
        foreach (var listId in listIds)
        {
            byList.TryGetValue(listId, out var active);
            active ??= Array.Empty<CustomerRatingFacts>();
            var phantom = EmployeeSalesRating.Snapshot(active, asOf, usePhantomSaleDate: true);
            byListWeek.TryGetValue(listId, out var wActive);
            wActive ??= Array.Empty<CustomerRatingFacts>();
            var weekPhantom = EmployeeSalesRating.Snapshot(wActive, weekAgo, usePhantomSaleDate: true);
            listNames.TryGetValue(listId, out var name);
            listCards.Add(new DirectorFollowerListCardDto
            {
                ListId = listId,
                ListName = string.IsNullOrWhiteSpace(name) ? $"قائمة {listId}" : name!,
                PhantomRating = phantom.AverageScore,
                PhantomRatingLabel = phantom.RatingLabel,
                PhantomRatingWeekAgo = weekPhantom.AverageScore,
                RatingTrend = DirectorFollowerOpsTrendRules.ComputeScoreTrend(
                    phantom.AverageScore, weekPhantom.AverageScore),
                RatedCustomerCount = phantom.CustomerCount
            });
        }

        var ratedResults = CustomerRatingCalculator.EvaluateMany(allActive, asOf, usePhantomSaleDate: true);
        var agg = ListRatingAggregator.Aggregate(ratedResults);
        var yRated = CustomerRatingCalculator.EvaluateMany(yesterdayActive, yesterday, usePhantomSaleDate: true);
        var yAgg = ListRatingAggregator.Aggregate(yRated);
        var ratingBuckets = new List<DirectorFollowerRatingBucketDto>
        {
            Bucket("excellent", CustomerRatingLabels.Excellent, agg.ExcellentCount, yAgg.ExcellentCount),
            Bucket("good", CustomerRatingLabels.Good, agg.GoodCount, yAgg.GoodCount),
            Bucket("weak", CustomerRatingLabels.Weak, agg.WeakCount, yAgg.WeakCount),
            Bucket("lagging", CustomerRatingLabels.Rejected, agg.RejectedCount, yAgg.RejectedCount),
            Bucket("bad", CustomerRatingLabels.Bad, agg.BadCount, yAgg.BadCount),
        };

        // Same bands as follower list_detail: باقي أقل من 100,000 / باقي من 100,000 إلى أقل من 200,000
        var le100 = CountRemainingBand(allActive, 0.0000001, RemainingBand100k);
        var lt200 = CountRemainingBand(allActive, RemainingBand100k, RemainingBand200k);
        var le100Y = CountRemainingBand(yesterdayActive, 0.0000001, RemainingBand100k);
        var lt200Y = CountRemainingBand(yesterdayActive, RemainingBand100k, RemainingBand200k);

        return new DirectorFollowerWorkBundleDto
        {
            ListCards = listCards.OrderBy(x => x.ListName, StringComparer.CurrentCultureIgnoreCase).ToList(),
            RatingBuckets = ratingBuckets,
            RatedCustomerCount = agg.CustomerCount,
            RemainingLe100Count = le100,
            RemainingLt200Count = lt200,
            RemainingLe100CountYesterday = le100Y,
            RemainingLt200CountYesterday = lt200Y,
            RemainingLe100Trend = DirectorFollowerOpsTrendRules.ComputeCountTrend(le100, le100Y),
            RemainingLt200Trend = DirectorFollowerOpsTrendRules.ComputeCountTrend(lt200, lt200Y)
        };

        static DirectorFollowerRatingBucketDto Bucket(string key, string label, int today, int yesterday) =>
            new()
            {
                LevelKey = key,
                Label = label,
                CustomerCount = today,
                CustomerCountYesterday = yesterday,
                CountTrend = DirectorFollowerOpsTrendRules.ComputeCountTrend(today, yesterday)
            };
    }

    public async Task<DirectorFollowerSalesWorkspaceDto> GetSalesWorkspaceAsync(int followerId, CancellationToken ct)
    {
        var authTask = GetSalesAuthAsync(followerId, ct);
        var bundleTask = GetWorkBundleAsync(followerId, ct);
        await Task.WhenAll(authTask, bundleTask);
        return new DirectorFollowerSalesWorkspaceDto
        {
            SalesAuth = await authTask,
            Bundle = await bundleTask
        };
    }

    public async Task<IReadOnlyList<DirectorFollowerRemainingCustomerDto>> GetRemainingCustomersAsync(
        int followerId, string bucket, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return [];
        }

        var listIds = (await _followerLists.GetAssignedListIdsAsync(followerId, ct)).ToList();
        if (listIds.Count == 0)
        {
            return [];
        }

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var active = await LoadFollowerAppRatingFactsAsync(listIds, asOf, ct);

        var key = (bucket ?? "").Trim().ToLowerInvariant();
        if (key is "le100" or "100" or "100k")
        {
            active = active
                .Where(f => f.AmountRemaining > 0 && f.AmountRemaining < RemainingBand100k)
                .ToList();
        }
        else if (key is "lt200" or "200" or "200k")
        {
            active = active
                .Where(f => f.AmountRemaining >= RemainingBand100k && f.AmountRemaining < RemainingBand200k)
                .ToList();
        }

        var ratedPairs = active
            .Select(f => (Fact: f, Rated: CustomerRatingCalculator.Evaluate(f, asOf, usePhantomSaleDate: true)))
            .ToList();

        ratedPairs = key switch
        {
            "excellent" or "ممتاز" => ratedPairs.Where(x => x.Rated.Level == CustomerRatingLevel.Excellent).ToList(),
            "good" or "جيد" => ratedPairs.Where(x => x.Rated.Level == CustomerRatingLevel.Good).ToList(),
            "weak" or "ضعيف" => ratedPairs.Where(x => x.Rated.Level == CustomerRatingLevel.Weak).ToList(),
            "lagging" or "rejected" or "متلكئ" => ratedPairs.Where(x => x.Rated.Level == CustomerRatingLevel.Rejected).ToList(),
            "bad" or "سيء" => ratedPairs.Where(x => x.Rated.Level == CustomerRatingLevel.Bad).ToList(),
            "rated" or "ratings" or "all" or "le100" or "100" or "lt200" or "200" or "" => ratedPairs,
            _ => ratedPairs
        };

        var names = await LoadNamesAsync(ratedPairs.Select(r => r.Fact.CustomerId).ToList(), ct);
        var listNames = await LoadListNamesAsync(listIds, ct);
        return ratedPairs.Select(x =>
        {
            names.TryGetValue(x.Fact.CustomerId, out var info);
            listNames.TryGetValue(x.Fact.DelegateId ?? 0, out var listName);
            return new DirectorFollowerRemainingCustomerDto
            {
                CustomerId = x.Fact.CustomerId,
                CustomerName = string.IsNullOrWhiteSpace(info.Name) ? $"زبون {x.Fact.CustomerId}" : info.Name!,
                Phone = info.Phone,
                ListId = x.Fact.DelegateId ?? 0,
                ListName = listName,
                AmountRemaining = x.Fact.AmountRemaining,
                RatingLabel = CustomerRatingLabels.FromLevel(x.Rated.Level)
            };
        }).OrderBy(x => x.CustomerName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<DirectorFollowerSaleAuthRowDto>> GetSalesAuthAsync(
        int followerId, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return [];
        }

        await EnsureSaleAuthTableAsync(ct);
        var listIds = (await _followerLists.GetAssignedListIdsAsync(followerId, ct)).ToList();
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await EnsureSalesRequestShopRatingColumnsAsync(connection, ct);

        // Authenticated rows for lists assigned to this follower (any authenticator on the list).
        List<DirectorFollowerSaleAuthRowDto> done = [];
        if (listIds.Count > 0)
        {
            // One row per sale/customer: if the same sale was authenticated under multiple
            // lists assigned to this follower, keep the latest auth and show current list name.
            done = (await connection.QueryAsync<DirectorFollowerSaleAuthRowDto>(new CommandDefinition(@"
;WITH Ranked AS (
SELECT
    a.CustomerId,
    ISNULL(c.CustomerName, N'') AS CustomerName,
    c.PhoneNumber AS Phone,
    COALESCE(NULLIF(c.DelegateID, 0), a.ListId) AS ListId,
    COALESCE(dCurr.DelegateName, dAuth.DelegateName) AS ListName,
    a.SaleDraftId,
    a.CustomerSaleId,
    CAST(1 AS bit) AS Authenticated,
    a.AuthenticatedAtUtc,
    a.Rating AS AuthRating,
    a.Note AS AuthNote,
    CAST(COALESCE(dfc.Rating, sr.ShopRating) AS int) AS ShopRating,
    COALESCE(dfc.Note, sr.ShopRatingNote) AS ShopRatingNote,
    CAST(NULLIF(a.ShopStockEstimatedValue, 0) AS float) AS ShopStockEstimatedValue,
    CAST(NULLIF(a.EstimatedDailyRevenue, 0) AS float) AS EstimatedDailyRevenue,
    CAST(COALESCE(NULLIF(dfc.ShopStockEstimatedValue, 0), NULLIF(sr.ShopStockEstimatedValue, 0)) AS float) AS DelegateShopStockEstimatedValue,
    CAST(COALESCE(NULLIF(dfc.EstimatedDailyRevenue, 0), NULLIF(sr.EstimatedDailyRevenue, 0)) AS float) AS DelegateEstimatedDailyRevenue,
    CAST(NULLIF(emp.ShopStockEstimatedValue, 0) AS float) AS EmployeeShopStockEstimatedValue,
    CAST(NULLIF(emp.EstimatedDailyRevenue, 0) AS float) AS EmployeeEstimatedDailyRevenue,
    CAST(COALESCE(sd.FinalSalePrice, vc.AmountTotalSales, 0) AS float) AS SalePrice,
    COALESCE(sd.PostedAtUtc, sd.CompletedAt, cs.DateCreate) AS SaleDate,
    CAST(COALESCE(sd.DailyInstallment, vc.AmountDaySales, 0) AS float) AS DailyInstallment,
    COALESCE(sd.UserName, u.UserName) AS SellerName,
    CAST(ISNULL(vc.AmountTotalSales, 0) - ISNULL(vc.AmountRemaining, 0) AS float) AS PaidAmount,
    CAST(ISNULL(vc.AmountRemaining, 0) AS float) AS RemainingAmount,
    ROW_NUMBER() OVER (
        PARTITION BY
            CASE
                WHEN a.SaleDraftId IS NOT NULL THEN CONCAT(N'SD', a.SaleDraftId)
                WHEN a.CustomerSaleId IS NOT NULL THEN CONCAT(N'CS', a.CustomerSaleId)
                ELSE CONCAT(N'C', a.CustomerId)
            END
        ORDER BY a.AuthenticatedAtUtc DESC, a.Id DESC
    ) AS rn
FROM dbo.FollowerSaleAuthentications a
LEFT JOIN dbo.Customers c ON c.CustomerID = a.CustomerId
LEFT JOIN dbo.Delegates dAuth ON dAuth.DelegateID = a.ListId
LEFT JOIN dbo.Delegates dCurr ON dCurr.DelegateID = c.DelegateID
LEFT JOIN dbo.SalesDrafts sd ON sd.SaleId = a.SaleDraftId
LEFT JOIN dbo.CustomersSales cs ON cs.CustomerSaleID = a.CustomerSaleId
LEFT JOIN dbo.Users u ON u.UserID = cs.UserID
OUTER APPLY (
    SELECT TOP 1 v.AmountTotalSales, v.AmountDaySales, v.AmountRemaining
    FROM dbo.View_Customers v WHERE v.CustomerID = a.CustomerId
) vc
OUTER APPLY (
    SELECT TOP 1 e.Rating, e.Note, e.ShopStockEstimatedValue, e.EstimatedDailyRevenue
    FROM dbo.DelegateFirstCollectionEvaluations e
    WHERE e.CustomerId = a.CustomerId
    ORDER BY e.CreatedAtUtc DESC
) dfc
OUTER APPLY (
    SELECT TOP 1 r.ShopRating, r.ShopRatingNote, r.ShopStockEstimatedValue, r.EstimatedDailyRevenue
    FROM dbo.SalesRequests r
    WHERE r.ExistingCustomerId = a.CustomerId AND r.ShopRating IS NOT NULL
    ORDER BY r.CreatedAtUtc DESC
) sr
OUTER APPLY (
    SELECT TOP 1 p.ShopStockEstimatedValue, p.EstimatedDailyRevenue
    FROM dbo.SalesShopProfiles p
    WHERE (a.SaleDraftId IS NOT NULL AND p.SaleId = a.SaleDraftId)
       OR (a.SaleDraftId IS NULL AND p.CustomerId = a.CustomerId)
    ORDER BY CASE WHEN a.SaleDraftId IS NOT NULL AND p.SaleId = a.SaleDraftId THEN 0 ELSE 1 END,
             p.CreatedAtUtc DESC
) emp
WHERE a.ListId IN @ListIds
)
SELECT
    CustomerId, CustomerName, Phone, ListId, ListName, SaleDraftId, CustomerSaleId,
    Authenticated, AuthenticatedAtUtc, AuthRating, AuthNote, ShopRating, ShopRatingNote,
    ShopStockEstimatedValue, EstimatedDailyRevenue, DelegateShopStockEstimatedValue,
    DelegateEstimatedDailyRevenue, EmployeeShopStockEstimatedValue, EmployeeEstimatedDailyRevenue,
    SalePrice, SaleDate, DailyInstallment, SellerName, PaidAmount, RemainingAmount
FROM Ranked
WHERE rn = 1
ORDER BY AuthenticatedAtUtc DESC;",
                new { ListIds = listIds }, cancellationToken: ct))).ToList();
        }

        // Pending = sales-employee posted drafts on follower lists not yet authenticated.
        List<DirectorFollowerSaleAuthRowDto> pending = [];
        if (listIds.Count > 0)
        {
            pending = (await connection.QueryAsync<DirectorFollowerSaleAuthRowDto>(new CommandDefinition(@"
SELECT TOP 300
    c.CustomerID AS CustomerId,
    ISNULL(c.CustomerName, N'') AS CustomerName,
    c.PhoneNumber AS Phone,
    c.DelegateID AS ListId,
    d.DelegateName AS ListName,
    sd.SaleId AS SaleDraftId,
    CAST(NULL AS int) AS CustomerSaleId,
    CAST(0 AS bit) AS Authenticated,
    CAST(NULL AS datetime2) AS AuthenticatedAtUtc,
    CAST(NULL AS int) AS AuthRating,
    CAST(NULL AS nvarchar(1)) AS AuthNote,
    CAST(COALESCE(dfc.Rating, sr.ShopRating) AS int) AS ShopRating,
    COALESCE(dfc.Note, sr.ShopRatingNote) AS ShopRatingNote,
    CAST(NULL AS float) AS ShopStockEstimatedValue,
    CAST(NULL AS float) AS EstimatedDailyRevenue,
    CAST(COALESCE(NULLIF(dfc.ShopStockEstimatedValue, 0), NULLIF(sr.ShopStockEstimatedValue, 0)) AS float) AS DelegateShopStockEstimatedValue,
    CAST(COALESCE(NULLIF(dfc.EstimatedDailyRevenue, 0), NULLIF(sr.EstimatedDailyRevenue, 0)) AS float) AS DelegateEstimatedDailyRevenue,
    CAST(NULLIF(emp.ShopStockEstimatedValue, 0) AS float) AS EmployeeShopStockEstimatedValue,
    CAST(NULLIF(emp.EstimatedDailyRevenue, 0) AS float) AS EmployeeEstimatedDailyRevenue,
    CAST(COALESCE(sd.FinalSalePrice, vc.AmountTotalSales, 0) AS float) AS SalePrice,
    COALESCE(sd.PostedAtUtc, sd.CompletedAt) AS SaleDate,
    CAST(COALESCE(sd.DailyInstallment, vc.AmountDaySales, 0) AS float) AS DailyInstallment,
    sd.UserName AS SellerName,
    CAST(ISNULL(vc.AmountTotalSales, 0) - ISNULL(vc.AmountRemaining, 0) AS float) AS PaidAmount,
    CAST(ISNULL(vc.AmountRemaining, 0) AS float) AS RemainingAmount
FROM dbo.Customers c
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
OUTER APPLY (
    SELECT TOP 1 s.*
    FROM dbo.SalesDrafts s
    WHERE s.CustomerId = c.CustomerID
      AND s.UserType = N'موظف مبيعات'
      AND s.Status = N'Completed'
      AND ISNULL(s.PostingStatus, N'') = N'Posted'
    ORDER BY s.PostedAtUtc DESC, s.SaleId DESC
) sd
OUTER APPLY (
    SELECT TOP 1 v.AmountTotalSales, v.AmountDaySales, v.AmountRemaining
    FROM dbo.View_Customers v WHERE v.CustomerID = c.CustomerID
) vc
OUTER APPLY (
    SELECT TOP 1 e.Rating, e.Note, e.ShopStockEstimatedValue, e.EstimatedDailyRevenue
    FROM dbo.DelegateFirstCollectionEvaluations e
    WHERE e.CustomerId = c.CustomerID
    ORDER BY e.CreatedAtUtc DESC
) dfc
OUTER APPLY (
    SELECT TOP 1 r.ShopRating, r.ShopRatingNote, r.ShopStockEstimatedValue, r.EstimatedDailyRevenue
    FROM dbo.SalesRequests r
    WHERE r.ExistingCustomerId = c.CustomerID AND r.ShopRating IS NOT NULL
    ORDER BY r.CreatedAtUtc DESC
) sr
OUTER APPLY (
    SELECT TOP 1 p.ShopStockEstimatedValue, p.EstimatedDailyRevenue
    FROM dbo.SalesShopProfiles p
    WHERE p.SaleId = sd.SaleId
) emp
WHERE c.DelegateID IN @ListIds
  AND ISNULL(c.CustomerState, 1) = 1
  AND NOT EXISTS (
      SELECT 1 FROM dbo.FollowerSaleAuthentications a
      WHERE a.CustomerId = c.CustomerID
         OR (sd.SaleId IS NOT NULL AND a.SaleDraftId = sd.SaleId)
  )
  AND sd.SaleId IS NOT NULL
ORDER BY COALESCE(sd.PostedAtUtc, sd.CompletedAt) DESC;",
                new { ListIds = listIds }, cancellationToken: ct))).ToList();
        }

        var merged = DirectorFollowerSaleAuthDedupeRules.Deduplicate(done.Concat(pending)).ToList();
        await FillCurrentRatingsAsync(merged, ct);
        return merged;
    }

    public async Task<IReadOnlyList<DirectorFollowerDecliningRowDto>> GetDecliningAsync(
        int followerId, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return [];
        }

        var listIds = (await _followerLists.GetAssignedListIdsAsync(followerId, ct)).ToList();
        if (listIds.Count == 0)
        {
            return [];
        }

        // Read-only: same snapshots the follower app writes. Skip heavy refresh (was causing 504).
        await EnsureRatingSnapshotsAsync(ct);

        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var raw = (await connection.QueryAsync<DeclineSnapRow>(new CommandDefinition(@"
DECLARE @Today DATE = @AsOf;
DECLARE @From DATE = DATEADD(DAY, -@Lookback, @Today);

;WITH Latest AS (
    SELECT
        s.CustomerId, s.ListId, s.CustomerName, s.PhoneNumber,
        s.RatingLabel AS ToLabel, s.Score AS ToScore, s.CaptureDate AS CurrentCaptureDate,
        ROW_NUMBER() OVER (PARTITION BY s.CustomerId ORDER BY s.CaptureDate DESC, s.CapturedAtUtc DESC) AS rn
    FROM dbo.FollowerCustomerRatingSnapshots s
    WHERE s.ListId IN @ListIds
),
CurrentRows AS (SELECT * FROM Latest WHERE rn = 1),
PriorRows AS (
    SELECT s.CustomerId, s.RatingLabel AS FromLabel, s.Score AS FromScore, s.CaptureDate AS PriorCaptureDate,
        ROW_NUMBER() OVER (PARTITION BY s.CustomerId ORDER BY s.CaptureDate DESC, s.CapturedAtUtc DESC) AS rn
    FROM dbo.FollowerCustomerRatingSnapshots s
    INNER JOIN CurrentRows c ON c.CustomerId = s.CustomerId
    WHERE s.CaptureDate < c.CurrentCaptureDate AND s.CaptureDate >= @From
)
SELECT
    c.CustomerId,
    COALESCE(NULLIF(LTRIM(RTRIM(c.CustomerName)), N''), cust.CustomerName, N'') AS CustomerName,
    COALESCE(NULLIF(LTRIM(RTRIM(c.PhoneNumber)), N''), cust.PhoneNumber) AS Phone,
    c.ListId,
    d.DelegateName AS ListName,
    p.FromLabel, c.ToLabel, p.FromScore, c.ToScore,
    DATEDIFF(DAY, p.PriorCaptureDate, c.CurrentCaptureDate) AS DaysBetween
FROM CurrentRows c
INNER JOIN PriorRows p ON p.CustomerId = c.CustomerId AND p.rn = 1
LEFT JOIN dbo.Customers cust ON cust.CustomerID = c.CustomerId
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.ListId
WHERE DATEDIFF(DAY, p.PriorCaptureDate, c.CurrentCaptureDate) BETWEEN 0 AND @Lookback;",
            new
            {
                ListIds = listIds,
                AsOf = asOf,
                Lookback = DirectorFollowerOpsDeclineRules.LookbackDays
            },
            cancellationToken: ct,
            commandTimeout: 90))).ToList();

        var filtered = raw
            .Where(r => DirectorFollowerOpsDeclineRules.IsDeclining(
                r.FromLabel, r.FromScore, r.ToLabel, r.ToScore, r.DaysBetween))
            .ToList();

        var noteMap = await LoadLatestNotesAsync(filtered.Select(x => x.CustomerId).ToList(), ct);
        var authSet = await LoadAuthCustomerIdsAsync(followerId, filtered.Select(x => x.CustomerId).ToList(), ct);

        return filtered.Select(r =>
        {
            noteMap.TryGetValue(r.CustomerId, out var noteAt);
            return new DirectorFollowerDecliningRowDto
            {
                CustomerId = r.CustomerId,
                CustomerName = r.CustomerName ?? "",
                Phone = r.Phone,
                ListId = r.ListId,
                ListName = r.ListName,
                FromLabel = r.FromLabel ?? "",
                ToLabel = r.ToLabel ?? "",
                RatingLabel = r.ToLabel,
                FromScore = r.FromScore,
                ToScore = r.ToScore,
                DaysBetween = r.DaysBetween,
                HasNote = noteAt.HasValue,
                LastNoteAtUtc = noteAt,
                HasSaleAuth = authSet.Contains(r.CustomerId)
            };
        }).OrderBy(x => x.CustomerName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<IReadOnlyList<DirectorFollowerNoteRowDto>> GetNotesAsync(
        int followerId, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return [];
        }

        var listIds = (await _followerLists.GetAssignedListIdsAsync(followerId, ct)).ToList();
        if (listIds.Count == 0)
        {
            return [];
        }

        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var has = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (has != 1)
        {
            return [];
        }

        var rows = (await connection.QueryAsync<DirectorFollowerNoteRowDto>(new CommandDefinition(@"
SELECT TOP 400
    n.CustomerID AS CustomerId,
    ISNULL(c.CustomerName, N'') AS CustomerName,
    c.PhoneNumber AS Phone,
    ISNULL(c.DelegateID, 0) AS ListId,
    d.DelegateName AS ListName,
    ISNULL(n.NoteText, N'') AS NoteText,
    n.CreatedByName AS AuthorName,
    n.CreatedAtUtc,
    N'customer' AS Source
FROM dbo.CustomerNotes n
INNER JOIN dbo.Customers c ON c.CustomerID = n.CustomerID
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE c.DelegateID IN @ListIds
  AND (n.UserID = @Fid OR ISNULL(n.CreatedByName, N'') <> N'')
ORDER BY n.CreatedAtUtc DESC;",
            new { ListIds = listIds, Fid = followerId }, cancellationToken: ct))).ToList();

        // Prefer notes authored by this follower when UserID matches; still show list-scoped notes.
        return rows;
    }

    public async Task<IReadOnlyList<DirectorFollowerFieldFollowupRowDto>> GetFieldFollowupsAsync(
        int followerId, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return [];
        }

        await EnsureFieldFollowupsTableAsync(ct);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var rows = (await connection.QueryAsync<DirectorFollowerFieldFollowupRowDto>(new CommandDefinition(@"
SELECT
    Id,
    CustomerId,
    ISNULL(CustomerName, N'') AS CustomerName,
    PhoneNumber AS Phone,
    ListId,
    ListName,
    Status,
    CompletionNote,
    CreatedAtUtc,
    CompletedAtUtc,
    CAST(CASE WHEN Status = N'Completed' THEN 1 ELSE 0 END AS bit) AS IsDone
FROM dbo.FollowerFieldFollowups
WHERE FollowerUserId = @Fid
ORDER BY
    CASE WHEN Status = N'Pending' THEN 0 ELSE 1 END,
    COALESCE(CompletedAtUtc, CreatedAtUtc) DESC;",
            new { Fid = followerId }, cancellationToken: ct))).ToList();
        return rows;
    }

    public async Task<IReadOnlyList<DirectorFollowerLegalFollowupRowDto>> GetLegalFollowupsAsync(
        int followerId, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return [];
        }

        var cases = await _legalFollowups.ListForFollowerAsync(followerId, status: null, ct);
        return cases
            .Where(c => !LegalFollowupStatuses.IsCancelled(c.Status))
            .OrderBy(c => string.Equals(c.Status, LegalFollowupStatuses.Completed, StringComparison.Ordinal) ? 1 : 0)
            .ThenByDescending(c => c.CompletedAtUtc ?? c.CreatedAtUtc)
            .Select(c => new DirectorFollowerLegalFollowupRowDto
            {
                Id = c.Id,
                CustomerId = c.CustomerId,
                SalesRequestId = c.SalesRequestId,
                CustomerName = c.CustomerName ?? "",
                Phone = c.CustomerPhone,
                Address = c.CustomerAddress,
                Status = c.Status ?? "",
                StatusLabel = c.StatusLabel,
                Note = c.Note,
                CompletionNote = c.CompletionNote,
                CreatedAtUtc = c.CreatedAtUtc,
                CompletedAtUtc = c.CompletedAtUtc,
                IsDone = string.Equals(c.Status, LegalFollowupStatuses.Completed, StringComparison.Ordinal)
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DirectorFollowerDelegateNoteRowDto>> GetDelegateNotesAsync(
        int followerId, CancellationToken ct)
    {
        if (followerId <= 0)
        {
            return [];
        }

        await EnsureDelegateNotesTableAsync(ct);
        var listIds = (await _followerLists.GetAssignedListIdsAsync(followerId, ct)).ToList();
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var rows = (await connection.QueryAsync<DirectorFollowerDelegateNoteRowDto>(new CommandDefinition(@"
SELECT
    n.Id,
    n.DelegateId,
    n.ListId,
    d.DelegateName AS ListName,
    ISNULL(n.NoteText, N'') AS NoteText,
    n.CreatedByName AS AuthorName,
    n.CreatedAtUtc
FROM dbo.FollowerDelegateNotes n
LEFT JOIN dbo.Delegates d ON d.DelegateID = ISNULL(n.ListId, n.DelegateId)
WHERE n.CreatedByUserId = @Fid
   OR (n.ListId IS NOT NULL AND n.ListId IN @ListIds)
   OR (n.DelegateId IN @ListIds)
ORDER BY n.CreatedAtUtc DESC;",
            new { Fid = followerId, ListIds = listIds.Count == 0 ? new[] { -1 } : listIds.ToArray() },
            cancellationToken: ct))).ToList();
        return rows;
    }

    public async Task<DirectorFollowerCollectionBundleDto> GetCollectionAsync(
        int followerId, CancellationToken ct)
    {
        await _delegates.EnsureFollowerCollectionColumnAsync(ct);
        var listIds = (await _followerLists.GetAssignedListIdsAsync(followerId, ct)).ToList();
        if (listIds.Count == 0)
        {
            return new DirectorFollowerCollectionBundleDto();
        }

        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);

        var lists = (await connection.QueryAsync<DirectorFollowerCollectionListDto>(new CommandDefinition(@"
SELECT
    d.DelegateID AS ListId,
    d.DelegateName AS ListName,
    CAST(ISNULL(d.FollowerCollectionEnabled, 0) AS bit) AS FollowerCollectionEnabled,
    d.FollowerCollectionAssignedAtUtc AS AssignedAtUtc
FROM dbo.Delegates d
WHERE d.DelegateID IN @ListIds
ORDER BY d.DelegateName;",
            new { ListIds = listIds }, cancellationToken: ct))).ToList();

        var payments = new List<DirectorFollowerCollectionPaymentDto>();
        var hasReq = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.CustomersPaymentsRequest', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (hasReq == 1)
        {
            payments = (await connection.QueryAsync<DirectorFollowerCollectionPaymentDto>(new CommandDefinition(@"
SELECT TOP 400
    r.CustomerID AS CustomerId,
    ISNULL(c.CustomerName, N'') AS CustomerName,
    ISNULL(c.DelegateID, 0) AS ListId,
    d.DelegateName AS ListName,
    CAST(ISNULL(r.Amount, 0) * 1448 AS float) AS Amount,
    r.PaymentDate AS PaymentDate,
    N'follower' AS Source
FROM dbo.CustomersPaymentsRequest r
LEFT JOIN dbo.Customers c ON c.CustomerID = r.CustomerID
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
WHERE c.DelegateID IN @ListIds
ORDER BY r.PaymentDate DESC;",
                new { ListIds = listIds }, cancellationToken: ct))).ToList();
        }

        var iraqToday = DateTime.UtcNow.AddHours(3).Date;
        var yesterday = iraqToday.AddDays(-1);
        var yFrom = DateTime.SpecifyKind(yesterday.AddHours(-3), DateTimeKind.Utc);
        var yTo = DateTime.SpecifyKind(iraqToday.AddHours(-3), DateTimeKind.Utc);

        var delegateByList = (await connection.QueryAsync<(int ListId, double Amt)>(new CommandDefinition(@"
-- FollowerOpsDelegateYesterdayTotal
SELECT c.DelegateID AS ListId,
       CAST(ISNULL(SUM(ISNULL(p.AmountDenar, 0)), 0) AS float) AS Amt
FROM dbo.View_CustomersPaymentsDelegate p
INNER JOIN dbo.Customers c ON c.CustomerID = p.CustomerID
WHERE c.DelegateID IN @ListIds
  AND p.PaymentDate >= @From AND p.PaymentDate < @To
GROUP BY c.DelegateID;",
            new { ListIds = listIds, From = yFrom, To = yTo }, cancellationToken: ct)))
            .ToDictionary(x => x.ListId, x => x.Amt);

        foreach (var list in lists)
        {
            list.LastDelegatePaymentAt = yesterday;
            list.LastDelegatePaymentAmount = delegateByList.TryGetValue(list.ListId, out var dAmt) ? dAmt : 0;

            if (hasReq != 1 || list.AssignedAtUtc is null)
            {
                continue;
            }

            var enableDay = list.AssignedAtUtc.Value.AddHours(3).Date;
            var eFrom = DateTime.SpecifyKind(enableDay.AddHours(-3), DateTimeKind.Utc);
            var eTo = DateTime.SpecifyKind(enableDay.AddDays(1).AddHours(-3), DateTimeKind.Utc);
            var fAmt = await connection.ExecuteScalarAsync<double?>(new CommandDefinition(@"
-- FollowerOpsEnableDayTotal
SELECT CAST(ISNULL(SUM(ISNULL(r.Amount, 0) * 1448), 0) AS float)
FROM dbo.CustomersPaymentsRequest r
INNER JOIN dbo.Customers c ON c.CustomerID = r.CustomerID
WHERE c.DelegateID = @ListId
  AND r.PaymentDate >= @From AND r.PaymentDate < @To;",
                new { ListId = list.ListId, From = eFrom, To = eTo }, cancellationToken: ct));
            list.LastFollowerPaymentAt = enableDay;
            list.LastFollowerPaymentAmount = fAmt ?? 0;
        }

        return new DirectorFollowerCollectionBundleDto { Lists = lists, Payments = payments };
    }

    public Task<bool> SetCollectionEnabledAsync(int listId, bool enabled, int? userId, CancellationToken ct) =>
        _delegates.SetFollowerCollectionEnabledAsync(listId, enabled, userId, ct);

    private async Task FillCurrentRatingsAsync(
        List<DirectorFollowerSaleAuthRowDto> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var listIds = rows.Select(r => r.ListId).Where(id => id > 0).Distinct().ToList();
        if (listIds.Count == 0)
        {
            return;
        }

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        var facts = await _rating.GetFactsByListIdsAsync(listIds, ct);
        var byId = facts.ToDictionary(f => f.CustomerId, f => f);
        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.CustomerId, out var fact))
            {
                continue;
            }

            var rated = CustomerRatingCalculator.Evaluate(fact, asOf);
            row.CurrentRatingLabel = rated.RatingLabel;
        }
    }

    private async Task<(int Declining, int Noted)> CountDecliningFromSnapshotsAsync(
        SqlConnection connection, IReadOnlyList<int> listIds, CancellationToken ct)
    {
        if (listIds.Count == 0)
        {
            return (0, 0);
        }

        try
        {
            var has = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerCustomerRatingSnapshots', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (has != 1)
            {
                return (0, 0);
            }

            var asOf = DateTime.UtcNow.AddHours(3).Date;
            var raw = (await connection.QueryAsync<DeclineSnapRow>(new CommandDefinition(@"
-- FollowerOpsSummaryNoRefresh
DECLARE @Today DATE = @AsOf;
DECLARE @From DATE = DATEADD(DAY, -@Lookback, @Today);
;WITH Latest AS (
    SELECT s.CustomerId, s.RatingLabel AS ToLabel, s.Score AS ToScore, s.CaptureDate AS CurrentCaptureDate,
        ROW_NUMBER() OVER (PARTITION BY s.CustomerId ORDER BY s.CaptureDate DESC, s.CapturedAtUtc DESC) AS rn
    FROM dbo.FollowerCustomerRatingSnapshots s WHERE s.ListId IN @ListIds
),
CurrentRows AS (SELECT * FROM Latest WHERE rn = 1),
PriorRows AS (
    SELECT s.CustomerId, s.RatingLabel AS FromLabel, s.Score AS FromScore, s.CaptureDate AS PriorCaptureDate,
        ROW_NUMBER() OVER (PARTITION BY s.CustomerId ORDER BY s.CaptureDate DESC, s.CapturedAtUtc DESC) AS rn
    FROM dbo.FollowerCustomerRatingSnapshots s
    INNER JOIN CurrentRows c ON c.CustomerId = s.CustomerId
    WHERE s.CaptureDate < c.CurrentCaptureDate AND s.CaptureDate >= @From
)
SELECT c.CustomerId, p.FromLabel, c.ToLabel, p.FromScore, c.ToScore,
    DATEDIFF(DAY, p.PriorCaptureDate, c.CurrentCaptureDate) AS DaysBetween
FROM CurrentRows c
INNER JOIN PriorRows p ON p.CustomerId = c.CustomerId AND p.rn = 1
WHERE DATEDIFF(DAY, p.PriorCaptureDate, c.CurrentCaptureDate) BETWEEN 0 AND @Lookback;",
                new
                {
                    ListIds = listIds.ToArray(),
                    AsOf = asOf,
                    Lookback = DirectorFollowerOpsDeclineRules.LookbackDays
                },
                cancellationToken: ct,
                commandTimeout: 20))).ToList();

            var decliningIds = raw
                .Where(r => DirectorFollowerOpsDeclineRules.IsDeclining(
                    r.FromLabel, r.FromScore, r.ToLabel, r.ToScore, r.DaysBetween))
                .Select(r => r.CustomerId)
                .Distinct()
                .ToList();
            if (decliningIds.Count == 0)
            {
                return (0, 0);
            }

            var noteMap = await LoadLatestNotesAsync(decliningIds, ct);
            return (decliningIds.Count, decliningIds.Count(id => noteMap.ContainsKey(id) && noteMap[id].HasValue));
        }
        catch
        {
            return (0, 0);
        }
    }

    private static async Task<int> CountNotesAsync(
        SqlConnection connection, int followerId, IReadOnlyList<int> listIds, CancellationToken ct)
    {
        if (listIds.Count == 0)
        {
            return 0;
        }

        var has = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (has != 1)
        {
            return 0;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.CustomerNotes n
INNER JOIN dbo.Customers c ON c.CustomerID = n.CustomerID
WHERE c.DelegateID IN @ListIds;",
            new { ListIds = listIds.ToArray() }, cancellationToken: ct));
    }

    private static async Task<(int Pending, int Done)> CountFieldFollowupsAsync(
        SqlConnection connection, int followerId, CancellationToken ct)
    {
        var has = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerFieldFollowups', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (has != 1)
        {
            return (0, 0);
        }

        var row = await connection.QueryFirstOrDefaultAsync<(int Pending, int Done)>(new CommandDefinition(@"
SELECT
    SUM(CASE WHEN Status = N'Pending' THEN 1 ELSE 0 END) AS Pending,
    SUM(CASE WHEN Status = N'Completed' THEN 1 ELSE 0 END) AS Done
FROM dbo.FollowerFieldFollowups WHERE FollowerUserId = @Fid;",
            new { Fid = followerId }, cancellationToken: ct));
        return (row.Pending, row.Done);
    }

    private static async Task<int> CountDelegateNotesAsync(
        SqlConnection connection, int followerId, IReadOnlyList<int> listIds, CancellationToken ct)
    {
        var has = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerDelegateNotes', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (has != 1)
        {
            return 0;
        }

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.FollowerDelegateNotes
WHERE CreatedByUserId = @Fid
   OR (ListId IS NOT NULL AND ListId IN @ListIds)
   OR (DelegateId IN @ListIds);",
            new { Fid = followerId, ListIds = listIds.Count == 0 ? new[] { -1 } : listIds.ToArray() },
            cancellationToken: ct));
    }

    private static async Task<(int Count, double Sum)> SumFollowerPaymentsAsync(
        SqlConnection connection, IReadOnlyList<int> listIds, CancellationToken ct)
    {
        if (listIds.Count == 0)
        {
            return (0, 0);
        }

        var hasReq = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.CustomersPaymentsRequest', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (hasReq != 1)
        {
            return (0, 0);
        }

        var row = await connection.QueryFirstOrDefaultAsync<(int Cnt, double Sum)>(new CommandDefinition(@"
SELECT COUNT(1) AS Cnt, CAST(ISNULL(SUM(ISNULL(r.Amount, 0) * 1448), 0) AS float) AS Sum
FROM dbo.CustomersPaymentsRequest r
INNER JOIN dbo.Customers c ON c.CustomerID = r.CustomerID
WHERE c.DelegateID IN @ListIds;",
            new { ListIds = listIds.ToArray() }, cancellationToken: ct));
        return (row.Cnt, row.Sum);
    }

    /// <summary>
    /// Lockstep with follower app <c>FollowerDeclineService.RefreshForFollowerAsync</c>:
    /// active open-balance cohort + phantom sale-date + short band labels.
    /// </summary>
    private async Task RefreshRatingSnapshotsAsync(
        int followerId, IReadOnlyList<int> listIds, CancellationToken ct)
    {
        if (listIds.Count == 0)
        {
            return;
        }

        var asOf = DateTime.UtcNow.AddHours(3).Date;
        // Same cohort as follower declining / list cards (not OpenBalance-all).
        var facts = await LoadFollowerAppRatingFactsAsync(listIds, asOf, ct);
        var names = await LoadNamesAsync(facts.Select(f => f.CustomerId).ToList(), ct);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);

        // Drop today's rows for these lists first so a prior bad refresh (non-phantom)
        // cannot leave false "1-day" declines that the follower app does not show.
        await connection.ExecuteAsync(new CommandDefinition(@"
DELETE FROM dbo.FollowerCustomerRatingSnapshots
WHERE CaptureDate = @CaptureDate AND ListId IN @ListIds;",
            new { CaptureDate = asOf, ListIds = listIds.ToArray() },
            cancellationToken: ct));

        foreach (var fact in facts)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var rated = CustomerRatingCalculator.Evaluate(fact, asOf, usePhantomSaleDate: true);
                names.TryGetValue(fact.CustomerId, out var info);
                await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.FollowerCustomerRatingSnapshots
    (CustomerId, ListId, CustomerName, PhoneNumber, RatingLabel, Score, CapturedAtUtc, CaptureDate)
VALUES
    (@CustomerId, @ListId, @CustomerName, @Phone, @Label, @Score, SYSUTCDATETIME(), @CaptureDate);",
                    new
                    {
                        CustomerId = fact.CustomerId,
                        ListId = fact.DelegateId ?? 0,
                        CustomerName = info.Name,
                        Phone = info.Phone,
                        Label = CustomerRatingLabels.FromLevel(rated.Level),
                        Score = rated.Score,
                        CaptureDate = asOf
                    },
                    cancellationToken: ct));
            }
            catch
            {
                // skip one customer
            }
        }
    }

    private static async Task EnsureSalesRequestShopRatingColumnsAsync(
        SqlConnection connection, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.SalesRequests', N'ShopRating') IS NULL
        ALTER TABLE dbo.SalesRequests ADD ShopRating INT NULL;
    IF COL_LENGTH(N'dbo.SalesRequests', N'ShopRatingNote') IS NULL
        ALTER TABLE dbo.SalesRequests ADD ShopRatingNote NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'dbo.SalesRequests', N'ShopStockEstimatedValue') IS NULL
        ALTER TABLE dbo.SalesRequests ADD ShopStockEstimatedValue DECIMAL(18,0) NULL;
    IF COL_LENGTH(N'dbo.SalesRequests', N'EstimatedDailyRevenue') IS NULL
        ALTER TABLE dbo.SalesRequests ADD EstimatedDailyRevenue DECIMAL(18,0) NULL;
END
IF OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DelegateFirstCollectionEvaluations (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DelegateFirstCollectionEvaluations PRIMARY KEY,
        CustomerId INT NOT NULL,
        ListId INT NOT NULL CONSTRAINT DF_DFCE_ListId_Ops DEFAULT (0),
        DelegateId INT NOT NULL CONSTRAINT DF_DFCE_DelegateId_Ops DEFAULT (0),
        DelegateName NVARCHAR(200) NULL,
        Rating INT NOT NULL CONSTRAINT DF_DFCE_Rating_Ops DEFAULT (1),
        Note NVARCHAR(1000) NULL,
        ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_DFCE_Stock_Ops DEFAULT (0),
        EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_DFCE_Daily_Ops DEFAULT (0),
        SaleAmount DECIMAL(18,0) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DFCE_At_Ops DEFAULT (SYSUTCDATETIME())
    );
END
ELSE
BEGIN
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'ShopStockEstimatedValue') IS NULL
        ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_DFCE_Stock_Ops2 DEFAULT (0);
    IF COL_LENGTH(N'dbo.DelegateFirstCollectionEvaluations', N'EstimatedDailyRevenue') IS NULL
        ALTER TABLE dbo.DelegateFirstCollectionEvaluations ADD EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_DFCE_Daily_Ops2 DEFAULT (0);
END
IF OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesShopProfiles (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesShopProfiles_Ops PRIMARY KEY,
        SaleId INT NOT NULL,
        CustomerId INT NULL,
        ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_SSP_Stock_Ops DEFAULT (0),
        EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_SSP_Daily_Ops DEFAULT (0),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SSP_At_Ops DEFAULT (SYSUTCDATETIME())
    );
END", cancellationToken: ct));
    }

    private async Task EnsureSaleAuthTableAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerSaleAuthentications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerSaleAuthentications (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerSaleAuthentications PRIMARY KEY,
        CustomerId INT NOT NULL,
        ListId INT NOT NULL,
        CustomerSaleId INT NULL,
        SaleDraftId INT NULL,
        FollowerUserId INT NOT NULL,
        FollowerName NVARCHAR(200) NULL,
        Rating INT NOT NULL,
        Note NVARCHAR(MAX) NULL,
        ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FSA_Stock DEFAULT (0),
        EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FSA_Daily DEFAULT (0),
        AuthenticatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FSA_At DEFAULT (SYSUTCDATETIME())
    );
END
IF COL_LENGTH(N'dbo.FollowerSaleAuthentications', N'ShopStockEstimatedValue') IS NULL
    ALTER TABLE dbo.FollowerSaleAuthentications ADD ShopStockEstimatedValue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FSA_Stock2 DEFAULT (0);
IF COL_LENGTH(N'dbo.FollowerSaleAuthentications', N'EstimatedDailyRevenue') IS NULL
    ALTER TABLE dbo.FollowerSaleAuthentications ADD EstimatedDailyRevenue DECIMAL(18,0) NOT NULL CONSTRAINT DF_FSA_Daily2 DEFAULT (0);
IF COL_LENGTH(N'dbo.FollowerSaleAuthentications', N'Note') IS NULL
    ALTER TABLE dbo.FollowerSaleAuthentications ADD Note NVARCHAR(MAX) NULL;",
            cancellationToken: ct));
    }

    private async Task EnsureFieldFollowupsTableAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerFieldFollowups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerFieldFollowups (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerFieldFollowups PRIMARY KEY,
        FollowerUserId INT NOT NULL,
        ListId INT NOT NULL,
        ListName NVARCHAR(200) NULL,
        CustomerId INT NOT NULL,
        CustomerName NVARCHAR(200) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_FollowerFieldFollowups_Status DEFAULT (N'Pending'),
        CompletionNote NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerFieldFollowups_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CompletedAtUtc DATETIME2 NULL
    );
END", cancellationToken: ct));
    }

    private async Task EnsureDelegateNotesTableAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerDelegateNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerDelegateNotes (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerDelegateNotes PRIMARY KEY,
        DelegateId INT NOT NULL,
        ListId INT NULL,
        NoteText NVARCHAR(2000) NOT NULL,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NULL,
        CreatedByRole NVARCHAR(50) NOT NULL CONSTRAINT DF_FollowerDelegateNotes_Role DEFAULT (N'Follower'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerDelegateNotes_At DEFAULT (SYSUTCDATETIME())
    );
END", cancellationToken: ct));
    }

    private async Task EnsureRatingSnapshotsAsync(CancellationToken ct)
    {
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerCustomerRatingSnapshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerCustomerRatingSnapshots (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerCustomerRatingSnapshots PRIMARY KEY,
        CustomerId INT NOT NULL,
        ListId INT NOT NULL,
        CustomerName NVARCHAR(200) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        RatingLabel NVARCHAR(32) NOT NULL,
        Score INT NOT NULL,
        CapturedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FCRS_Captured DEFAULT (SYSUTCDATETIME()),
        CaptureDate DATE NOT NULL
    );
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UQ_FollowerCustomerRatingSnapshots_Cust_Day'
      AND object_id = OBJECT_ID(N'dbo.FollowerCustomerRatingSnapshots')
)
    CREATE UNIQUE INDEX UQ_FollowerCustomerRatingSnapshots_Cust_Day
        ON dbo.FollowerCustomerRatingSnapshots (CustomerId, CaptureDate);",
            cancellationToken: ct));
    }

    /// <summary>
    /// Same cohort as follower app ListCards: open remaining + payment in last 30 days,
    /// phantom sale-date math, equation factor forced to 1 (follower calculator has no equation).
    /// </summary>
    private async Task<List<CustomerRatingFacts>> LoadFollowerAppRatingFactsAsync(
        IReadOnlyList<int> listIds, DateTime asOf, CancellationToken ct)
    {
        var raw = await LoadOpenBalanceRatingFactsAsync(listIds, ct);
        return FilterActiveRatingFacts(raw, asOf);
    }

    private async Task<List<CustomerRatingFacts>> LoadOpenBalanceRatingFactsAsync(
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

    private static List<CustomerRatingFacts> FilterActiveRatingFacts(
        IEnumerable<CustomerRatingFacts> facts, DateTime asOf) =>
        facts.Where(f => ActiveCustomerRules.IsActive(f.LastPaymentDate, asOf)).ToList();

    private static int CountRemainingBand(
        IEnumerable<CustomerRatingFacts> facts, double minInclusive, double maxExclusive) =>
        facts.Count(f => f.AmountRemaining >= minInclusive && f.AmountRemaining < maxExclusive);

    private async Task<Dictionary<int, List<int>>> LoadFollowerListMapAsync(
        IReadOnlyList<int> followerIds, CancellationToken ct)
    {
        var map = followerIds.Distinct().ToDictionary(id => id, _ => new List<int>());
        if (map.Count == 0)
        {
            return map;
        }

        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var rows = await connection.QueryAsync<(int UserId, int ListId)>(new CommandDefinition(@"
SELECT UserId, ListId
FROM dbo.FollowerUserLists
WHERE UserId IN @Ids;",
            new { Ids = map.Keys.ToArray() }, cancellationToken: ct));
        foreach (var r in rows)
        {
            if (map.TryGetValue(r.UserId, out var list))
            {
                list.Add(r.ListId);
            }
        }

        return map;
    }

    private async Task<Dictionary<int, string?>> LoadListNamesAsync(
        IReadOnlyList<int> listIds, CancellationToken ct)
    {
        var map = new Dictionary<int, string?>();
        if (listIds.Count == 0)
        {
            return map;
        }

        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var rows = await connection.QueryAsync<(int Id, string? Name)>(new CommandDefinition(@"
SELECT DelegateID AS Id, DelegateName AS Name
FROM dbo.Delegates WHERE DelegateID IN @Ids;",
            new { Ids = listIds.Distinct().ToArray() }, cancellationToken: ct));
        foreach (var r in rows)
        {
            map[r.Id] = r.Name;
        }

        return map;
    }

    private async Task<Dictionary<int, (string? Name, string? Phone)>> LoadNamesAsync(
        IReadOnlyList<int> ids, CancellationToken ct)
    {
        var map = new Dictionary<int, (string? Name, string? Phone)>();
        if (ids.Count == 0)
        {
            return map;
        }

        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var rows = await connection.QueryAsync<(int Id, string? Name, string? Phone)>(new CommandDefinition(@"
SELECT CustomerID AS Id, CustomerName AS Name, PhoneNumber AS Phone
FROM dbo.Customers WHERE CustomerID IN @Ids;",
            new { Ids = ids.Distinct().ToArray() }, cancellationToken: ct));
        foreach (var r in rows)
        {
            map[r.Id] = (r.Name, r.Phone);
        }

        return map;
    }

    private async Task<Dictionary<int, DateTime?>> LoadLatestNotesAsync(
        IReadOnlyList<int> customerIds, CancellationToken ct)
    {
        var map = new Dictionary<int, DateTime?>();
        if (customerIds.Count == 0)
        {
            return map;
        }

        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var has = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (has != 1)
        {
            return map;
        }

        var rows = await connection.QueryAsync<(int CustomerId, DateTime? At)>(new CommandDefinition(@"
SELECT CustomerID AS CustomerId, MAX(CreatedAtUtc) AS At
FROM dbo.CustomerNotes WHERE CustomerID IN @Ids GROUP BY CustomerID;",
            new { Ids = customerIds.Distinct().ToArray() }, cancellationToken: ct));
        foreach (var r in rows)
        {
            map[r.CustomerId] = r.At;
        }

        return map;
    }

    private async Task<HashSet<int>> LoadAuthCustomerIdsAsync(
        int followerId, IReadOnlyList<int> customerIds, CancellationToken ct)
    {
        var set = new HashSet<int>();
        if (customerIds.Count == 0)
        {
            return set;
        }

        await EnsureSaleAuthTableAsync(ct);
        var cs = RequireCs();
        await using var connection = new SqlConnection(cs);
        var rows = await connection.QueryAsync<int>(new CommandDefinition(@"
SELECT DISTINCT CustomerId FROM dbo.FollowerSaleAuthentications
WHERE FollowerUserId = @Fid AND CustomerId IN @Ids;",
            new { Fid = followerId, Ids = customerIds.Distinct().ToArray() }, cancellationToken: ct));
        foreach (var id in rows)
        {
            set.Add(id);
        }

        return set;
    }

    private string RequireCs()
    {
        var cs = _guard.GetSalesConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("لا يوجد اتصال بقاعدة بيانات الفرع.");
        }

        return cs;
    }

    private sealed class DeclineSnapRow
    {
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public int ListId { get; set; }
        public string? ListName { get; set; }
        public string? FromLabel { get; set; }
        public string? ToLabel { get; set; }
        public int FromScore { get; set; }
        public int ToScore { get; set; }
        public int DaysBetween { get; set; }
    }
}
