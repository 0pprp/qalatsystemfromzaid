namespace BE_Company.DTO
{
    public class CustomerWeekDecisionGetDTO
    {
        public int? DecisionID { get; set; }
        public int? CustomerID { get; set; }
        public int? UserID { get; set; }
        public string? DecisionType { get; set; }
        public double? WeekPaid { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? PaidPercent { get; set; }
        public DateTime? WeekStartDate { get; set; }
        public DateTime? WeekEndDate { get; set; }
        public DateTime? SnoozeUntil { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? UserName { get; set; }
        public string? UserType { get; set; }
        public bool? IsLegal { get; set; }
        public bool? IsFakeSale { get; set; }
    }
}
