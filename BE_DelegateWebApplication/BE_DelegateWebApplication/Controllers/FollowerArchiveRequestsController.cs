using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerIdentity;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    /// <summary>
    /// Follower requests the paperwork (archive) of a finished customer; the main accountant
    /// sends it from the dashboard; the follower confirms receipt to close the request.
    /// </summary>
    [Route("api/Followers/ArchiveRequests")]
    [ApiController]
    public sealed class FollowerArchiveRequestsController : ControllerBase
    {
        private readonly IFollowerIdentityService _identity;
        private readonly IFollowerArchiveRequestsService _archive;

        public FollowerArchiveRequestsController(
            IFollowerIdentityService identity,
            IFollowerArchiveRequestsService archive)
        {
            _identity = identity;
            _archive = archive;
        }

        public sealed class CreateBody
        {
            public string? AsyncId { get; set; }
            public int CustomerId { get; set; }
            public string? Note { get; set; }
        }

        public sealed class AsyncIdBody
        {
            public string? AsyncId { get; set; }
        }

        /// <summary>Finished customers on the follower's lists (most recent first, optional name/phone search), with request status.</summary>
        [HttpGet("FinishedCustomers")]
        public Task<IActionResult> Finished([FromQuery] string? asyncId, [FromQuery] string? q, CancellationToken ct) =>
            Run(asyncId, async (userId, _) => Ok(await _archive.ListFinishedAsync(userId, q, ct)));

        /// <summary>All customers on the follower's lists (finished first), with remainder and request status.</summary>
        [HttpGet("Customers")]
        public Task<IActionResult> Customers([FromQuery] string? asyncId, [FromQuery] string? q, CancellationToken ct) =>
            Run(asyncId, async (userId, _) => Ok(await _archive.ListFinishedAsync(userId, q, ct, onlyFinished: false)));

        [HttpGet]
        public Task<IActionResult> Mine([FromQuery] string? asyncId, CancellationToken ct) =>
            Run(asyncId, async (userId, _) => Ok(await _archive.ListMineAsync(userId, ct)));

        [HttpPost]
        public Task<IActionResult> Create([FromBody] CreateBody? body, CancellationToken ct) =>
            Run(body?.AsyncId, async (userId, name) =>
                Ok(await _archive.CreateAsync(userId, name, body!.CustomerId, body.Note, ct)));

        [HttpPost("{id:int}/Received")]
        public Task<IActionResult> Received(int id, [FromBody] AsyncIdBody? body, CancellationToken ct) =>
            Run(body?.AsyncId, async (userId, _) => Ok(await _archive.MarkReceivedAsync(userId, id, ct)));

        private async Task<IActionResult> Run(string? asyncId, Func<int, string?, Task<IActionResult>> action)
        {
            var user = await _identity.ResolveByAsyncIdAsync(asyncId);
            if (user == null || user.UserId <= 0 || !user.IsActive)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            try
            {
                return await action(user.UserId, user.UserName);
            }
            catch (FollowerArchiveException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }
    }
}
