using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services.FollowerTracking;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [ApiController]
    [Route("api/Followers")]
    public sealed class FollowerTrackingController : ControllerBase
    {
        private readonly IFollowerTrackingService _tracking;
        private readonly IDelegateRepository _delegates;

        public FollowerTrackingController(IFollowerTrackingService tracking, IDelegateRepository delegates)
        {
            _tracking = tracking;
            _delegates = delegates;
        }

        [HttpPost("shifts/start")]
        public async Task<IActionResult> StartShift([FromQuery] string asyncId, CancellationToken ct)
        {
            var follower = await Auth(asyncId);
            if (follower == null) return Unauthorized();
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
            var follower = await Auth(asyncId);
            if (follower == null) return Unauthorized();
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
            var follower = await Auth(asyncId);
            if (follower == null) return Unauthorized();
            var current = await _tracking.CurrentAsync(follower.DelegateId, ct);
            return Ok(current);
        }

        [HttpPost("location/batch")]
        public async Task<IActionResult> LocationBatch([FromQuery] string asyncId, [FromBody] FollowerLocationBatchRequestDto body, CancellationToken ct)
        {
            var follower = await Auth(asyncId);
            if (follower == null) return Unauthorized();
            try
            {
                return Ok(await _tracking.IngestBatchAsync(follower, body, ct));
            }
            catch (FollowerTrackingException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("location/live")]
        public async Task<IActionResult> LocationLive([FromQuery] string asyncId, [FromBody] FollowerLiveLocationRequestDto body, CancellationToken ct)
        {
            var follower = await Auth(asyncId);
            if (follower == null) return Unauthorized();
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
            var follower = await Auth(asyncId);
            if (follower == null) return Unauthorized();
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

        /// <summary>Accountant/admin read — requires shared secret header X-Follower-Tracking-Key matching config.</summary>
        [HttpGet("tracking/live")]
        public async Task<IActionResult> LiveLocations([FromHeader(Name = "X-Follower-Tracking-Key")] string? key, CancellationToken ct)
        {
            if (!IsAccountantKey(key)) return Forbid();
            return Ok(await _tracking.ListLiveAsync(ct));
        }

        [HttpGet("tracking/followers")]
        public async Task<IActionResult> Followers([FromHeader(Name = "X-Follower-Tracking-Key")] string? key, CancellationToken ct)
        {
            if (!IsAccountantKey(key)) return Forbid();
            var rows = await _tracking.ListFollowersAsync(ct);
            return Ok(rows.Select(r => new { followerId = r.FollowerId, followerName = r.FollowerName }));
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

        private async Task<DelegateGetDTO?> Auth(string? asyncId)
        {
            if (string.IsNullOrWhiteSpace(asyncId)) return null;
            var login = await _delegates.GetDelegateLogin(asyncId);
            return login is { DelegateId: > 0 } ? login : null;
        }

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
