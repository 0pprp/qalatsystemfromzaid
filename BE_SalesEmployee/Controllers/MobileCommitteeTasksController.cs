using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[AllowAnonymous]
[Route("api/mobile-committee-tasks")]
[ApiController]
public class MobileCommitteeTasksController : ControllerBase
{
    private readonly AppCommitteeTaskService _svc;

    public MobileCommitteeTasksController(AppCommitteeTaskService svc)
    {
        _svc = svc;
    }

    /// <summary>Public list for مسؤول اللجنة app (appKey path kept as follower for routing stability).</summary>
    [HttpGet("follower")]
    public async Task<IActionResult> ListForFollowerApp(CancellationToken ct)
    {
        var rows = await _svc.ListPublicAsync(ct);
        return Ok(rows.Select(r => new
        {
            id = r.Id,
            body = r.Body,
            sortOrder = r.SortOrder
        }));
    }
}
