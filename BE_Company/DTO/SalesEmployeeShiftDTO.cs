namespace BE_Company.DTO
{
    public class SalesEmployeeShiftDTO
    {
        public int? ShiftID { get; set; }
        public int? UserID { get; set; }
        public DateTime? ShiftDate { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public bool Active { get; set; }
    }

    public class SalesEmployeeTrackPointDTO
    {
        public string? ClientKey { get; set; }
        public DateTime RecordedAt { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
    }

    public class SalesEmployeeTrackSyncDTO
    {
        public int? ShiftID { get; set; }
        public List<SalesEmployeeTrackPointDTO> Points { get; set; } = new();
    }

    public class SalesEmployeeTrackSyncResultDTO
    {
        public int Inserted { get; set; }
        public int Skipped { get; set; }
        public int? ShiftID { get; set; }
    }

    public class SalesCustomerRatingPostDTO
    {
        public int? CustomerID { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public int RatingLevel { get; set; }
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
    }

    public class SalesCustomerRatingGetDTO
    {
        public int? RatingID { get; set; }
        public int? UserID { get; set; }
        public int? CustomerID { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public int RatingLevel { get; set; }
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class SalesEmployeeSearchCustomerDTO
    {
        public int? CustomerID { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? ShopName { get; set; }
        public string? NearestFunctionPoint { get; set; }
        public string? SaleName { get; set; }
        public string? ReceiptName { get; set; }
        public int? DelegateID { get; set; }
        public string? DelegateName { get; set; }
        public string? CityName { get; set; }
    }
}
