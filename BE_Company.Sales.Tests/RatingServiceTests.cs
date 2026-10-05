using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class RatingServiceTests
{
    private sealed class FakeSource : IRatingDataSource
    {
        public List<CustomerRatingFacts> Facts { get; } = new();

        public Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByCustomerIdsAsync(
            IReadOnlyList<int> customerIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerRatingFacts>>(
                Facts.Where(f => customerIds.Contains(f.CustomerId)).ToList());

        public Task<CustomerRatingFacts?> GetFactByCustomerIdAsync(int customerId, CancellationToken ct = default) =>
            Task.FromResult(Facts.FirstOrDefault(f => f.CustomerId == customerId));

        public Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByListIdsAsync(
            IReadOnlyList<int> listIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerRatingFacts>>(
                Facts.Where(f => f.DelegateId is int d && listIds.Contains(d)).ToList());
    }

    private static readonly DateTime AsOf = new(2026, 3, 1);

    [Fact]
    public async Task Customer_Summary_Batch_Returns_Labels()
    {
        var source = new FakeSource();
        source.Facts.Add(new CustomerRatingFacts(1, 9, true, false, AsOf.AddDays(-10), AsOf, 1000, 1000, 0));
        source.Facts.Add(new CustomerRatingFacts(2, 9, false, false, AsOf.AddDays(-99), AsOf, 1_000_000, 1_000_000, 0));
        var svc = new RatingService(source, new FixedClock(AsOf));

        var rows = await svc.GetCustomerSummariesAsync([1, 2]);
        Assert.Equal(2, rows.Count);
        // IsLegal=true still rates by settle days (10 days → ممتاز), not قانونية.
        Assert.Equal("ممتاز", rows.First(r => r.CustomerId == 1).Rating);
        Assert.Equal(10, rows.First(r => r.CustomerId == 1).Score);
        Assert.Equal("ممتاز", rows.First(r => r.CustomerId == 2).Rating);
        Assert.Equal(10, rows.First(r => r.CustomerId == 2).Score);
    }

    [Fact]
    public async Task List_Summary_Includes_Historical_And_Recent()
    {
        // List/follower cohort uses open-balance only (AmountRemaining > 0).
        var source = new FakeSource();
        var cutoff = ListRatingAggregator.RecentCutoffDate;
        // High payment rate → ممتاز; remaining still open.
        source.Facts.Add(OpenExcellent(1, 5, cutoff));
        source.Facts.Add(OpenExcellent(2, 5, cutoff.AddDays(-30)));
        var svc = new RatingService(source, new FixedClock(AsOf));

        var rows = await svc.GetListSummariesAsync([5]);
        var row = Assert.Single(rows);
        Assert.Equal(2, row.OverallCustomerCount);
        Assert.Equal(1, row.RecentCustomerCount);
        Assert.Equal("ممتاز", row.OverallRating);
        Assert.Equal("ممتاز", row.RecentRating);
    }

    [Fact]
    public async Task Missing_Customer_Detail_Returns_Null()
    {
        var svc = new RatingService(new FakeSource(), new FixedClock(AsOf));
        Assert.Null(await svc.GetCustomerDetailAsync(99));
    }

    private static CustomerRatingFacts OpenExcellent(int id, int listId, DateTime saleDate)
    {
        // Score as-of AsOf: set receipts so rate ≥ ExcellentRate for the real day span.
        const double sales = 1_000_000;
        var days = CustomerRatingCalculator.DiffDaysInclusive(saleDate, AsOf);
        var receipts = CustomerRatingCalculator.ExcellentRate * sales * days / 1000.0;
        return new CustomerRatingFacts(
            id,
            listId,
            false,
            false,
            saleDate,
            AsOf.AddDays(-1),
            sales,
            receipts,
            Math.Max(1, sales - receipts));
    }

    private sealed class FixedClock : IIraqClock
    {
        private readonly DateTime _utc;
        public FixedClock(DateTime iraqLocal) =>
            _utc = IraqTimeService.ToUtcFromIraq(iraqLocal);
        public DateTime UtcNow => _utc;
    }
}
