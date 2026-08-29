using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BE_DelegateWebApplication.DTO
{
    public class CustomersSalesGetDTO
    {
        public int CustomerSaleId { get; set; }
        public int? UserId { get; set; }
        public int? CustomerId { get; set; }
        public string? Notes { get; set; }
        public DateTime? DateCreate { get; set; }
        public DateTime? DateModify { get; set; }
        public int? BoundNumber { get; set; }
        public int? StoreId { get; set; }
        public int? DelegateId { get; set; }
        public bool? AccountZero { get; set; }
        public bool? DelegateState { get; set; }
        public double? DiscountAmountTotal { get; set; }
        public double? DiscountAmountTotalDay { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncId { get; set; }
        public double? DiscountAmountTotalTwoWay { get; set; }
        public double? DiscountAmountDayTotalTwoWay { get; set; }
        public double? DiscountAmountTotalDenar { get; set; }
        public double? DiscountAmountTotalDayDenar { get; set; }
        public int? MerchantId { get; set; }
        public int? CityId { get; set; }
        public string? CityName { get; set; }
        public string? UserName { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? StoreName { get; set; }
        public string? DelegateName { get; set; }
        public int? NumberOfItemsSales { get; set; }
        public double? AmountTotalDenar { get; set; }
        public double? AmountTotalDayDenar { get; set; }
        public double? AmountTotalSalesDenar { get; set; }
        public double? AmountDaySalesDenar { get; set; }
        public double? AmountTotalCostDenar { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountRemaining { get; set; }
        public string? ItemsNames { get; set; }
    }
}
