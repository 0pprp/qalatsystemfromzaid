namespace BE_CustomerApp.Domain.Orders;

/// <summary>The customer: either FullName (one field, at least three names) or the name parts.</summary>
public sealed record OrderCustomerInput(
    string? FirstName, string? FatherName, string? GrandfatherName, string? Surname, string? Phone, string? SecondaryPhone, string? FullName = null);
public sealed record OrderAddressInput(string? Area, string? Details, string? NearestLandmark, decimal? Latitude, decimal? Longitude);
public sealed record OrderShopInput(string? Name, string? BusinessType, string? Address, bool? OwnsShop);
/// <summary>What was sold, typed by the agent (no catalog): e.g. «ثلاجة سامسونج 18 قدم» × 1.</summary>
public sealed record OrderItemInput(string? Name, int Quantity);

/// <summary>An agent's sale order. Required: branch, item, customer name and phone, address. Shop photo and GPS location are optional.
/// Sales are daily installments only: PurchaseType defaults to DailyInstallments.</summary>
public sealed record CreateOrderInput(
    int CityValue, string? PurchaseType, OrderCustomerInput? Customer, OrderAddressInput? Address,
    OrderShopInput? Shop, IReadOnlyList<OrderItemInput>? Items, string? Notes, string? ShopPhotoUrl = null);

public sealed record OrderCustomerDto(string FirstName, string FatherName, string GrandfatherName, string? Surname, string FullName, string Phone, string? SecondaryPhone);
public sealed record OrderAddressDto(string Area, string Details, string? NearestLandmark, decimal? Latitude, decimal? Longitude);
public sealed record OrderShopDto(string? Name, string? BusinessType, string? Address, bool? OwnsShop);
public sealed record OrderItemDto(string Name, int Quantity);
public sealed record TimelineDto(string Status, string StatusLabel, DateTime At, string? Note);

/// <summary>Payment progress of a sold order in the company system, until it is fully paid.</summary>
public sealed record SaleProgressDto(
    int? SaleId, long Total, long Paid, long Remaining, int PaymentsCount, DateTime? LastPaymentAt, int ProgressPercent,
    DateTime? SoldAt, DateTime? FinishedAt, DateTime? SyncedAt);

public sealed record OrderSummaryDto(
    long Id, string OrderNumber, string Status, string StatusLabel, string PurchaseType, string CustomerName,
    string CityName, string ItemsSummary, DateTime CreatedAt, DateTime UpdatedAt, SaleProgressDto? Sale = null);

public sealed record OrderDetailDto(
    long Id, string OrderNumber, string Status, string StatusLabel, string PurchaseType, string CustomerName,
    string CityName, string ItemsSummary, DateTime CreatedAt, DateTime UpdatedAt,
    int CityValue, OrderCustomerDto Customer, OrderAddressDto Address, OrderShopDto? Shop, string? Notes,
    IReadOnlyList<OrderItemDto> Items, string? PublicReason, IReadOnlyList<TimelineDto> Timeline, bool CanCancel,
    string? ShopPhotoUrl = null, SaleProgressDto? Sale = null);

/// <summary>The app's home page («الرئيسية»).</summary>
public sealed record AgentOverviewDto(
    int OrdersToday, int OrdersThisMonth, int SoldThisMonth, int OpenOrders, int WaitingPayment, int FullyPaid, int NotSoldThisMonth,
    int PinsThisMonth, IReadOnlyList<OrderSummaryDto> RecentOrders, int PinsTotal = 0, IReadOnlyList<PinsByInterestDto>? PinsByInterest = null,
    int DailyPinTarget = 0, int PinsToday = 0);

/// <summary>How many of the agent's pinned shops (all time, not deleted) are at one interest level (1 … 5).</summary>
public sealed record PinsByInterestDto(int Interest, string Label, int Count);

public sealed record SubmittedByDto(long UserId, string FullName, string Phone);

public sealed record AdminOrderRow(
    long Id, string OrderNumber, string Status, string StatusLabel, string PurchaseType, int CityValue, string CityName,
    string CustomerName, string CustomerPhone, string FirstName, string FatherName, string GrandfatherName, string? Surname,
    string ItemsSummary, SubmittedByDto SubmittedBy, DateTime CreatedAt,
    int? SalesRequestId, int OtherOpenOrdersSamePhone);

public sealed record AdminTimelineDto(string Status, string StatusLabel, DateTime At, string? Note, bool IsInternal, string? Actor);

public sealed record LinkedSalesRequestDto(int CityValue, int? SalesRequestId, string? BranchStatus, string? BranchRejectReason, DateTime? LastSyncAt);

public sealed record AdminOrderDetailDto(
    AdminOrderRow Order, OrderCustomerDto Customer, OrderAddressDto Address, OrderShopDto? Shop, string? Notes,
    IReadOnlyList<OrderItemDto> Items, string? PublicReason, string? InternalNote, IReadOnlyList<AdminTimelineDto> Timeline,
    LinkedSalesRequestDto SalesRequest, string? ReviewedBy, string? DecidedBy, DateTime? DecidedAt,
    string? ShopPhotoUrl = null, SaleProgressDto? Sale = null);

/// <summary>Flat DB row for agents.Orders.</summary>
public sealed class OrderRow
{
    public long Id { get; set; }
    public Guid OrderUuid { get; set; }
    public string OrderNumber { get; set; } = "";
    public long SubmittedByUserId { get; set; }
    public int CityValue { get; set; }
    public string CityName { get; set; } = "";
    public string PurchaseType { get; set; } = "";
    public bool ForSelf { get; set; }
    public string FirstName { get; set; } = "";
    public string FatherName { get; set; } = "";
    public string GrandfatherName { get; set; } = "";
    public string? Surname { get; set; }
    public string CustomerPhone { get; set; } = "";
    public string? SecondaryPhone { get; set; }
    public string Area { get; set; } = "";
    public string AddressDetails { get; set; } = "";
    public string? NearestLandmark { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ShopName { get; set; }
    public string? ShopBusinessType { get; set; }
    public string? ShopAddress { get; set; }
    public bool? OwnsShop { get; set; }
    public string? Notes { get; set; }
    public long EstimatedTotal { get; set; }
    public string Status { get; set; } = "";
    public string? PublicReason { get; set; }
    public string? InternalNote { get; set; }
    public int? SalesRequestId { get; set; }
    public string? BranchStatus { get; set; }
    public string? BranchRejectReason { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime? SoldAt { get; set; }
    public string? ShopPhotoUrl { get; set; }
    public int? SaleId { get; set; }
    public long? SaleTotal { get; set; }
    public long? SalePaid { get; set; }
    public int? SalePaymentsCount { get; set; }
    public DateTime? SaleLastPaymentAt { get; set; }
    public DateTime? SaleProgressSyncAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string CustomerName => string.Join(' ', new[] { FirstName, FatherName, GrandfatherName, Surname }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
