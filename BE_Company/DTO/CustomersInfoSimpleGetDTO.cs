namespace BE_Company.DTO
{
    public class CustomersInfoSimpleGetDTO
    {
        public int? CustomerID { get; set; }
        public int? DelegateID { get; set; }
        public string? CustomerName { get; set; }
        public string? DelegateName { get; set; }
        public double? AmountDaySales { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? AmountRemaining { get; set; }
    }
}
