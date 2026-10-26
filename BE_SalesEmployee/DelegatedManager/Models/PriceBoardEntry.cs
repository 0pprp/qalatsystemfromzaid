namespace BE_SalesEmployee.DelegatedManager.Models;

public sealed class PriceBoardEntry
{
    public int Id { get; set; }
    public string ProductName { get; set; } = "";
    public decimal SalePrice { get; set; }
    public decimal DailyInstallment { get; set; }
    public decimal DownPayment { get; set; }
    public string TargetCityValue { get; set; } = "";
    public string TargetCityName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? CreatedByName { get; set; }
    public string? UpdatedByName { get; set; }
}
