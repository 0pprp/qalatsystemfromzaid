using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>
/// Paper archive chain, called only by the sales gateway on behalf of central accounts:
/// مسؤول/مساعد مسؤول لجنة المراقبة (committee) and مسؤول الارشيف (archive manager).
/// The gateway checks the role and branch scope; the actor name arrives as the UserName claim.
/// </summary>
[Authorize(Policy = SalesPolicies.SalesManager)]
[Route("api/sales-manager/archive-flow")]
[ApiController]
public sealed class ArchiveFlowController : ControllerBase
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly ISalesArchiveService _archive;
    private readonly IFollowerArchiveRequestsService _requests;

    public ArchiveFlowController(
        SalesDevelopmentGuard guard,
        ISalesArchiveService archive,
        IFollowerArchiveRequestsService requests)
    {
        _guard = guard;
        _archive = archive;
        _requests = requests;
    }

    public sealed class AssignBody
    {
        public string? CommitteeUser { get; set; }
        public string? Note { get; set; }
    }

    // —— مسؤول الارشيف: sales bundles ——
    [HttpGet("board")]
    public Task<IActionResult> Board(CancellationToken ct) =>
        RunSales(async _ => Ok(await _archive.BoardAsync(ct)), ct);

    [HttpGet("bundles/{bundleId:int}")]
    public Task<IActionResult> Bundle(int bundleId, CancellationToken ct) =>
        RunSales(async _ =>
        {
            var bundle = await _archive.GetForAccountantAsync(bundleId, ct);
            return bundle == null ? NotFound(new { message = "الحزمة غير موجودة." }) : Ok(bundle);
        }, ct);

    [HttpGet("bundles/{bundleId:int}/photo")]
    public Task<IActionResult> Photo(int bundleId, CancellationToken ct) =>
        RunSales(async _ => PhotoResult(await _archive.ReadPhotoForAccountantAsync(bundleId, ct)), ct);

    [HttpGet("bundles/{bundleId:int}/photos/{photoId:int}")]
    public Task<IActionResult> PhotoById(int bundleId, int photoId, CancellationToken ct) =>
        RunSales(async _ => PhotoResult(await _archive.ReadPhotoForAccountantAsync(bundleId, photoId, ct)), ct);

    [HttpPost("archive/decide")]
    public Task<IActionResult> ArchiveDecide([FromBody] SalesArchiveIdsBody? body, CancellationToken ct) =>
        RunSales(async actor => Ok(new
        {
            updated = await _archive.ArchiveDecideAsync(actor, body?.ArchiveIds ?? [], body?.Accept == true, body?.Note, ct)
        }), ct);

    /// <summary>
    /// One branch's slice of the archive manager's global overview: counts plus what waits on him
    /// (bundles handed over by the committee, new follower requests).
    /// </summary>
    [HttpGet("overview")]
    public Task<IActionResult> Overview(CancellationToken ct) =>
        RunSales(async _ =>
        {
            var board = await _archive.BoardAsync(ct);
            var requests = await _requests.BoardAsync(ct);
            static object Brief(SalesArchiveBundleDto b) => new
            {
                b.BundleId, b.BundleNumber, b.EmployeeName, b.PeriodStart, b.PeriodEnd,
                b.RecipientName, b.ToArchiveCount, b.AtCommitteeCount, b.ItemCount
            };
            return Ok(new
            {
                counts = new
                {
                    atArchiveBundles = board.AtArchive.Count,
                    atArchiveItems = board.AtArchive.Sum(b => b.ToArchiveCount),
                    atCommitteeBundles = board.AtCommittee.Count,
                    atCommitteeItems = board.AtCommittee.Sum(b => b.AtCommitteeCount) + board.AtArchive.Sum(b => b.AtCommitteeCount),
                    readyBundles = board.Ready.Count,
                    needsFixBundles = board.NeedsFix.Count,
                    currentBundles = board.Current.Count,
                    approvedBundles = board.Approved.Count,
                    newRequests = requests.Requested.Count,
                    requestsAtCommittee = requests.AtCommittee.Count,
                    requestsWithFollower = requests.Sent.Count,
                    requestsReceived = requests.Received.Count
                },
                // One list per overview card (the dashboard shows them in a check-only popup).
                atArchive = board.AtArchive.Select(Brief),
                newRequests = requests.Requested,
                atCommittee = board.AtCommittee.Concat(board.AtArchive).Where(b => b.AtCommitteeCount > 0).Select(Brief),
                ready = board.Ready.Select(Brief),
                needsFix = board.NeedsFix.Select(b => new
                {
                    b.BundleId, b.BundleNumber, b.EmployeeName, b.PeriodStart, b.PeriodEnd,
                    b.RecipientName, b.ToArchiveCount, b.AtCommitteeCount, b.ItemCount, b.RefusedCount
                }),
                approved = board.Approved.OrderByDescending(b => b.PeriodStart).Take(100).Select(Brief),
                requestsAtCommittee = requests.AtCommittee,
                requestsWithFollower = requests.Sent
            });
        }, ct);

    // —— لجنة المراقبة: sales papers ——
    [HttpGet("committee/items")]
    public Task<IActionResult> CommitteeItems(CancellationToken ct) =>
        RunSales(async actor => Ok(new { items = await _archive.CommitteeItemsAsync(actor, ct) }), ct);

    [HttpPost("committee/receive")]
    public Task<IActionResult> CommitteeReceive([FromBody] SalesArchiveIdsBody? body, CancellationToken ct) =>
        RunSales(async actor => Ok(new
        {
            updated = await _archive.CommitteeReceiveAsync(actor, body?.ArchiveIds ?? [], body?.Accept == true, body?.Note, ct)
        }), ct);

    [HttpPost("committee/to-archive")]
    public Task<IActionResult> CommitteeToArchive([FromBody] SalesArchiveIdsBody? body, CancellationToken ct) =>
        RunSales(async actor => Ok(new
        {
            updated = await _archive.CommitteeSendToArchiveAsync(actor, body?.ArchiveIds ?? [], ct)
        }), ct);

    [HttpPost("committee/return")]
    public Task<IActionResult> CommitteeReturn([FromBody] SalesArchiveIdsBody? body, CancellationToken ct) =>
        RunSales(async actor => Ok(new
        {
            updated = await _archive.CommitteeReturnToAgentAsync(actor, body?.ArchiveIds ?? [], body?.Note, ct)
        }), ct);

    // —— Follower archive requests ——
    [HttpGet("requests")]
    public Task<IActionResult> Requests(CancellationToken ct) =>
        RunRequests(async _ => Ok(await _requests.BoardAsync(ct)));

    [HttpGet("requests/finished-customers")]
    public Task<IActionResult> Finished([FromQuery] int days = 30, CancellationToken ct = default) =>
        RunRequests(async _ => Ok(await _requests.FinishedByFollowerAsync(days, ct)));

    [HttpPost("requests/{id:int}/assign")]
    public Task<IActionResult> Assign(int id, [FromBody] AssignBody? body, CancellationToken ct) =>
        RunRequests(async actor => Ok(await _requests.AssignAsync(id, body?.CommitteeUser ?? "", actor, body?.Note, ct)));

    [HttpGet("committee/requests")]
    public Task<IActionResult> CommitteeRequests(CancellationToken ct) =>
        RunRequests(async actor => Ok(new { items = await _requests.CommitteeListAsync(actor, ct) }));

    [HttpPost("committee/requests/{id:int}/accept")]
    public Task<IActionResult> CommitteeAccept(int id, CancellationToken ct) =>
        RunRequests(async actor => Ok(await _requests.CommitteeAcceptAsync(actor, id, ct)));

    [HttpPost("committee/requests/{id:int}/give")]
    public Task<IActionResult> CommitteeGive(int id, CancellationToken ct) =>
        RunRequests(async actor => Ok(await _requests.CommitteeGiveAsync(actor, id, ct)));

    private IActionResult PhotoResult((string ContentType, byte[] Bytes)? photo) =>
        photo == null
            ? NotFound(new { message = "لا توجد صورة تسليم." })
            : File(photo.Value.Bytes, photo.Value.ContentType);

    private string? GatewayActor()
    {
        var source = User.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value;
        if (!string.Equals(source, SalesGatewayKeyHandler.AuthSourceGateway, StringComparison.Ordinal))
        {
            return null;
        }

        var name = User.FindFirst("UserName")?.Value?.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private async Task<IActionResult> RunSales(Func<string, Task<IActionResult>> action, CancellationToken ct)
    {
        var actor = GatewayActor();
        if (actor == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذا القسم متاح عبر لوحة الإدارة المركزية فقط." });
        }

        var check = await _guard.CanRunSalesModuleAsync(ct);
        if (!check.Ok)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = check.Reason });
        }

        try
        {
            return await action(actor);
        }
        catch (SalesArchiveException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    private async Task<IActionResult> RunRequests(Func<string, Task<IActionResult>> action)
    {
        var actor = GatewayActor();
        if (actor == null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذا القسم متاح عبر لوحة الإدارة المركزية فقط." });
        }

        try
        {
            return await action(actor);
        }
        catch (ArchiveRequestException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
