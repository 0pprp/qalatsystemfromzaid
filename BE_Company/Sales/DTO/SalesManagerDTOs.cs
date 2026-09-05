namespace BE_Company.Sales.DTO
{
    public sealed class SalesManagerEmployeeDTO
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string CityValue { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string ShiftStatus { get; set; } = "NoShift";
        public DateTime? ShiftStartedAt { get; set; }
        public DateTime? ShiftCutoffAt { get; set; }
        public DateTime? LastLocationAt { get; set; }
        public double? LastLatitude { get; set; }
        public double? LastLongitude { get; set; }
        public double? LastAccuracy { get; set; }
        public string LocationStatus { get; set; } = "NoShift";
        public string? LastTrackingEvent { get; set; }
        public int? CurrentShiftId { get; set; }
        public int SalesTodayCount { get; set; }
        public int PendingSalesCount { get; set; }
    }

    public sealed class SalesManagerRouteDTO
    {
        public SalesManagerRouteEmployeeDTO Employee { get; set; } = new();
        public SalesManagerRouteShiftDTO? Shift { get; set; }
        public List<SalesManagerRoutePointDTO> Points { get; set; } = [];
        public int TotalPoints { get; set; }
        public int ReturnedPoints { get; set; }
        public bool IsTruncated { get; set; }
    }

    public sealed class SalesManagerRouteEmployeeDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public sealed class SalesManagerRouteShiftDTO
    {
        public int Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime CutoffAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class SalesManagerRoutePointDTO
    {
        public int ShiftId { get; set; }
        public long DeviceSequence { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public DateTime CapturedAt { get; set; }
        public DateTime CapturedAtUtc
        {
            get => CapturedAt;
            set => CapturedAt = value;
        }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public bool IsOfficial { get; set; }
    }

    public sealed class SalesManagerTrackingEventDTO
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public string? Metadata { get; set; }
    }

    public sealed class SalesManagerSaleListItemDTO
    {
        public int SaleId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? CityName { get; set; }
        public string? Province { get; set; }
        public int? CustomerId { get; set; }
        public decimal BaseSalePrice { get; set; }
        public decimal FinalSalePrice { get; set; }
        public decimal DailyInstallment { get; set; }
        public decimal DownPayment { get; set; }
        public int EvaluationLevel { get; set; }
        public string EvaluationName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public sealed class SalesManagerDashboardDTO
    {
        public int EmployeesOnShift { get; set; }
        public int EmployeesOffShift { get; set; }
        public int LiveLocations { get; set; }
        public int SalesToday { get; set; }
        public int PendingSales { get; set; }
        public int NewSalesRequests { get; set; }
    }

    public sealed class SalesLiveLocationDTO
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public int ShiftId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public DateTime CapturedAt { get; set; }
    }

    public sealed class SalesManagerLocationPointDTO
    {
        public int EmployeeId { get; set; }
        public int ShiftId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public DateTime CapturedAtUtc { get; set; }
    }
}
