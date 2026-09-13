using BE_Company.Sales;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// Proves evaluation rating load survives &gt;2100 matched customer IDs via batched facts queries.
/// </summary>
public class SalesRequestEvaluationSqlParameterBatchTests
{
    private static SalesIdentity Manager() => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = "najaf-demo",
        BranchName = "النجف",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

    private sealed class RecordingRatingSource : IRatingDataSource
    {
        public List<int> BatchSizes { get; } = [];
        public int MaxBatchSeen { get; private set; }
        public HashSet<int> LegalIds { get; } = [];

        public async Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByCustomerIdsAsync(
            IReadOnlyList<int> customerIds,
            CancellationToken ct = default)
        {
            // Simulate RatingDataSource batching contract used in production.
            var merged = new List<CustomerRatingFacts>();
            var seen = new HashSet<int>();
            foreach (var chunk in SqlInClauseBatch.Chunk(customerIds))
            {
                ct.ThrowIfCancellationRequested();
                if (chunk.Length >= SqlInClauseBatch.SqlServerMaxParameters)
                {
                    throw new InvalidOperationException("SQL parameter overflow simulated.");
                }

                BatchSizes.Add(chunk.Length);
                MaxBatchSeen = Math.Max(MaxBatchSeen, chunk.Length);
                await Task.Yield();
                foreach (var id in chunk)
                {
                    if (!seen.Add(id))
                    {
                        continue;
                    }

                    merged.Add(new CustomerRatingFacts(
                        id,
                        1,
                        IsLegal: LegalIds.Contains(id),
                        IsFakeSale: false,
                        DateSaleDevice: DateTime.UtcNow.Date.AddDays(-30),
                        LastPaymentDate: DateTime.UtcNow.Date,
                        AmountTotalSales: 1000,
                        ReceiptsTotal: 1000,
                        AmountRemaining: 0,
                        ReceiptCount: 1));
                }
            }

            return merged;
        }

        public Task<CustomerRatingFacts?> GetFactByCustomerIdAsync(int customerId, CancellationToken ct = default) =>
            Task.FromResult<CustomerRatingFacts?>(null);

        public Task<IReadOnlyList<CustomerRatingFacts>> GetFactsByListIdsAsync(
            IReadOnlyList<int> listIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CustomerRatingFacts>>([]);
    }

    private sealed class HugeCatalog : ISalesExcelCustomerSearchCatalog
    {
        public string CityValue => "najaf-demo";
        public string CityName => "النجف";
        public int WriteCount => 0;
        public List<SalesExcelSearchCustomerRow> Customers { get; } = [];

        public Task<IReadOnlyList<SalesExcelSearchCustomerRow>> LoadCustomersAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesExcelSearchCustomerRow>>(Customers);

        public Task<IReadOnlyList<SalesExcelSearchSaleRow>> LoadSalesAsync(
            IReadOnlyCollection<int> customerIds,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesExcelSearchSaleRow>>([]);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(2100)]
    [InlineData(2500)]
    [InlineData(5000)]
    public async Task Evaluate_With_Many_Phone_Matches_Does_Not_Overflow_Sql_Parameters(int matchCount)
    {
        var catalog = new HugeCatalog();
        var rating = new RecordingRatingSource();
        // Last id is Legal so overall must surface قانونية even if it sits in a later batch.
        var legalId = matchCount;
        rating.LegalIds.Add(legalId);

        for (var i = 1; i <= matchCount; i++)
        {
            catalog.Customers.Add(new SalesExcelSearchCustomerRow
            {
                CustomerId = i,
                FullName = $"زبون {i}",
                Phone = "07804924373"
            });
        }

        var svc = new SalesRequestEvaluationService(
            new FakeRequestRepository(),
            catalog,
            rating,
            new FakeClock { UtcNow = DateTime.UtcNow });

        var result = await svc.EvaluateBatchAsync(Manager(), new SalesRequestEvaluationBatchRequestDTO
        {
            Items =
            [
                new SalesRequestEvaluationPayloadDTO
                {
                    RequestId = 7,
                    SourceCityValue = "najaf-demo",
                    CustomerName = "اسم لا يطابق",
                    CustomerPhone = "07804924373"
                }
            ]
        }, default);

        Assert.Equal(matchCount, result.Items[0].Phone.ResultCount);
        Assert.Equal(CustomerRatingLabels.Legal, result.Items[0].OverallRatingLabel);
        Assert.Equal(-10, result.Items[0].OverallScore);
        Assert.True(rating.MaxBatchSeen <= SqlInClauseBatch.DefaultSafeSize);
        Assert.True(rating.MaxBatchSeen < SqlInClauseBatch.SqlServerMaxParameters);
        Assert.Equal(
            (matchCount + SqlInClauseBatch.DefaultSafeSize - 1) / SqlInClauseBatch.DefaultSafeSize,
            rating.BatchSizes.Count);
    }

    [Fact]
    public async Task Empty_Matched_Ids_Skip_Rating_Query()
    {
        var catalog = new HugeCatalog();
        var rating = new RecordingRatingSource();
        catalog.Customers.Add(new() { CustomerId = 1, FullName = "آخر", Phone = "07801112233" });
        var svc = new SalesRequestEvaluationService(
            new FakeRequestRepository(),
            catalog,
            rating,
            new FakeClock { UtcNow = DateTime.UtcNow });

        var result = await svc.EvaluateBatchAsync(Manager(), new SalesRequestEvaluationBatchRequestDTO
        {
            Items =
            [
                new SalesRequestEvaluationPayloadDTO
                {
                    RequestId = 1,
                    SourceCityValue = "najaf-demo",
                    CustomerName = "لا يوجد",
                    CustomerPhone = "07809999999"
                }
            ]
        }, default);

        Assert.Empty(rating.BatchSizes);
        Assert.False(result.Items[0].HasAnyMatch);
        Assert.Equal(0, result.Items[0].Phone.ResultCount);
    }
}
