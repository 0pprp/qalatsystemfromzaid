namespace BE_Company.DTO
{
    public class StatisticsGetDTO
    {
        public int? DelegateID { get; set; }
        public string? DelegateName { get; set; }
        public int? NumberOfCustomer { get; set; }
        public double? AmountPrice { get; set; }
        public double? AmountCost { get; set; }
        public double? AmountDay { get; set; }
        public int? NumberOfItemSale { get; set; }
        public double? AmountReceipt { get; set; }
        public int? NumberOfCustomerZero { get; set; }
        public double? AmountPriceZero { get; set; }
        public double? AmountDayZero { get; set; }
    }

    public class NoStatisticsGetDTO
    {
        public int? DelegateID { get; set; }
        public string? DelegateName { get; set; }
        public int? NumberOfCustomer { get; set; }
        public double? AmountPrice { get; set; }
        public double? AmountCost { get; set; }
        public double? AmountDay { get; set; }
        public int? NumberOfItemSale { get; set; }
        public double? AmountReceipt { get; set; }
    }
}
