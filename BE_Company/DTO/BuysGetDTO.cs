namespace BE_Company.DTO
{
    public class BuysGetDTO
    {
        public int? BuyID { get; set; }
        public int? UserID { get; set; }
        public int? SupplierID { get; set; }
        public int? BoundNumber { get; set; }
        public string? Recipient { get; set; }
        public string? Notes { get; set; }
        public DateTime? DateCreate { get; set; }
        public DateTime? DateModify { get; set; }
        public int? StoreID { get; set; }
        public bool? BuyState { get; set; }
        public int? BoxID { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public string? UserName { get; set; }
        public string? SupplierName { get; set; }
        public int? CityID { get; set; }
        public string? CityName { get; set; }
        public string? StoreName { get; set; }
        public string? BoxName { get; set; }
        public double? TotalAmountDenar { get; set; }
        public double? AmountSpentDenar { get; set; }
        public int? NumberOfItemsBuys { get; set; }
        public string? ItemsNames { get; set; }
    }
}
