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
        /// <summary>All-time average score (latest as-of today) — التقييم الحقيقي.</summary>
        public double? LatestRating { get; set; }
        public string? RatingLabel { get; set; }
        public int? RatedCustomerCount { get; set; }
        /// <summary>«التقييم الوهمي» for the same cohort.</summary>
        public double? PhantomLatestRating { get; set; }
        public string? PhantomRatingLabel { get; set; }
    }

    /// <summary>
    /// Same shape as <see cref="SalesManagerEmployeeDTO"/> for the parallel Followers tab.
    /// GPS comes from the Delegate FollowerTracking proxy — not sales shift tables.
    /// </summary>
    public sealed class SalesManagerFollowerDTO
    {
        public int FollowerId { get; set; }
        public string FollowerName { get; set; } = string.Empty;
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
        /// <summary>All-time average score (latest as-of today) from assigned lists — التقييم الحقيقي.</summary>
        public double? LatestRating { get; set; }
        public string? RatingLabel { get; set; }
        public int? RatedCustomerCount { get; set; }
        /// <summary>«التقييم الوهمي» for the same cohort.</summary>
        public double? PhantomLatestRating { get; set; }
        public string? PhantomRatingLabel { get; set; }
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
        public string? CityValue { get; set; }
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
        public int Year { get; set; }
        public int Month { get; set; }
        public int DaysInMonth { get; set; }
        public string CityValue { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        /// <summary>Completed official sales in the month. Deleted sales are excluded.</summary>
        public int MonthSalesCount { get; set; }
        public decimal MonthSalePrice { get; set; }
        /// <summary>Payments since each sale, capped at that sale price. Not every cash receipt in the month.</summary>
        public decimal MonthCollected { get; set; }
        public decimal CollectedPercent { get; set; }
        public int PreviousMonthSalesCount { get; set; }
        public decimal PreviousMonthSalePrice { get; set; }
        public decimal? SalesCountGrowthPercent { get; set; }
        public decimal? SalePriceGrowthPercent { get; set; }
        public decimal AverageSalePrice { get; set; }
        public decimal MonthDownPayment { get; set; }
        public int MonthItemQuantity { get; set; }
        public int OrdersCreated { get; set; }
        public int OrdersConverted { get; set; }
        public int OrdersFinished { get; set; }
        public int OrdersRejected { get; set; }
        public int OrdersOpen { get; set; }
        public int OrdersPending { get; set; }
        public int OrdersUnassigned { get; set; }
        public decimal OrderConversionPercent { get; set; }
        /// <summary>Official month sales whose customer has an app payment rating.</summary>
        public int ScoredSalesCount { get; set; }
        public int UnscoredSalesCount { get; set; }
        public int ExcellentSales { get; set; }
        public int GoodSales { get; set; }
        public int WeakSales { get; set; }
        public int LaggingSales { get; set; }
        public int BadSales { get; set; }
        public int HighScoreSales { get; set; }
        public int LowScoreSales { get; set; }
        public decimal ScoreSum { get; set; }
        public decimal? AverageSaleScore { get; set; }
        public List<SalesOverviewDayDTO> Daily { get; set; } = [];
        public List<SalesOverviewItemDTO> Items { get; set; } = [];
        public List<SalesOverviewCategoryDTO> Categories { get; set; } = [];
        public List<SalesOverviewPersonDTO> Recommenders { get; set; } = [];
        public List<SalesOverviewPersonDTO> Sellers { get; set; } = [];
        public List<SalesOverviewScoreDTO> SaleScores { get; set; } = [];
        public List<SalesOverviewStatusDTO> OrderStatuses { get; set; } = [];
        public string? OverviewError { get; set; }
    }

    public sealed class SalesOverviewDayDTO
    {
        public int Day { get; set; }
        public int SalesCount { get; set; }
        public decimal SalePrice { get; set; }
    }

    public sealed class SalesOverviewItemDTO
    {
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int SalesCount { get; set; }
        public decimal SalePrice { get; set; }
    }

    public sealed class SalesOverviewCategoryDTO
    {
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int SalesCount { get; set; }
        public decimal SalePrice { get; set; }
    }

    public sealed class SalesOverviewPersonDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public decimal SalePrice { get; set; }
        public int ScoredSales { get; set; }
        public int HighScores { get; set; }
        public int LowScores { get; set; }
        public int ExcellentSales { get; set; }
        public int GoodSales { get; set; }
        public int WeakSales { get; set; }
        public int LaggingSales { get; set; }
        public int BadSales { get; set; }
        public decimal ScoreSum { get; set; }
        public decimal? AverageScore { get; set; }
    }

    public sealed class SalesOverviewScoreDTO
    {
        public string Label { get; set; } = string.Empty;
        public int Score { get; set; }
        public int Count { get; set; }
        public decimal SalePrice { get; set; }
    }

    public sealed class SalesOverviewStatusDTO
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public sealed class SalesDayDetailDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Day { get; set; }
        public string CityValue { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public int SalesCount { get; set; }
        public decimal SalePrice { get; set; }
        public decimal AverageSalePrice { get; set; }
        public decimal DownPayment { get; set; }
        public int ItemQuantity { get; set; }
        public int OrdersAdded { get; set; }
        public int OrdersCompleted { get; set; }
        public int OrdersRejected { get; set; }
        public List<SalesDayPersonDTO> Sellers { get; set; } = [];
        public List<SalesDayPersonDTO> Creators { get; set; } = [];
        public List<SalesDayAgentDTO> Agents { get; set; } = [];
        public List<SalesOverviewStatusDTO> OrderStatuses { get; set; } = [];
        public List<SalesOverviewItemDTO> Items { get; set; } = [];
        public string? DayError { get; set; }
    }

    public sealed class SalesDayPersonDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal SalePrice { get; set; }
    }

    public sealed class SalesDayAgentDTO
    {
        public string Name { get; set; } = string.Empty;
        public int Orders { get; set; }
        public List<SalesOverviewStatusDTO> Statuses { get; set; } = [];
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
        public double? Speed { get; set; }
        public double? Heading { get; set; }
        public DateTime CapturedAt { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime DeviceTimestampUtc { get; set; }
        public string LocationStatus { get; set; } = "Live";
        public string ShiftStatus { get; set; } = "Active";
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
