using System.Text.Json;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ShiftController : ControllerBase
    {
        private readonly BranchProxyService _proxy;

        public ShiftController(BranchProxyService proxy)
        {
            _proxy = proxy;
        }

        [HttpGet("Status")]
        public async Task<IActionResult> Status(CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            using var response = await _proxy.SendAuthorizedAsync(
                user.CityLink, "SalesEmployee/ShiftStatus", HttpMethod.Get, user.BranchToken, null, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if ((int)response.StatusCode == 404)
            {
                return Ok(null);
            }
            return StatusCode((int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : BranchProxyService.TryParseJson(body));
        }

        [HttpPost("Start")]
        public async Task<IActionResult> Start(CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            using var response = await _proxy.SendAuthorizedAsync(
                user.CityLink, "SalesEmployee/ShiftStart", HttpMethod.Post, user.BranchToken, null, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if ((int)response.StatusCode == 404)
            {
                var now = DateTime.UtcNow.AddHours(3);
                var date = now.Hour < 3 ? now.Date.AddDays(-1) : now.Date;
                return Ok(new
                {
                    shiftID = 0,
                    shiftDate = date,
                    startedAt = now,
                    endsAt = date.AddDays(1).AddHours(3),
                    active = true,
                    localFallback = true
                });
            }
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, body);
            }
            return Ok(BranchProxyService.TryParseJson(body));
        }
    }

    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TrackController : ControllerBase
    {
        private readonly BranchProxyService _proxy;

        public TrackController(BranchProxyService proxy)
        {
            _proxy = proxy;
        }

        [HttpPost("Sync")]
        public async Task<IActionResult> Sync([FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            using var response = await _proxy.SendAuthorizedAsync(
                user.CityLink,
                "SalesEmployee/TrackSync",
                HttpMethod.Post,
                user.BranchToken,
                BranchProxyService.JsonContent(JsonSerializer.Deserialize<object>(body.GetRawText())!),
                ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if ((int)response.StatusCode == 404)
            {
                return Ok(new { inserted = 0, skipped = 0, deferred = true });
            }
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, text);
            }
            return Ok(BranchProxyService.TryParseJson(text));
        }
    }

    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class RatingsController : ControllerBase
    {
        private readonly BranchProxyService _proxy;

        public RatingsController(BranchProxyService proxy)
        {
            _proxy = proxy;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var level = 0;
            if (body.TryGetProperty("ratingLevel", out var levelEl) || body.TryGetProperty("RatingLevel", out levelEl))
            {
                int.TryParse(levelEl.ToString(), out level);
            }
            if (level == 1)
            {
                var reason = "";
                if (body.TryGetProperty("rejectionReason", out var reasonEl) || body.TryGetProperty("RejectionReason", out reasonEl))
                {
                    reason = reasonEl.ToString();
                }
                if (string.IsNullOrWhiteSpace(reason))
                {
                    return BadRequest(new { message = "سبب الرفض إلزامي للتقييم المرفوض" });
                }
            }

            using var response = await _proxy.SendAuthorizedAsync(
                user.CityLink,
                "SalesEmployee/Ratings",
                HttpMethod.Post,
                user.BranchToken,
                BranchProxyService.JsonContent(JsonSerializer.Deserialize<object>(body.GetRawText())!),
                ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if ((int)response.StatusCode == 404)
            {
                return Ok(new { ratingID = 0, localFallback = true });
            }
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, text);
            }
            return Ok(BranchProxyService.TryParseJson(text));
        }
    }
}
