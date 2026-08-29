namespace BE_DelegateWebApplication.DTO
{
    public class CustomersPaymentsRequestsPostDTO
    {
        public int? CustomerId { get; set; }
        public int? DelegateId { get; set; }
        public double? Amount { get; set; }
        public string? Location { get; set; }
    }
}
