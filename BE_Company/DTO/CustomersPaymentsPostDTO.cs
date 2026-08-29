namespace BE_Company.DTO
{
    public class CustomersPaymentsPostDTO
    {
        public int? UserCreateID { get; set; }
        public int? CustomerID { get; set; }
        public DateTime? PaymentDate { get; set; }
        public double? PaymentAmount { get; set; }
    }
}
