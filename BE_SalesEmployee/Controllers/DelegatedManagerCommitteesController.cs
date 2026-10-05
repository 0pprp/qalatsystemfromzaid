using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Route("api/delegated-manager/committees")]
[ApiController]
[Authorize(Policy = SalesPolicies.DelegatedManager)]
public sealed class DelegatedManagerCommitteesController : ControllerBase
{
    private readonly CommitteeService _committees;
    private readonly MainFollowerAccountService _mainFollowers;
    private readonly CommitteeHistoryService _history;
    private readonly MainFollowerShiftService _shifts;

    public DelegatedManagerCommitteesController(
        CommitteeService committees,
        MainFollowerAccountService mainFollowers,
        CommitteeHistoryService history,
        MainFollowerShiftService shifts)
    {
        _committees = committees;
        _mainFollowers = mainFollowers;
        _history = history;
        _shifts = shifts;
    }

    public sealed class CreateCommitteeBody
    {
        public string? Name { get; set; }
        public List<string>? MemberUserNames { get; set; }
    }

    public sealed class CreateMissionBody
    {
        public int CommitteeId { get; set; }
        public List<string>? CityValues { get; set; }
        public string? Note { get; set; }
    }

    [HttpGet("main-followers")]
    public IActionResult ListMainFollowers() =>
        Ok(_mainFollowers.ListAccounts().Select(a => new
        {
            userName = a.UserName,
            displayName = a.DisplayName
        }));

    /// <summary>Live locations for all configured main followers (committee map).</summary>
    [HttpGet("main-followers/live")]
    public async Task<IActionResult> MainFollowersLive(CancellationToken ct)
    {
        var accounts = _mainFollowers.ListAccounts()
            .Select(a => (a.UserName, a.DisplayName));
        var rows = await _shifts.LiveSnapshotAsync(accounts, ct);
        return Ok(rows);
    }

    [HttpGet("main-followers/{userName}/trail")]
    public async Task<IActionResult> MainFollowerTrail(
        string userName,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken ct)
    {
        var rows = await _shifts.TrailAsync(userName, fromUtc, toUtc, ct);
        return Ok(rows.Select(p => new
        {
            id = p.Id,
            shiftId = p.ShiftId,
            latitude = p.Latitude,
            longitude = p.Longitude,
            accuracyMeters = p.AccuracyMeters,
            recordedAtUtc = p.RecordedAtUtc
        }));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCommittee([FromBody] CreateCommitteeBody body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var row = await _committees.CreateCommitteeAsync(
                actor.UserName,
                actor.UserName,
                body?.Name ?? "",
                body?.MemberUserNames ?? [],
                ct);
            return Ok(ToCommitteeDto(row));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("missions")]
    public async Task<IActionResult> CreateMission([FromBody] CreateMissionBody body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var row = await _committees.CreateMissionAsync(
                actor.UserName,
                body?.CommitteeId ?? 0,
                body?.CityValues ?? [],
                body?.Note,
                ct);
            return Ok(ToMissionDto(row));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("missions")]
    public async Task<IActionResult> ListMissions(CancellationToken ct)
    {
        var rows = await _committees.ListAllMissionsAsync(ct);
        return Ok(rows.Select(ToMissionDto));
    }

    [HttpGet("missions/{missionId:int}/summary")]
    public async Task<IActionResult> MissionSummary(int missionId, CancellationToken ct)
    {
        try
        {
            var row = await _history.SummarizeMissionAsync(missionId, ct);
            return Ok(new
            {
                missionId = row.MissionId,
                committeeName = row.CommitteeName,
                saleReviewCount = row.SaleReviewCount,
                staffReviewCount = row.StaffReviewCount,
                avgSaleScore = row.AvgSaleScore,
                avgStaffScore = row.AvgStaffScore,
                cityValues = row.CityValues
            });
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static object ToCommitteeDto(CommitteeRecord row) => new
    {
        id = row.Id,
        name = row.Name,
        memberUserNames = row.MemberUserNames,
        createdByUserName = row.CreatedByUserName,
        createdAtUtc = row.CreatedAtUtc
    };

    private static object ToMissionDto(MissionRecord row) => new
    {
        id = row.Id,
        committeeId = row.CommitteeId,
        committeeName = row.CommitteeName,
        status = row.Status,
        note = row.Note,
        cityValues = row.CityValues,
        memberUserNames = row.MemberUserNames,
        createdByUserName = row.CreatedByUserName,
        createdAtUtc = row.CreatedAtUtc
    };
}
