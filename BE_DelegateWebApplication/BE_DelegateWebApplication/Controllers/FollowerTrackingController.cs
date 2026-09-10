using BE_DelegateWebApplication.Services.FollowerIdentity;
using BE_DelegateWebApplication.Services.FollowerTracking;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [ApiController]
    [Route("api/Followers")]
    public sealed class FollowerTrackingController : ControllerBase
    {
        private readonly IFollowerTrackingService _tracking;
        private readonly IFollowerIdentityService _identity;

        public FollowerTrackingController(IFollowerTrackingService tracking, IFollowerIdentityService identity)
        {
            _tracking = tracking;
            _identity = identity;
        }

        /// <summary>
        /// Follower login: Users.AsyncID where UserType = متابع and UserState active.
        /// No FollowerProfiles gate.
        /// </summary>
        [HttpGet("Login")]
        public async Task<IActionResult> Login([FromQuery] string asyncId, CancellationToken ct)
        {
            var follower = await _identity.ResolveByAsyncIdAsync(asyncId, ct);
            if (follower == null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "المستخدم ليس من نوع متابع أو الحساب غير فعال."
                });
            }

            return Ok(new
            {
                userId = follower.UserId,
                userName = follower.UserName,
                asyncId = follower.AsyncId,
                userType = follower.UserType,
                cityId = follower.CityId,
                cityName = follower.CityName,
                isActive = follower.IsActive,
                // Compat aliases for older Flutter session keys (identity is User, not Delegate).
                delegateId = follower.UserId,
                delegateName = follower.UserName,
            });
        }

        [HttpPost("shifts/start")]
        public async Task<IActionResult> StartShift([FromQuery] string asyncId, CancellationToken ct)
        {
            var follower = await Auth(asyncId, ct);
            if (follower == null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مصرح." });
            try
            {
                return Ok(await _tracking.StartAsync(follower, ct));
            }
            catch (FollowerTrackingException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("shifts/end")]
        public async Task<IActionResult> EndShift([FromQuery] string asyncId, CancellationToken ct)
        {
            var follower = await Auth(asyncId, ct);
            if (follower == null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مصرح." });
            try
            {
                return Ok(await _tracking.EndAsync(follower, ct));
            }
            catch (FollowerTrackingException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("shifts/current")]
        public async Task<IActionResult> CurrentShift([FromQuery] string asyncId, CancellationToken ct)
        {
            var follower = await Auth(asyncId, ct);
            if (follower == null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مصرح." });
            var shift = await _tracking.CurrentAsync(follower.UserId, ct);
            return Ok(shift);
        }

        [HttpPost("locations/batch")]
        public async Task<IActionResult> LocationsBatch(
            [FromQuery] string asyncId,
            [FromBody] FollowerLocationBatchRequestDto body,
            CancellationToken ct)
        {
            var follower = await Auth(asyncId, ct);
            if (follower == null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مصرح." });
            try
            {
                return Ok(await _tracking.IngestBatchAsync(follower, body, ct));
            }
            catch (FollowerTrackingException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("locations/live")]
        public async Task<IActionResult> LocationsLive(
            [FromQuery] string asyncId,
            [FromBody] FollowerLiveLocationRequestDto body,
            CancellationToken ct)
        {
            var follower = await Auth(asyncId, ct);
            if (follower == null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مصرح." });
            try
            {
                return Ok(await _tracking.IngestLiveAsync(follower, body, ct));
            }
            catch (FollowerTrackingException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("tracking/events")]
        public async Task<IActionResult> TrackingEvent(
            [FromQuery] string asyncId,
            [FromBody] FollowerTrackingEventBody body,
            CancellationToken ct)
        {
            var follower = await Auth(asyncId, ct);
            if (follower == null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مصرح." });
            try
            {
                await _tracking.RecordEventAsync(follower, body.ShiftId, body.EventType ?? "", ct);
                return Ok(new { ok = true });
            }
            catch (FollowerTrackingException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("tracking/live")]
        public async Task<IActionResult> Live(
            [FromHeader(Name = "X-Follower-Tracking-Key")] string? key,
            CancellationToken ct)
        {
            if (!IsAccountantKey(key)) return Forbid();
            return Ok(await _tracking.ListLiveAsync(ct));
        }

        [HttpGet("tracking/followers")]
        public async Task<IActionResult> Followers(
            [FromHeader(Name = "X-Follower-Tracking-Key")] string? key,
            CancellationToken ct)
        {
            if (!IsAccountantKey(key)) return Forbid();
            return Ok(await _tracking.ListFollowersAsync(ct));
        }

        [HttpGet("tracking/followers/{followerId:int}/route")]
        public async Task<IActionResult> Route(
            int followerId,
            [FromQuery] string date,
            [FromHeader(Name = "X-Follower-Tracking-Key")] string? key,
            CancellationToken ct)
        {
            if (!IsAccountantKey(key)) return Forbid();
            if (!DateTime.TryParse(date, out var day))
            {
                return BadRequest(new { message = "تاريخ غير صالح." });
            }

            var (shift, points) = await _tracking.GetRouteAsync(followerId, day, ct);
            return Ok(new
            {
                shift,
                points,
                count = points.Count,
                first = points.FirstOrDefault()?.CapturedAt,
                last = points.LastOrDefault()?.CapturedAt,
            });
        }

        private async Task<FollowerUserIdentity?> Auth(string? asyncId, CancellationToken ct) =>
            await _identity.ResolveByAsyncIdAsync(asyncId, ct);

        private bool IsAccountantKey(string? key)
        {
            var expected = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["FollowerTracking:AccountantKey"];
            return !string.IsNullOrWhiteSpace(expected) && string.Equals(expected, key, StringComparison.Ordinal);
        }
    }

    public sealed class FollowerTrackingEventBody
    {
        public int? ShiftId { get; set; }
        public string? EventType { get; set; }
    }
}
