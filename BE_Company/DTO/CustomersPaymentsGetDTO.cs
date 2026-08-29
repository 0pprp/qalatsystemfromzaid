namespace BE_Company.DTO
{
    public class CustomersPaymentsGetDTO
    {
        public int? CustomerPaymentID { get; set; }
        public int? UserID { get; set; }
        public int? CustomerID { get; set; }
        public int? BoxID { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? BoundNumber { get; set; }
        public int? DelegateID { get; set; }
        public bool? AccountZero { get; set; }
        public bool? DelegateState { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public bool? SelectState { get; set; }
        public string? Location { get; set; }
        public string? UserName { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? BoxName { get; set; }
        public string? DelegateName { get; set; }
        public double? AmountDenar { get; set; }
        public double? AmountDaySales { get; set; }
        public int? CityID { get; set; }
        public string? CityName { get; set; }
        public string? ItemsNames { get; set; }
    }
}
