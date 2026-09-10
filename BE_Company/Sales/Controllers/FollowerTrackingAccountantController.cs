using BE_Company.Sales.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers
{
    /// <summary>
    /// Main Accountant read API for follower GPS. Proxies to DelegateWeb follower-tracking endpoints
    /// so write (asyncId) and read share the same province database.
    /// </summary>
    [ApiController]
    [Route("api/follower-tracking")]
    [Authorize(Policy = SalesPolicies.ReadFollowerGps)]
    public sealed class FollowerTrackingAccountantController : ControllerBase
    {
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;
        private readonly ILogger<FollowerTrackingAccountantController> _logger;

        public FollowerTrackingAccountantController(
            IHttpClientFactory http,
            IConfiguration config,
            ILogger<FollowerTrackingAccountantController> logger)
        {
            _http = http;
            _config = config;
            _logger = logger;
        }

        [HttpGet("live")]
        public Task<IActionResult> Live(CancellationToken ct) =>
            ProxyGet("Followers/tracking/live", ct);

        [HttpGet("followers")]
        public Task<IActionResult> Followers(CancellationToken ct) =>
            ProxyGet("Followers/tracking/followers", ct);

        [HttpGet("followers/{followerId:int}/route")]
        public Task<IActionResult> Route(int followerId, [FromQuery] string date, CancellationToken ct) =>
            ProxyGet($"Followers/tracking/followers/{followerId}/route?date={Uri.EscapeDataString(date ?? "")}", ct);

        private async Task<IActionResult> ProxyGet(string relative, CancellationToken ct)
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

            try
            {
                var client = _http.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/{relative.TrimStart('/')}");
                req.Headers.TryAddWithoutValidation("X-Follower-Tracking-Key", key);
                using var res = await client.SendAsync(req, ct);
                var body = await res.Content.ReadAsStringAsync(ct);
                return new ContentResult
                {
                    StatusCode = (int)res.StatusCode,
                    Content = body,
                    ContentType = res.Content.Headers.ContentType?.ToString() ?? "application/json"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Follower tracking proxy failed");
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "تعذر الوصول لخدمة تتبع المتابعين." });
            }
        }
    }
}
