using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Stores;

namespace BE_SalesEmployee.MainFollower.Services;

public sealed class SaleReviewService
{
    private readonly ICommitteeStore _committees;
    private readonly ISaleReviewStore _reviews;

    public SaleReviewService(ICommitteeStore committees, ISaleReviewStore reviews)
    {
        _committees = committees;
        _reviews = reviews;
    }

    public async Task<SaleReviewRecord> SubmitAsync(
        string reviewerUserName,
        int missionId,
        string cityValue,
        int saleId,
        string? customerName,
        int score,
        string? note,
        decimal shopStockEstimatedValue,
        decimal estimatedDailyRevenue,
        CancellationToken ct)
    {
        var err = SaleReviewRules.ValidateWithShop(
            score, note, shopStockEstimatedValue, estimatedDailyRevenue);
        if (err != null)
        {
            throw new CommitteeException(err);
        }

        var missions = await _committees.ListMissionsForMemberAsync(reviewerUserName, ct);
        var mission = missions.FirstOrDefault(m => m.Id == missionId);
        var accessErr = MissionAccessRules.AuthorizeMemberCity(mission, reviewerUserName, cityValue);
        if (accessErr != null)
        {
            throw new CommitteeException(accessErr);
        }

        if (saleId <= 0)
        {
            throw new CommitteeException("معرّف المبيع مطلوب.");
        }

        var existing = await _reviews.FindForMissionSaleAsync(missionId, saleId, ct);
        if (existing != null)
        {
            throw new CommitteeException("سبق أن تمّت مصادقة هذا المبيع.");
        }

        var city = (cityValue ?? "").Trim();
        var row = new SaleReviewRecord
        {
            MissionId = missionId,
            CityValue = city,
            SaleId = saleId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            Score = score,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ShopStockEstimatedValue = decimal.Round(shopStockEstimatedValue, 0, MidpointRounding.AwayFromZero),
            EstimatedDailyRevenue = decimal.Round(estimatedDailyRevenue, 0, MidpointRounding.AwayFromZero),
            ReviewerUserName = (reviewerUserName ?? "").Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        return await _reviews.InsertAsync(row, ct);
    }

    public Task<IReadOnlyList<SaleReviewRecord>> ListForMissionAsync(int missionId, CancellationToken ct) =>
        _reviews.ListForMissionAsync(missionId, ct);
}
