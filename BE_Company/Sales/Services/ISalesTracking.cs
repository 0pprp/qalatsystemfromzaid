using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services
{
    public static class SalesShiftStatuses
    {
        public const string Active = "Active";
        public const string Closed = "Closed";
    }

    public static class SalesShiftCloseReasons
    {
        public const string AutomaticCutoff = "AutomaticCutoff";
        public const string SystemRecovery = "SystemRecovery";
        public const string ManualEnd = "ManualEnd";
    }

    public static class SalesTrackingEventTypes
    {
        public const string ShiftStarted = "SHIFT_STARTED";
        public const string ShiftAutoClosed = "SHIFT_AUTO_CLOSED";
        public const string GpsStarted = "GPS_STARTED";
        public const string GpsDisabled = "GPS_DISABLED";
        public const string GpsEnabled = "GPS_ENABLED";
        public const string InternetLost = "INTERNET_LOST";
        public const string InternetRestored = "INTERNET_RESTORED";
        public const string LocationPermissionDenied = "LOCATION_PERMISSION_DENIED";
        public const string LocationPermissionGranted = "LOCATION_PERMISSION_GRANTED";
        public const string SyncStarted = "SYNC_STARTED";
        public const string SyncCompleted = "SYNC_COMPLETED";
        public const string SyncFailed = "SYNC_FAILED";

        public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        {
            ShiftStarted, ShiftAutoClosed, GpsStarted, GpsDisabled, GpsEnabled,
            InternetLost, InternetRestored, LocationPermissionDenied, LocationPermissionGranted,
            SyncStarted, SyncCompleted, SyncFailed
        };
    }

    public interface ISalesTrackingRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        Task<SalesShiftDTO?> GetActiveByEmployeeAsync(int employeeId, CancellationToken ct);
        Task<SalesShiftDTO?> GetByIdAsync(int shiftId, CancellationToken ct);
        Task<SalesShiftDTO> InsertActiveAsync(
            int employeeId,
            string employeeName,
            string cityValue,
            string cityName,
            DateTime startedAtUtc,
            DateTime startedAtIraq,
            DateTime cutoffAtUtc,
            CancellationToken ct);
        Task CloseAsync(int shiftId, DateTime closedAtUtc, string reason, CancellationToken ct);
        Task CloseExpiredAsync(DateTime utcNow, CancellationToken ct);
        Task<int> TryInsertPointAsync(int employeeId, int shiftId, SalesLocationPointRequestDTO point, DateTime receivedAtUtc, CancellationToken ct);
        Task InsertEventAsync(int employeeId, int? shiftId, string eventType, DateTime occurredAtUtc, string? metadata, CancellationToken ct);
    }

    public interface ISalesShiftService
    {
        Task<SalesShiftDTO> StartAsync(SalesIdentity identity, CancellationToken ct);
        Task<SalesShiftDTO> EndAsync(SalesIdentity identity, CancellationToken ct);
        Task<SalesShiftDTO?> GetCurrentAsync(int employeeId, CancellationToken ct);
        Task<bool> IsShiftStartedAsync(int employeeId, CancellationToken ct);
        Task CloseExpiredAsync(CancellationToken ct);
    }

    public interface ISalesLocationIngestService
    {
        Task<SalesLocationBatchResultDTO> IngestBatchAsync(SalesIdentity identity, SalesLocationBatchRequestDTO request, CancellationToken ct);
        Task RecordEventAsync(SalesIdentity identity, SalesTrackingEventRequestDTO request, CancellationToken ct);
    }
}
