namespace BE_SalesEmployee.MainFollower.Domain;

public sealed class SaleReviewRecord
{
    public int Id { get; set; }
    public int MissionId { get; set; }
    public string CityValue { get; set; } = "";
    public int SaleId { get; set; }
    public string? CustomerName { get; set; }
    public int Score { get; set; }
    public string? Note { get; set; }
    public decimal ShopStockEstimatedValue { get; set; }
    public decimal EstimatedDailyRevenue { get; set; }
    public string ReviewerUserName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
