namespace BE_Company.DTO
{
    public class CustomersPaymentsRequestGetDTO
    {
        public int? CustomersPaymentsRequestID { get; set; }
        public int? CustomerID { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? DelegateID { get; set; }
        public float? Amount { get; set; }
        public string? Location { get; set; }
        public string? CustomerName { get; set; }
        public string? DelegateName { get; set; }
        public float? AmountTotalSales { get; set; }
        public float? AmountDaySales { get; set; }
        public float? ReceiptsTotal { get; set; }
        public float? AmountRemaining { get; set; }
    }
}
