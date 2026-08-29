using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BE_DelegateWebApplication.DTO
{
    public class CustomersPaymentsRequestsGetDTO
    {
        public int CustomersPaymentsRequestId { get; set; }
        public int? CustomerId { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? BoundNumber { get; set; }
        public int? DelegateId { get; set; }
        public bool? AccountZero { get; set; }
        public bool? DelegateState { get; set; }
        public double? Amount { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncId { get; set; }
        public bool? SelectState { get; set; }
        public string? Location { get; set; }
    }
}
