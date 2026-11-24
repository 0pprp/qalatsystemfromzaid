using BE_DelegateWebApplication.Services.FollowerRating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public interface IFollowerBranchFactsCache
{
    /// <summary>
    /// Balance facts of every customer in the branch, keyed by list (Customers.DelegateID).
    /// Null when the View_Customers shape is not usable on this branch DB: callers then run
    /// their own per-list query.
    /// </summary>
    Task<ILookup<int, FollowerCustomerRatingFacts>?> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// dbo.View_Customers recomputes the balance of every customer in the branch each time it is
/// read, whatever the filter (about 4 s on the largest branch). The follower screens read it
/// once per list and per widget, so one follower opening a list ran a dozen full passes at once
/// and the requests timed out. This keeps one whole-branch pass for a short time and shares it
/// between all of those requests; concurrent callers wait for the same load.
/// </summary>
public sealed class FollowerBranchFactsCache : IFollowerBranchFactsCache
{
    /// <summary>How long one snapshot is reused. Balances shown can be this stale.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    /// <summary>After a failed load, wait this long before trying the view again.</summary>
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(2);

    private readonly string _cs;
    private readonly object _gate = new();
    private Task<ILookup<int, FollowerCustomerRatingFacts>?>? _load;
    private DateTime _loadStartedUtc;
    private DateTime _expiresUtc;

    public FollowerBranchFactsCache(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public Task<ILookup<int, FollowerCustomerRatingFacts>?> GetAsync(CancellationToken ct = default)
    {
        Task<ILookup<int, FollowerCustomerRatingFacts>?> load;
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            if (_load is null || (_load.IsCompleted && now >= _expiresUtc))
            {
                _loadStartedUtc = now;
                _load = LoadAndStampAsync();
            }

            load = _load;
        }

        // The load is shared, so one caller going away must not cancel it for the others.
        return load.WaitAsync(ct);
    }

    private async Task<ILookup<int, FollowerCustomerRatingFacts>?> LoadAndStampAsync()
    {
        ILookup<int, FollowerCustomerRatingFacts>? result = null;
        try
        {
            result = await LoadAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // View missing / different shape on this branch, or a timeout: callers fall back.
        }

        lock (_gate)
        {
            // Age counts from when the load started, so data is never older than Lifetime + load time.
            _expiresUtc = result is null
                ? DateTime.UtcNow + FailureBackoff
                : _loadStartedUtc + Lifetime;
        }

        return result;
    }

    private async Task<ILookup<int, FollowerCustomerRatingFacts>> LoadAsync()
    {
        await using var c = new SqlConnection(_cs);
        var rows = await c.QueryAsync<FactsRow>(new CommandDefinition(@"
SELECT
    C.CustomerID AS CustomerId,
    C.DelegateID AS DelegateId,
    ISNULL(VC.CustomerName, C.CustomerName) AS CustomerName,
    ISNULL(VC.PhoneNumber, C.PhoneNumber) AS PhoneNumber,
    CAST(ISNULL(C.IsLegal, 0) AS bit) AS IsLegal,
    VC.DateSaleDevice AS DateSaleDevice,
    VC.LastPaymentDate AS LastPaymentDate,
    CAST(ISNULL(VC.AmountTotalSales, 0) AS float) AS AmountTotalSales,
    CAST(ISNULL(VC.ReceiptsTotal, 0) AS float) AS ReceiptsTotal,
    CAST(ISNULL(VC.AmountRemaining, 0) AS float) AS AmountRemaining,
    CAST(ISNULL(VC.AmountDaySales, 0) AS float) AS AmountDaySales
FROM dbo.Customers C
LEFT JOIN dbo.View_Customers VC ON VC.CustomerID = C.CustomerID
WHERE C.DelegateID IS NOT NULL;",
            commandTimeout: 90)).ConfigureAwait(false);

        return rows
            .Select(r => new FollowerCustomerRatingFacts(
                r.CustomerId,
                r.DelegateId,
                r.CustomerName,
                r.PhoneNumber,
                r.IsLegal,
                r.DateSaleDevice,
                r.LastPaymentDate,
                r.AmountTotalSales,
                r.ReceiptsTotal,
                r.AmountRemaining,
                r.AmountDaySales))
            .ToLookup(f => f.DelegateId ?? 0);
    }

    private sealed class FactsRow
    {
        public int CustomerId { get; set; }
        public int? DelegateId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsLegal { get; set; }
        public DateTime? DateSaleDevice { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public double AmountTotalSales { get; set; }
        public double ReceiptsTotal { get; set; }
        public double AmountRemaining { get; set; }
        public double AmountDaySales { get; set; }
    }
}
