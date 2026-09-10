namespace BE_DelegateWebApplication.DTO
{
    public class CustomersPaymentsRequestsPostDTO
    {
        public int? CustomerId { get; set; }
        public int? DelegateId { get; set; }
        public double? Amount { get; set; }
        public string? Location { get; set; }
        public string? ClientPaymentId { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
        public string? ReceiptNumber { get; set; }
        public string? AsyncId { get; set; }
    }

    public class PaymentIdempotentResultDTO
    {
        public bool Success { get; set; }
        public bool AlreadyExists { get; set; }
        public int? CustomersPaymentsRequestID { get; set; }
        public string? ClientPaymentId { get; set; }
        public string? Message { get; set; }
    }
}
