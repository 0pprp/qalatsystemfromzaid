namespace BE_Company.DTO
{
    public class DelegatesDashboardDataGetDTO
    {
        public int? DelegateID { get; set; }
        public string? DelegateName { get; set; }
        public int? NumberOfSale { get; set; }
        public int? NumberOfItemsSales { get; set; }
        public double? AmountTotalSalesDenar { get; set; }
        public double? AmountTotalCostDenar { get; set; }
        public double? AmountDaySalesDenar { get; set; }
        public double? AmountReceipt { get; set; }
        public double? AmountRemaining { get; set; }
        public int? Rate { get; set; }
    }
}
