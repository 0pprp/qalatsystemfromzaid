using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    /// <summary>
    /// Admin APIs for follower list ACL (FollowerUserLists). ListId = Delegates.DelegateID.
    /// Does not gate login — login remains Users.UserType = متابع.
    /// </summary>
    [ApiController]
    [Route("api/follower-lists")]
    [Authorize]
    public sealed class FollowerUserListsController : ControllerBase
    {
        private readonly IFollowerUserListsRepository _repo;

        public FollowerUserListsController(IFollowerUserListsRepository repo)
        {
            _repo = repo;
        }

        [HttpGet("available")]
        public async Task<IActionResult> Available(CancellationToken ct)
        {
            return Ok(await _repo.GetAvailableListsAsync(ct));
        }

        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetAssigned(int userId, CancellationToken ct)
        {
            var ids = await _repo.GetAssignedListIdsAsync(userId, ct);
            return Ok(new { userId, listIds = ids });
        }

        [HttpPut("{userId:int}")]
        public async Task<IActionResult> PutAssigned(int userId, [FromBody] FollowerUserListsPutDTO body, CancellationToken ct)
        {
            if (userId <= 0) return BadRequest(new { message = "UserId غير صالح." });
            body ??= new FollowerUserListsPutDTO();
            await _repo.ReplaceAssignmentsAsync(userId, body.ListIds ?? [], ct);
            var ids = await _repo.GetAssignedListIdsAsync(userId, ct);
            return Ok(new { userId, listIds = ids });
        }
    }
}
