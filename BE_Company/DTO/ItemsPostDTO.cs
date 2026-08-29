namespace BE_Company.DTO
{
    public class ItemsPostDTO
    {
        public int? StoreID { get; set; }
        public int? UserCreateID { get; set; }
        public string? ItemName { get; set; }
        public double? ItemPriceDenar { get; set; }
        public double? ItemCostDenar { get; set; }
        public double? AmountDayDenar { get; set; }
        public int? Quantity { get; set; }
        public int? NotificationNumber { get; set; }
        public string? Notes { get; set; }
    }
}
