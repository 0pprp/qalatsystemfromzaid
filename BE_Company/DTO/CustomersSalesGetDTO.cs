namespace BE_Company.DTO
{
    public class CustomersSalesGetDTO
    {
        public int? CustomerSaleID { get; set; }
        public int? UserID { get; set; }
        public int? CustomerID { get; set; }
        public string? Notes { get; set; }
        public DateTime? DateCreate { get; set; }
        public DateTime? DateModify { get; set; }
        public int? BoundNumber { get; set; }
        public int? StoreID { get; set; }
        public int? DelegateID { get; set; }
        public bool? AccountZero { get; set; }
        public bool? DelegateState { get; set; }
        public double? DiscountAmountTotal { get; set; }
        public double? DiscountAmountTotalDay { get; set; }
        public double? DiscountAmountTotalDenar { get; set; }
        public double? DiscountAmountTotalDayDenar { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public double? DiscountAmountTotalTwoWay { get; set; }
        public double? DiscountAmountDayTotalTwoWay { get; set; }
        public int? MerchantID { get; set; }
        public int? CityID { get; set; }
        public string? CityName { get; set; }
        public string? UserName { get; set; }
        public string? CustomerName { get; set; }
        public string? SaleName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? StoreName { get; set; }
        public string? DelegateName { get; set; }
        public int? NumberOfItemsSales { get; set; }
        public double? AmountTotalDenar { get; set; }
        public double? AmountTotalDayDenar { get; set; }
        public double? AmountTotalSalesDenar { get; set; }
        public double? AmountDaySalesDenar { get; set; }
        public double? AmountTotalCostDenar { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
        public string? ItemsNames { get; set; }
        public int? CountReceiptDevice { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }
}
