using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BE_DelegateWebApplication.DTO
{
    public class DelegateInfoGetDTO
    {
        public int DelegateId { get; set; }
        public int? UserId { get; set; }
        public int? CityId { get; set; }
        public string? DelegateName { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public string? DelegateImage { get; set; }
        public bool? DelegateState { get; set; }
        public double? ProfitRatio { get; set; }
        public bool? SelectState { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncId { get; set; }
        public int? BoxId { get; set; }
        public int? BoxBalanceId { get; set; }
        public bool? BalanceSaleState { get; set; }
        public bool? DeviceSaleState { get; set; }
        public bool? BalancePaymentState { get; set; }
        public bool? DevicePaymentState { get; set; }
        public string? ReceiptName { get; set; }
        public bool? UpdateReceipt { get; set; }
        public bool? DeleteReceipt { get; set; }
        public int? NumberOfCustomer { get; set; }
        public int? NumberOfCustomerIsLegal { get; set; }
        public int? NumberOfCustomerIsNotZero { get; set; }
        public int? NumberOfCustomerIsZero { get; set; }
        public double? AmountTotal { get; set; }
        public double? AmountCost { get; set; }
        public double? AmountDay { get; set; }
        public double? AmountRecever { get; set; }
        public double? AmountRemaining { get; set; }
        public double? AmountAccount { get; set; }
        public double? AmountTotalBalance { get; set; }
        public double? AmountCostBalance { get; set; }
        public double? AmountDayBalance { get; set; }
        public double? AmountReceverBalance { get; set; }
        public double? AmountRemainingBalance { get; set; }
        public string? BoxName { get; set; }
        public string BoxNameBalance { get; set; } = null!;
        public string? CityName { get; set; }
        public double? ReceiptRateDevice { get; set; }
        public double? ReceiptRateBalance { get; set; }
        public double? ReceiptRateDayDevice { get; set; }
        public double? ReceiptRateDayBalance { get; set; }
    }
}


