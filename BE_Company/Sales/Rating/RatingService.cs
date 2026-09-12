using BE_Company.Sales.Services;

namespace BE_Company.Sales.Rating;

public sealed class RatingService : IRatingService
{
    private readonly IRatingDataSource _dataSource;
    private readonly IIraqClock _clock;

    public RatingService(IRatingDataSource dataSource, IIraqClock clock)
    {
        _dataSource = dataSource;
        _clock = clock;
    }

    private DateTime AsOfDate => IraqTimeService.IraqNow(_clock).Date;

    public async Task<IReadOnlyList<CustomerRatingSummaryDto>> GetCustomerSummariesAsync(
        IReadOnlyList<int> customerIds, CancellationToken ct = default)
    {
        var ids = customerIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return [];

        var facts = await _dataSource.GetFactsByCustomerIdsAsync(ids, ct);
        var asOf = AsOfDate;
        return facts.Select(f =>
        {
            var r = CustomerRatingCalculator.Evaluate(f, asOf);
            return new CustomerRatingSummaryDto
            {
                CustomerId = r.CustomerId,
                Rating = r.RatingLabel,
                Score = r.Score,
                ReasonShort = Truncate(r.Reason, 80)
            };
        }).ToList();
    }

    public async Task<CustomerRatingDetailDto?> GetCustomerDetailAsync(int customerId, CancellationToken ct = default)
    {
        if (customerId <= 0) return null;
        var fact = await _dataSource.GetFactByCustomerIdAsync(customerId, ct);
        if (fact is null) return null;
        var r = CustomerRatingCalculator.Evaluate(fact, AsOfDate);
        return new CustomerRatingDetailDto
        {
            CustomerId = r.CustomerId,
            Rating = r.RatingLabel,
            Score = r.Score,
            Reason = r.Reason,
            IsLegal = r.IsLegal,
            IsSettled = r.IsSettled,
            DaysToSettle = r.DaysToSettle,
            DaysSinceSale = r.DaysSinceSale,
            PaymentRate = r.PaymentRate
        };
    }

    public async Task<IReadOnlyList<ListRatingSummaryDto>> GetListSummariesAsync(
        IReadOnlyList<int> listIds, CancellationToken ct = default)
    {
        var ids = listIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0) return [];

        var facts = await _dataSource.GetFactsByListIdsAsync(ids, ct);
        var asOf = AsOfDate;
        var rated = facts.Select(f => CustomerRatingCalculator.Evaluate(f, asOf)).ToList();

        return ids.Select(listId =>
        {
            var forList = rated.Where(r => r.DelegateId == listId).ToList();
            var historical = ListRatingAggregator.Aggregate(forList);
            var recent = ListRatingAggregator.AggregateRecent(forList);
            return new ListRatingSummaryDto
            {
                ListId = listId,
                OverallRating = historical.FinalRatingLabel,
                OverallAverageScore = Round2(historical.AverageScore),
                OverallCustomerCount = historical.CustomerCount,
                RecentRating = recent.FinalRatingLabel,
                RecentAverageScore = Round2(recent.AverageScore),
                RecentCustomerCount = recent.CustomerCount,
                RiskIndicator = historical.RiskIndicator.ToString(),
                RecentRiskIndicator = recent.RiskIndicator.ToString()
            };
        }).ToList();
    }

    public async Task<ListRatingDetailDto?> GetListDetailAsync(int listId, CancellationToken ct = default)
    {
        if (listId <= 0) return null;
        var facts = await _dataSource.GetFactsByListIdsAsync([listId], ct);
        var asOf = AsOfDate;
        var rated = facts.Select(f => CustomerRatingCalculator.Evaluate(f, asOf)).ToList();
        var historical = ListRatingAggregator.Aggregate(rated);
        var recent = ListRatingAggregator.AggregateRecent(rated);
        return new ListRatingDetailDto
        {
            ListId = listId,
            Historical = ToBucket(historical),
            Recent = ToBucket(recent)
        };
    }

    private static ListRatingBucketDto ToBucket(ListRatingResult r) => new()
    {
        CustomerCount = r.CustomerCount,
        ExcellentCount = r.ExcellentCount,
        GoodCount = r.GoodCount,
        WeakCount = r.WeakCount,
        RejectedCount = r.RejectedCount,
        LegalCount = r.LegalCount,
        TotalPoints = r.TotalPoints,
        AverageScore = Round2(r.AverageScore),
        LegalPercentage = Round2(r.LegalPercentage),
        RiskIndicator = r.RiskIndicator.ToString(),
        FinalRating = r.FinalRatingLabel
    };

    private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
