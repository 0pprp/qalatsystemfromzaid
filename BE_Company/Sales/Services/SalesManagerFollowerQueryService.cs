using System.Text.Json;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public interface ISalesManagerFollowerQueryService
    {
        Task<IReadOnlyList<SalesManagerFollowerDTO>> ListFollowersAsync(
            string? shiftStatus,
            string? locationStatus,
            CancellationToken ct);

        Task<IReadOnlyList<SalesLiveLocationDTO>> ListLiveLocationsAsync(CancellationToken ct);

        /// <summary>Raw proxy JSON from Followers/tracking/followers/{id}/route?date= (or null if unreachable).</summary>
        Task<string?> GetRouteJsonAsync(int followerId, DateTime date, CancellationToken ct);
    }

    /// <summary>
    /// Sales Manager Followers tab: directory from <see cref="IFollowerDirectoryService"/>
    /// (dbo.Users, UserType = متابع), GPS enrichment from the same Delegate FollowerTracking
    /// proxy used by <c>FollowerTrackingAccountantController</c>
    /// (FollowerTracking:DelegateApiBase + AccountantKey). Never reads sales employee shift tables.
    /// </summary>
    public sealed class SalesManagerFollowerQueryService : ISalesManagerFollowerQueryService
    {
        private readonly IFollowerDirectoryService _directory;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly SalesManagerTrackingOptions _options;
        private readonly ILogger<SalesManagerFollowerQueryService> _logger;

        public SalesManagerFollowerQueryService(
            IFollowerDirectoryService directory,
            IHttpClientFactory http,
            IConfiguration config,
            SalesManagerTrackingOptions options,
            ILogger<SalesManagerFollowerQueryService> logger)
        {
            _directory = directory;
            _http = http;
            _config = config;
            _options = options;
            _logger = logger;
        }

        public async Task<IReadOnlyList<SalesManagerFollowerDTO>> ListFollowersAsync(
            string? shiftStatus,
            string? locationStatus,
            CancellationToken ct)
        {
            var directory = await _directory.ListActiveFollowersAsync(ct);
            var live = await LoadLiveMapAsync(ct);
            var (cityValue, cityName) = BranchLabel();
            var utc = DateTime.UtcNow;
            var rows = new List<SalesManagerFollowerDTO>();
            foreach (var f in directory)
            {
                var dto = Map(f, live, cityValue, cityName, utc);
                if (!string.IsNullOrWhiteSpace(shiftStatus)
                    && !string.Equals(dto.ShiftStatus, shiftStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(locationStatus)
                    && !string.Equals(dto.LocationStatus, locationStatus, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                rows.Add(dto);
            }

            return rows;
        }

        public async Task<IReadOnlyList<SalesLiveLocationDTO>> ListLiveLocationsAsync(CancellationToken ct)
        {
            var rows = await ListFollowersAsync(SalesShiftStatuses.Active, null, ct);
            return rows
                .Where(r => r.LastLatitude != null && r.LastLongitude != null)
                .Select(r => new SalesLiveLocationDTO
                {
                    EmployeeId = r.FollowerId,
                    EmployeeName = r.FollowerName,
                    CityValue = r.CityValue,
                    CityName = r.CityName,
                    ShiftId = r.CurrentShiftId ?? 0,
                    Latitude = r.LastLatitude!.Value,
                    Longitude = r.LastLongitude!.Value,
                    Accuracy = r.LastAccuracy,
                    CapturedAt = r.LastLocationAt ?? default,
                    DeviceTimestampUtc = r.LastLocationAt ?? default,
                    UpdatedAtUtc = r.LastLocationAt ?? default,
                    LocationStatus = r.LocationStatus,
                    ShiftStatus = r.ShiftStatus
                })
                .ToList();
        }

        public Task<string?> GetRouteJsonAsync(int followerId, DateTime date, CancellationToken ct) =>
            ProxyGetBodyAsync(
                $"Followers/tracking/followers/{followerId}/route?date={Uri.EscapeDataString(date.ToString("yyyy-MM-dd"))}",
                ct);

        private SalesManagerFollowerDTO Map(
            FollowerDirectoryItem f,
            IReadOnlyDictionary<int, FollowerLiveRow> live,
            string cityValue,
            string cityName,
            DateTime utc)
        {
            var hasLive = live.TryGetValue(f.FollowerId, out var loc);
            var locationStatus = hasLive
                ? SalesLocationStatusService.Resolve(true, loc!.CapturedAtUtc, null, utc, _options)
                : SalesLocationStatuses.NoShift;
            return new SalesManagerFollowerDTO
            {
                FollowerId = f.FollowerId,
                FollowerName = f.FollowerName,
                CityValue = cityValue,
                CityName = cityName,
                ShiftStatus = hasLive ? SalesShiftStatuses.Active : "NoShift",
                CurrentShiftId = hasLive ? loc!.ShiftId : null,
                LastLocationAt = loc?.CapturedAtUtc,
                LastLatitude = loc?.Latitude,
                LastLongitude = loc?.Longitude,
                LastAccuracy = loc?.Accuracy,
                LocationStatus = locationStatus
            };
        }

        private async Task<Dictionary<int, FollowerLiveRow>> LoadLiveMapAsync(CancellationToken ct)
        {
            var map = new Dictionary<int, FollowerLiveRow>();
            try
            {
                var json = await ProxyGetBodyAsync("Followers/tracking/live", ct);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return map;
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return map;
                }

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var id = ReadInt(el, "followerId", "FollowerId");
                    if (id <= 0)
                    {
                        continue;
                    }

                    map[id] = new FollowerLiveRow
                    {
                        ShiftId = ReadInt(el, "shiftId", "ShiftId"),
                        Latitude = ReadDouble(el, "latitude", "Latitude"),
                        Longitude = ReadDouble(el, "longitude", "Longitude"),
                        Accuracy = ReadDouble(el, "accuracy", "Accuracy"),
                        CapturedAtUtc = ReadDate(el, "capturedAtUtc", "CapturedAtUtc", "updatedAtUtc", "UpdatedAtUtc")
                    };
                }
            }
            catch (Exception ex)
            {
                // Names must still load; empty GPS is not a list failure.
                _logger.LogDebug(ex, "Follower live map load failed");
            }

            return map;
        }

        private async Task<string?> ProxyGetBodyAsync(string relative, CancellationToken ct)
        {
            var baseUrl = (_config["FollowerTracking:DelegateApiBase"] ?? "").TrimEnd('/');
            var key = _config["FollowerTracking:AccountantKey"] ?? "";
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            try
            {
                var client = _http.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/{relative.TrimStart('/')}");
                req.Headers.TryAddWithoutValidation("X-Follower-Tracking-Key", key);
                using var res = await client.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Follower tracking proxy HTTP {Status} for {Path}", (int)res.StatusCode, relative);
                    return null;
                }

                return await res.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Follower tracking proxy failed for {Path}", relative);
                return null;
            }
        }

        /// <summary>Branch label — same source as SalesManagerQueryService.BranchLabel (never per-follower).</summary>
        private (string CityValue, string CityName) BranchLabel()
        {
            var cityValue = _config["SalesManagement:BranchId"];
            var cityName = _config["SalesManagement:BranchName"];
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                var cs = _config.GetConnectionString("DataBaseConnection");
                cityValue = string.IsNullOrWhiteSpace(cs)
                    ? "branch"
                    : new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).InitialCatalog;
            }

            if (string.IsNullOrWhiteSpace(cityName))
            {
                cityName = cityValue;
            }

            return (cityValue, cityName);
        }

        private static int ReadInt(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                if (el.TryGetProperty(n, out var p) && p.TryGetInt32(out var v))
                {
                    return v;
                }
            }

            return 0;
        }

        private static double? ReadDouble(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                if (el.TryGetProperty(n, out var p) && p.TryGetDouble(out var v))
                {
                    return v;
                }
            }

            return null;
        }

        private static DateTime? ReadDate(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                if (el.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(p.GetString(), out var dt))
                {
                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
            }

            return null;
        }

        private sealed class FollowerLiveRow
        {
            public int ShiftId { get; set; }
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
            public double? Accuracy { get; set; }
            public DateTime? CapturedAtUtc { get; set; }
        }
    }
}
