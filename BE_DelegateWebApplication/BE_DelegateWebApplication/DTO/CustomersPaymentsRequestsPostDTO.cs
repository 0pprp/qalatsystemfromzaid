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

        /// <summary>Stable device / install id from the collector phone (optional until app update).</summary>
        public string? DeviceId { get; set; }
        public string? DeviceModel { get; set; }
        public string? DevicePlatform { get; set; }
        public string? AppVersion { get; set; }
    }

    public class PaymentIdempotentResultDTO
    {
        public bool Success { get; set; }
        public bool AlreadyExists { get; set; }
        /// <summary>True when blocked as same-day same-amount duplicate (distinct ClientPaymentId).</summary>
        public bool DuplicateBlocked { get; set; }
        public int? CustomersPaymentsRequestID { get; set; }
        public string? ClientPaymentId { get; set; }
        public string? Message { get; set; }
    }
}
