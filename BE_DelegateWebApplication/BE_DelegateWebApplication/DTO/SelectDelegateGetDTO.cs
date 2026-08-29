namespace BE_DelegateWebApplication.DTO
{
    public class SelectDelegateGetDTO
    {
        public int SelectDelegateId { get; set; }
        public int? DelegateFatherId { get; set; }
        public int? DelegateChildId { get; set; }
        public int? UserId { get; set; }
        public bool? AsyncState { get; set; }
        public int? DelegateId { get; set; }
        public string? DelegateName { get; set; }
        public string? ReceiptName { get; set; }
        public bool? UpdateReceipt { get; set; }
        public bool? DeleteReceipt { get; set; }
        public bool? DevicePaymentState { get; set; }
    }
}
