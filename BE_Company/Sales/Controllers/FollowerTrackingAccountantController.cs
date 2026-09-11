using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BE_Company.Sales.Controllers
{
    /// <summary>
    /// Main Accountant read API for follower GPS.
    /// Followers list is served from dbo.Users (متابع) locally — never FollowerProfiles / FollowerUserLists.
    /// Live + route still proxy to DelegateWeb when configured (same province GPS tables).
    /// </summary>
    [ApiController]
    [Route("api/follower-tracking")]
    [Authorize(Policy = SalesPolicies.ReadFollowerGps)]
    public sealed class FollowerTrackingAccountantController : ControllerBase
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly IFollowerDirectoryService _directory;
        private readonly ILogger<FollowerTrackingAccountantController> _logger;

        public FollowerTrackingAccountantController(
            IHttpClientFactory http,
            IConfiguration config,
            IFollowerDirectoryService directory,
            ILogger<FollowerTrackingAccountantController> logger)
        {
            _http = http;
            _config = config;
            _directory = directory;
            _logger = logger;
        }

        [HttpGet("live")]
        public Task<IActionResult> Live(CancellationToken ct) =>
            ProxyGet("Followers/tracking/live", ct);

        /// <summary>
        /// Always returns active Users of type متابع from Company DB.
        /// GPS enrichment is best-effort via Delegate proxy; missing GPS is not an error.
        /// </summary>
        [HttpGet("followers")]
        public async Task<IActionResult> Followers(CancellationToken ct)
        {
            var users = await _directory.ListActiveFollowersAsync(ct);
            var enriched = users.Select(u => new FollowerDirectoryItem
            {
                FollowerId = u.FollowerId,
                UserId = u.UserId,
                FollowerName = u.FollowerName,
                CityName = u.CityName,
                CityId = u.CityId,
                HasActiveShift = false,
            }).ToList();

            try
            {
                var liveJson = await ProxyGetBody("Followers/tracking/live", ct);
                if (!string.IsNullOrWhiteSpace(liveJson))
                {
                    using var doc = JsonDocument.Parse(liveJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var byId = new Dictionary<int, JsonElement>();
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var id = ReadInt(el, "followerId", "FollowerId");
                            if (id > 0) byId[id] = el;
                        }

                        foreach (var row in enriched)
                        {
                            if (!byId.TryGetValue(row.FollowerId, out var live)) continue;
                            row.HasActiveShift = true;
                            row.LastLatitude = ReadDouble(live, "latitude", "Latitude", "lastLatitude");
                            row.LastLongitude = ReadDouble(live, "longitude", "Longitude", "lastLongitude");
                            row.LastUpdatedAtUtc = ReadDate(live, "updatedAtUtc", "UpdatedAtUtc", "capturedAtUtc");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Names must still load; empty GPS is not a list failure.
                _logger.LogDebug(ex, "Follower live enrichment skipped");
            }

            return Ok(enriched.Select(u => new
            {
                followerId = u.FollowerId,
                userId = u.UserId,
                followerName = u.FollowerName,
                cityName = u.CityName,
                cityId = u.CityId,
                hasActiveShift = u.HasActiveShift,
                lastLatitude = u.LastLatitude,
                lastLongitude = u.LastLongitude,
                lastUpdatedAtUtc = u.LastUpdatedAtUtc,
            }));
        }

        [HttpGet("followers/{followerId:int}/route")]
        public Task<IActionResult> Route(int followerId, [FromQuery] string date, CancellationToken ct) =>
            ProxyGet($"Followers/tracking/followers/{followerId}/route?date={Uri.EscapeDataString(date ?? "")}", ct);

        private async Task<IActionResult> ProxyGet(string relative, CancellationToken ct)
        {
            var body = await ProxyGetBody(relative, ct);
            if (body == null)
            {
                var baseUrl = (_config["FollowerTracking:DelegateApiBase"] ?? "").TrimEnd('/');
                var key = _config["FollowerTracking:AccountantKey"] ?? "";
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                    {
                        message = "Follower tracking proxy is not configured (FollowerTracking:DelegateApiBase / AccountantKey)."
                    });
                }

                return StatusCode(StatusCodes.Status502BadGateway, new { message = "تعذر الوصول لخدمة تتبع المتابعين." });
            }

            return Content(body, "application/json");
        }

        private async Task<string?> ProxyGetBody(string relative, CancellationToken ct)
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

        private static int ReadInt(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                if (el.TryGetProperty(n, out var p) && p.TryGetInt32(out var v)) return v;
            }
            return 0;
        }

        private static double? ReadDouble(JsonElement el, params string[] names)
        {
            foreach (var n in names)
            {
                if (el.TryGetProperty(n, out var p) && p.TryGetDouble(out var v)) return v;
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
                    return dt.ToUniversalTime();
                }
            }
            return null;
        }
    }
}
