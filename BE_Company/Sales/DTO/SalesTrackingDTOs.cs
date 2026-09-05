namespace BE_Company.Sales.DTO
{
    public sealed class SalesShiftDTO
    {
        public int ShiftId { get; set; }
        public int EmployeeId { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime StartedAt { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime StartedAtIraq { get; set; }
        public DateTime CutoffAt { get; set; }
        public DateTime CutoffAtUtc { get; set; }
        public DateTime? ClosedAtUtc { get; set; }
        public string? CloseReason { get; set; }
        public bool IsNew { get; set; }
        public bool HasActiveShift { get; set; }
    }

    public sealed class SalesLocationPointRequestDTO
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double? Accuracy { get; set; }
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public double? Altitude { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public DateTime? OfficialSlotUtc { get; set; }
        public DateTime? ActualCapturedAtUtc { get; set; }
        public bool IsOfficial { get; set; } = true;
        public long DeviceSequence { get; set; }
        public string? DeviceSessionId { get; set; }
    }

    public sealed class SalesLocationBatchRequestDTO
    {
        public int ShiftId { get; set; }
        public List<SalesLocationPointRequestDTO> Points { get; set; } = [];
    }

    public sealed class SalesLocationBatchResultDTO
    {
        public int ShiftId { get; set; }
        public string ShiftStatus { get; set; } = string.Empty;
        public int Accepted { get; set; }
        public int Duplicates { get; set; }
        public int Rejected { get; set; }
        public SalesLiveLocationDTO? LiveUpdate { get; set; }
    }

    public sealed class SalesTrackingEventRequestDTO
    {
        public int? ShiftId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime? OccurredAtUtc { get; set; }
        public string? Metadata { get; set; }
    }
}
