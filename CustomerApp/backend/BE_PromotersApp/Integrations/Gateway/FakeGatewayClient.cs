using System.Collections.Concurrent;

namespace BE_PromotersApp.Integrations.Gateway;

/// <summary>
/// Local/demo stand-in for the Sales Gateway (Gateway:UseFake=true). Each sale-progress poll pays another 35% of a
/// sale, so a sale is finished on the third poll. Created sales requests advance one status step per status poll
/// (Assigned → Inspected → Completed), so the whole order → commission flow can be exercised end to end.
/// </summary>
public sealed class FakeGatewayClient : IGatewayClient
{
    private static readonly GatewayBranch[] Branches =
    [
        new(1, "النجف"), new(2, "بغداد - الرصافة"), new(3, "بغداد - الكرخ"), new(4, "كربلاء"), new(5, "الحلة"),
        new(6, "الديوانية"), new(8, "الناصرية"), new(9, "البصرة"), new(10, "المثنى"), new(11, "ديالى"),
        new(12, "الانوار"), new(13, "الموصل"), new(14, "كركوك"),
    ];

    private readonly ConcurrentDictionary<Guid, (GatewaySalesRequestCreate Request, int Id, int Step)> _requests = new();
    private readonly ConcurrentDictionary<(int, int), int> _progressPolls = new();
    private readonly ConcurrentDictionary<Guid, string> _rejections = new();

    /// <summary>Tests / demo: the sales employee rejects this request with a reason.</summary>
    public void Reject(Guid orderUuid, string reason) => _rejections[orderUuid] = reason;
    private int _nextId = 1000;

    public Task<IReadOnlyList<GatewayBranch>> GetBranchesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<GatewayBranch>>(Branches);

    public Task<GatewaySaleProgress?> GetSaleProgressAsync(int cityValue, int saleId, CancellationToken ct)
    {
        var polls = _progressPolls.AddOrUpdate((cityValue, saleId), 1, (_, n) => n + 1);
        const long total = 780_000;
        var paid = Math.Min(total, (long)(total * 0.35 * polls));
        return Task.FromResult<GatewaySaleProgress?>(new GatewaySaleProgress(cityValue, saleId, total, paid, total - paid, polls * 20,
            DateTime.UtcNow.Date));
    }

    public Task<GatewaySalesRequestCreated> CreateSalesRequestAsync(GatewaySalesRequestCreate request, CancellationToken ct)
    {
        var replay = _requests.ContainsKey(request.OrderUuid);
        var entry = _requests.GetOrAdd(request.OrderUuid, _ => (request, Interlocked.Increment(ref _nextId), 0));
        var branch = Branches.FirstOrDefault(b => b.CityValue == request.CityValue);
        return Task.FromResult(new GatewaySalesRequestCreated(request.CityValue, branch?.Name, entry.Id, "New", replay));
    }

    public Task<IReadOnlyList<GatewayRequestStatus>> GetStatusesAsync(IReadOnlyList<GatewayStatusQuery> items, CancellationToken ct)
    {
        string[] steps = ["Assigned", "Inspected", "Completed"];
        var result = new List<GatewayRequestStatus>();
        foreach (var item in items)
        {
            if (!_requests.TryGetValue(item.OrderUuid, out var entry))
            {
                result.Add(new GatewayRequestStatus(item.OrderUuid, item.CityValue, false, null, null, false, null, null, null));
                continue;
            }
            if (_rejections.TryGetValue(item.OrderUuid, out var reason))
            {
                result.Add(new GatewayRequestStatus(item.OrderUuid, item.CityValue, true, entry.Id, "Rejected", false, reason, null, DateTime.UtcNow));
                continue;
            }
            var step = Math.Min(entry.Step, steps.Length - 1);
            _requests[item.OrderUuid] = entry with { Step = entry.Step + 1 };
            result.Add(new GatewayRequestStatus(item.OrderUuid, item.CityValue, true, entry.Id, steps[step], false, null,
                steps[step] == "Completed" ? entry.Id + 50000 : null, DateTime.UtcNow));
        }
        return Task.FromResult<IReadOnlyList<GatewayRequestStatus>>(result);
    }
}
