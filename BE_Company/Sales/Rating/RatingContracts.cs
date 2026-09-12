namespace BE_Company.Sales.Rating;

public interface IRatingDataSource
{
    Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByCustomerIdsAsync(
        IReadOnlyList<int> customerIds, CancellationToken ct = default);

    Task<CustomerRatingFacts?> GetFactByCustomerIdAsync(int customerId, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByListIdsAsync(
        IReadOnlyList<int> listIds, CancellationToken ct = default);
}

public interface IRatingService
{
    Task<IReadOnlyList<CustomerRatingSummaryDto>> GetCustomerSummariesAsync(
        IReadOnlyList<int> customerIds, CancellationToken ct = default);

    Task<CustomerRatingDetailDto?> GetCustomerDetailAsync(int customerId, CancellationToken ct = default);

    Task<IReadOnlyList<ListRatingSummaryDto>> GetListSummariesAsync(
        IReadOnlyList<int> listIds, CancellationToken ct = default);

    Task<ListRatingDetailDto?> GetListDetailAsync(int listId, CancellationToken ct = default);
}

public sealed class CustomerRatingSummaryDto
{
    public int CustomerId { get; init; }
    public string Rating { get; init; } = CustomerRatingLabels.Weak;
    public int Score { get; init; }
    public string? ReasonShort { get; init; }
}

public sealed class CustomerRatingDetailDto
{
    public int CustomerId { get; init; }
    public string Rating { get; init; } = CustomerRatingLabels.Weak;
    public int Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IsLegal { get; init; }
    public bool IsSettled { get; init; }
    public int? DaysToSettle { get; init; }
    public int? DaysSinceSale { get; init; }
    public double? PaymentRate { get; init; }
}

public sealed class ListRatingSummaryDto
{
    public int ListId { get; init; }
    public string OverallRating { get; init; } = CustomerRatingLabels.Weak;
    public double OverallAverageScore { get; init; }
    public int OverallCustomerCount { get; init; }
    public string RecentRating { get; init; } = CustomerRatingLabels.Weak;
    public double RecentAverageScore { get; init; }
    public int RecentCustomerCount { get; init; }
    public string RiskIndicator { get; init; } = nameof(ListRiskIndicator.None);
    public string RecentRiskIndicator { get; init; } = nameof(ListRiskIndicator.None);
}

public sealed class ListRatingBucketDto
{
    public int CustomerCount { get; init; }
    public int ExcellentCount { get; init; }
    public int GoodCount { get; init; }
    public int WeakCount { get; init; }
    public int RejectedCount { get; init; }
    public int LegalCount { get; init; }
    public int TotalPoints { get; init; }
    public double AverageScore { get; init; }
    public double LegalPercentage { get; init; }
    public string RiskIndicator { get; init; } = nameof(ListRiskIndicator.None);
    public string FinalRating { get; init; } = CustomerRatingLabels.Weak;
}

public sealed class ListRatingDetailDto
{
    public int ListId { get; init; }
    public ListRatingBucketDto Historical { get; init; } = new();
    public ListRatingBucketDto Recent { get; init; } = new();
}
