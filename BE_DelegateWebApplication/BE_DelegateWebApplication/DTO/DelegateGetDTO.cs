using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BE_DelegateWebApplication.DTO
{
    public class DelegateGetDTO
    {
        public int DelegateId { get; set; }
        public int? UserId { get; set; }
        public int? CityId { get; set; }
        public string? DelegateName { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public bool? DeviceSaleState { get; set; }
        public string? AsyncId { get; set; }
        public bool? DevicePaymentState { get; set; }
        public string? ReceiptName { get; set; }
        public bool? UpdateReceipt { get; set; }
        public bool? DeleteReceipt { get; set; }
    }
}
