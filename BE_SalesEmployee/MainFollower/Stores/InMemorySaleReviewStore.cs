using BE_SalesEmployee.MainFollower.Domain;

namespace BE_SalesEmployee.MainFollower.Stores;

public sealed class InMemorySaleReviewStore : ISaleReviewStore
{
    private readonly object _gate = new();
    private int _seq;
    private readonly List<SaleReviewRecord> _rows = [];

    public Task<SaleReviewRecord> InsertAsync(SaleReviewRecord row, CancellationToken ct)
    {
        lock (_gate)
        {
            var saved = Clone(row);
            saved.Id = ++_seq;
            _rows.Add(saved);
            return Task.FromResult(Clone(saved));
        }
    }

    public Task<SaleReviewRecord?> FindForMissionSaleAsync(int missionId, int saleId, CancellationToken ct)
    {
        lock (_gate)
        {
            var row = _rows
                .Where(r => r.MissionId == missionId && r.SaleId == saleId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .FirstOrDefault();
            return Task.FromResult(row is null ? null : Clone(row));
        }
    }

    public Task<IReadOnlyList<SaleReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct)
    {
        lock (_gate)
        {
            var list = _rows
                .Where(r => r.MissionId == missionId)
                .OrderByDescending(r => r.CreatedAtUtc)
                .Select(Clone)
                .ToList();
            return Task.FromResult<IReadOnlyList<SaleReviewRecord>>(list);
        }
    }

    public Task<IReadOnlyList<SaleReviewRecord>> ListForCityAsync(string cityValue, CancellationToken ct)
    {
        var city = (cityValue ?? "").Trim();
        lock (_gate)
        {
            var list = _rows
                .Where(r => string.Equals(r.CityValue, city, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.CreatedAtUtc)
                .Select(Clone)
                .ToList();
            return Task.FromResult<IReadOnlyList<SaleReviewRecord>>(list);
        }
    }

    private static SaleReviewRecord Clone(SaleReviewRecord row) => new()
    {
        Id = row.Id,
        MissionId = row.MissionId,
        CityValue = row.CityValue,
        SaleId = row.SaleId,
        CustomerName = row.CustomerName,
        Score = row.Score,
        Note = row.Note,
        ShopStockEstimatedValue = row.ShopStockEstimatedValue,
        EstimatedDailyRevenue = row.EstimatedDailyRevenue,
        ReviewerUserName = row.ReviewerUserName,
        CreatedAtUtc = row.CreatedAtUtc
    };
}
