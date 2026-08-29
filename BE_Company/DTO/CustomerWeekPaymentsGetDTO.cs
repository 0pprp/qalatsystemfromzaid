namespace BE_Company.DTO
{
    public class CustomerWeekPaymentsGetDTO
    {
        public int? CustomerID { get; set; }
        public int? DelegateID { get; set; }
        public int? UserID { get; set; }
        public int? CityID { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public string? CustomerImage { get; set; }
        public string? Notes { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? CustomerState { get; set; }
        public string? ShopName { get; set; }
        public string? StoreAddress { get; set; }
        public string? NearestFunctionPoint { get; set; }
        public string? StorePhoneNumber { get; set; }
        public string? Neighborhood { get; set; }
        public double? AmountReceverDay { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public bool? SelectState { get; set; }
        public string? SaleName { get; set; }
        public string? ReceiptName { get; set; }
        public bool? IsLegal { get; set; }
        public bool? IsFakeSale { get; set; }
        public string? DelegateName { get; set; }
        public string? UserName { get; set; }
        public string? CityName { get; set; }
        public DateTime? DateSaleDevice { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? CostTotalSales { get; set; }
        public double? AmountDaySales { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
        public double? ReceiptRateDevice { get; set; }
        public int? NumberOfDayDevice { get; set; }
        public string? PhoneNumberCompany { get; set; }
        public int? CountReceiptDevice { get; set; }
        public string? ItemsNames { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public int? NumberOfDayPayment { get; set; }
        public double? Amount1 { get; set; }
        public double? Amount2 { get; set; }
        public double? Amount3 { get; set; }
        public double? Amount4 { get; set; }
        public double? Amount5 { get; set; }
        public double? Amount6 { get; set; }
        public double? Amount7 { get; set; }
        public double? WeekPaid { get; set; }
        public double? PaidPercent { get; set; }
    }
}
