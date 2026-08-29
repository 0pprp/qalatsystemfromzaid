namespace BE_Company.DTO
{
    public class StoresGetDTO
    {
        public int? StoreID { get; set; }
        public int? UserID { get; set; }
        public string? StoreName { get; set; }
        public string? StorePlace { get; set; }
        public string? Notes { get; set; }
        public int? CityID { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public bool? State { get; set; }
        public string? UserName { get; set; }
        public string? CityName { get; set; }
        public double? TotalPrice { get; set; }
        public double? TotalCost { get; set; }
        public double? AmountExchange { get; set; }
        public double? CostSalesItemsCurrent { get; set; }
        public double? CostBuyItemsCurrent { get; set; }
        public int? NumberOfTypes { get; set; }
        public int? NumberOfItems { get; set; }
        public int? NumberOfItemBuy { get; set; }
        public int? NumberOfItemsSales { get; set; }
        public double? AmountItemBuy { get; set; }
    }
}
