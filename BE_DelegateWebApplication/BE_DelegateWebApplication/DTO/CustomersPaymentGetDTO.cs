using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BE_DelegateWebApplication.DTO
{
    public class CustomersPaymentGetDTO
    {
        public int CustomerPaymentId { get; set; }
        public int? UserId { get; set; }
        public int? CustomerId { get; set; }
        public int? BoxId { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? BoundNumber { get; set; }
        public int? DelegateId { get; set; }
        public bool? AccountZero { get; set; }
        public bool? DelegateState { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncId { get; set; }
        public bool? SelectState { get; set; }
        public string? Location { get; set; }
        public string? UserName { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? BoxName { get; set; }
        public string? DelegateName { get; set; }
        public double? AmountDenar { get; set; }
        public int? CityId { get; set; }
        public string? CityName { get; set; }
    }
}
