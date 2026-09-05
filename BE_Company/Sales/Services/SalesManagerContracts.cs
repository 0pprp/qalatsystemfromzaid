using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services
{
    public static class SalesRequestStatuses
    {
        public const string New = "New";
        public const string Assigned = "Assigned";
        public const string Viewed = "Viewed";
        public const string Pending = "Pending";
        public const string PreparedForSale = "PreparedForSale";
        public const string InProgress = "InProgress";
        public const string ConvertedToSale = "ConvertedToSale";
        public const string Rejected = "Rejected";
        public const string Returned = "Returned";
        public const string Completed = "Completed";

        public static bool IsUnassigned(string? status) =>
            string.Equals(status, New, StringComparison.OrdinalIgnoreCase);

        public static bool IsOpenForEmployee(string? status) =>
            status is Assigned or Viewed or Pending or PreparedForSale or InProgress or Returned;

        public static bool CanPrepare(string? status) =>
            status is Assigned or Viewed or Pending or PreparedForSale or InProgress or Returned;

        public static bool CanPend(string? status) =>
            status is Assigned or Viewed or Pending or PreparedForSale or InProgress or Returned;

        public static bool CanReject(string? status) =>
            status is Assigned or Viewed or Pending or PreparedForSale or InProgress or Returned;

        public static bool CanConvertToSale(string? status) =>
            status is PreparedForSale or InProgress;

        public static bool IsTerminal(string? status) =>
            status is Completed or ConvertedToSale;
    }

    public static class SalesRequestEvents
    {
        public const string Created = "Created";
        public const string Assigned = "Assigned";
        public const string Pending = "Pending";
        public const string PendingNote = "PendingNote";
        public const string PreparedForSale = "PreparedForSale";
        public const string Rejected = "Rejected";
        public const string RejectionReason = "RejectionReason";
        public const string Returned = "Returned";
        public const string ReturnNote = "ReturnNote";
        public const string Completed = "Completed";
        public const string ConvertedToSale = "ConvertedToSale";
        public const string Viewed = "Viewed";
    }

    public static class SalesLocationStatuses
    {
        public const string Live = "Live";
        public const string Stale = "Stale";
        public const string Offline = "Offline";
        public const string NoLocation = "NoLocation";
        public const string NoShift = "NoShift";
    }

    public sealed class SalesManagerTrackingOptions
    {
        public int LiveThresholdSeconds { get; set; } = 60;
        public int StaleThresholdMinutes { get; set; } = 5;
        public int MaxRoutePoints { get; set; } = 10000;
    }

    public static class LiveLocationGate
    {
        public static bool ShouldMoveMarker(DateTime? currentCapturedAtUtc, DateTime incomingCapturedAtUtc)
        {
            if (currentCapturedAtUtc == null)
            {
                return true;
            }

            return incomingCapturedAtUtc > currentCapturedAtUtc.Value;
        }
    }

    public static class SalesLocationStatusService
    {
        public static string Resolve(
            bool hasActiveShift,
            DateTime? lastLocationAtUtc,
            string? lastEventType,
            DateTime utcNow,
            SalesManagerTrackingOptions options)
        {
            if (!hasActiveShift)
            {
                return SalesLocationStatuses.NoShift;
            }

            if (IsOfflineEvent(lastEventType))
            {
                return SalesLocationStatuses.Offline;
            }

            if (lastLocationAtUtc == null)
            {
                return SalesLocationStatuses.NoLocation;
            }

            var age = utcNow - lastLocationAtUtc.Value;
            if (age <= TimeSpan.FromSeconds(options.LiveThresholdSeconds))
            {
                return SalesLocationStatuses.Live;
            }

            if (age <= TimeSpan.FromMinutes(options.StaleThresholdMinutes))
            {
                return SalesLocationStatuses.Stale;
            }

            return SalesLocationStatuses.Stale;
        }

        public static bool IsOfflineEvent(string? eventType) =>
            string.Equals(eventType, SalesTrackingEventTypes.InternetLost, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, SalesTrackingEventTypes.GpsDisabled, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, SalesTrackingEventTypes.LocationPermissionDenied, StringComparison.OrdinalIgnoreCase);
    }

    public interface ISalesRequestRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        Task<SalesRequestDTO> InsertAsync(SalesRequestDTO row, CancellationToken ct);
        Task<SalesRequestDTO?> GetByIdAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<SalesRequestDTO>> ListAsync(int? targetEmployeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
        Task UpdateAsync(SalesRequestDTO row, CancellationToken ct);
        Task<int> CountByStatusAsync(string status, CancellationToken ct);
        Task InsertHistoryAsync(SalesRequestHistoryDTO row, CancellationToken ct);
        Task<IReadOnlyList<SalesRequestHistoryDTO>> ListHistoryAsync(int requestId, CancellationToken ct);
    }

    public interface ISalesRequestService
    {
        Task<SalesRequestDTO> CreateAsync(SalesIdentity actor, SalesRequestCreateDTO request, CancellationToken ct);
        Task<IReadOnlyList<SalesRequestDTO>> ListForManagerAsync(string? status, int? employeeId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
        Task<SalesRequestDTO?> GetForManagerAsync(int id, CancellationToken ct);
        Task<IReadOnlyList<SalesRequestDTO>> ListForEmployeeAsync(int employeeId, CancellationToken ct);
        Task<SalesRequestDTO> GetForEmployeeAsync(int id, int employeeId, CancellationToken ct);
        Task<SalesRequestDTO> ViewAsync(int id, int employeeId, CancellationToken ct);
        Task<SalesRequestDTO> StartProcessingAsync(int id, int employeeId, CancellationToken ct);
        Task<SalesRequestDTO> PrepareForSaleAsync(int id, int employeeId, CancellationToken ct);
        Task<SalesRequestDTO> PendAsync(int id, int employeeId, string note, CancellationToken ct);
        Task<SalesRequestDTO> RejectAsync(int id, int employeeId, string reason, CancellationToken ct);
        Task<SalesRequestDTO> AssignAsync(SalesIdentity manager, int id, SalesRequestAssignDTO request, CancellationToken ct);
        Task<SalesRequestDTO> ReturnAsync(SalesIdentity manager, int id, string note, CancellationToken ct);
        Task MarkConvertedAsync(int requestId, int employeeId, int saleId, DateTime utcNow, CancellationToken ct);
        Task MarkCompletedBySaleIdAsync(int saleId, DateTime utcNow, CancellationToken ct);
    }

    public interface ISalesManagerReadRepository
    {
        Task<IReadOnlyList<SalesManagerEmployeeRow>> ListEmployeesAsync(CancellationToken ct);
        Task<SalesManagerLocationPointDTO?> GetLatestPointAsync(int employeeId, CancellationToken ct);
        Task<SalesManagerTrackingEventDTO?> GetLatestEventAsync(int employeeId, CancellationToken ct);
        Task<SalesShiftDTO?> GetShiftForBusinessDateAsync(int employeeId, DateTime businessDateIraq, CancellationToken ct);
        Task<IReadOnlyList<SalesManagerRoutePointDTO>> GetRouteAsync(int employeeId, DateTime fromUtc, DateTime toUtc, int maxPoints, CancellationToken ct);
        Task<int> CountRouteAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
        Task<IReadOnlyList<SalesManagerTrackingEventDTO>> GetEventsAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
        Task<IReadOnlyList<SalesDraftDTO>> ListSalesAsync(int? employeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
        Task<SalesDraftDTO?> GetSaleAsync(int saleId, CancellationToken ct);
        Task<int> CountSalesTodayAsync(int employeeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
        Task<int> CountPendingSalesAsync(int? employeeId, CancellationToken ct);
    }

    public sealed class SalesManagerEmployeeRow
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
    }
}
