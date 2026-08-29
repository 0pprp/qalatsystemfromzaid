namespace BE_Company.DTO
{
    public class DecisionNotificationGetDTO
    {
        public int? NotificationID { get; set; }
        public int? DecisionID { get; set; }
        public bool? IsRead { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int? CustomerID { get; set; }
        public string? DecisionType { get; set; }
        public double? WeekPaid { get; set; }
        public double? PaidPercent { get; set; }
        public string? Note { get; set; }
        public string? CustomerName { get; set; }
        public string? UserName { get; set; }
    }
}
