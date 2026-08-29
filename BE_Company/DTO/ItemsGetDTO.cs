namespace BE_Company.DTO
{
    public class ItemsGetDTO
    {
        public int? ItemID { get; set; }
        public int? StoreID { get; set; }
        public int? UserID { get; set; }
        public string? ItemName { get; set; }
        public double? ItemPrice { get; set; }
        public double? ItemCost { get; set; }
        public int? Quantity { get; set; }
        public string? ItemImage { get; set; }
        public string? Notes { get; set; }
        public int? NotificationNumber { get; set; }
        public double? AmountDay { get; set; }
        public int? NumberOfSales { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public string? Link { get; set; }
        public bool? ItemState { get; set; }
        public string? UserName { get; set; }
        public string? StoreName { get; set; }
        public double? ItemPriceDenar { get; set; }
        public double? ItemCostDenar { get; set; }
        public double? AmountDayDenar { get; set; }
        public int? NumberOfItemsSales { get; set; }
        public int? NumberOfItemsBuys { get; set; }
        public double? PriceTotalItem { get; set; }
        public double? CostTotalItem { get; set; }
    }
}
