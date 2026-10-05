using BE_SalesEmployee.MainFollower.Domain;

namespace BE_SalesEmployee.MainFollower.Stores;

public interface ISaleReviewStore
{
    Task<SaleReviewRecord> InsertAsync(SaleReviewRecord row, CancellationToken ct);
    Task<SaleReviewRecord?> FindForMissionSaleAsync(int missionId, int saleId, CancellationToken ct);
    Task<IReadOnlyList<SaleReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct);
    Task<IReadOnlyList<SaleReviewRecord>> ListForCityAsync(string cityValue, CancellationToken ct);
}
