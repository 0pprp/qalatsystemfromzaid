using System.Text.Json;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Paper archive chain for central accounts:
/// sales agent → مسؤول/مساعد مسؤول لجنة المراقبة → مسؤول الارشيف, and follower requests back down the chain.
/// Committee calls are limited to the committee's branches by <see cref="Middleware.CommitteeScopeMiddleware"/>.
/// </summary>
[Route("api/archive-flow")]
[ApiController]
[Authorize(Policy = SalesPolicies.ArchiveFlow)]
public sealed class ArchiveFlowController : ControllerBase
{
    private const string BranchBase = "sales-manager/archive-flow";
    private readonly ISalesManagerBranchAggregator _aggregator;
    private readonly ArchiveRecipientsService _recipients;

    public ArchiveFlowController(ISalesManagerBranchAggregator aggregator, ArchiveRecipientsService recipients)
    {
        _aggregator = aggregator;
        _recipients = recipients;
    }

    public sealed class AssignBody
    {
        public string? CommitteeUser { get; set; }
        public string? Note { get; set; }
    }

    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken ct)
    {
        var branches = await _aggregator.BranchesAsync(ct);
        var actor = TokenService.FromPrincipal(User);
        var visible = SalesRoles.IsCommittee(actor.UserType)
            ? branches.Where(b => actor.AllowsFilterCity(b.Value))
            : branches;
        return Ok(visible.Select(b => new { value = b.Value, name = b.Name }));
    }

    /// <summary>Committee head/assistant accounts that cover the branch.</summary>
    [HttpGet("recipients")]
    public async Task<IActionResult> Recipients([FromQuery] string? cityValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "اختر الفرع." });
        return Ok(await _recipients.ForCityAsync(cityValue, ct));
    }

    // —— مسؤول الارشيف ——
    [HttpGet("archive/board")]
    public Task<IActionResult> Board([FromQuery] string? cityValue, CancellationToken ct) =>
        ArchiveManagerGet(cityValue, "board", ct);

    [HttpGet("archive/bundles/{bundleId:int}")]
    public Task<IActionResult> Bundle(int bundleId, [FromQuery] string? cityValue, CancellationToken ct) =>
        ArchiveManagerGet(cityValue, $"bundles/{bundleId}", ct);

    [HttpGet("archive/bundles/{bundleId:int}/photo")]
    public Task<IActionResult> Photo(int bundleId, [FromQuery] string? cityValue, CancellationToken ct) =>
        ArchiveManagerFile(cityValue, $"bundles/{bundleId}/photo", ct);

    [HttpGet("archive/bundles/{bundleId:int}/photos/{photoId:int}")]
    public Task<IActionResult> PhotoById(int bundleId, int photoId, [FromQuery] string? cityValue, CancellationToken ct) =>
        ArchiveManagerFile(cityValue, $"bundles/{bundleId}/photos/{photoId}", ct);

    [HttpPost("archive/decide")]
    public Task<IActionResult> Decide([FromQuery] string? cityValue, [FromBody] JsonElement body, CancellationToken ct) =>
        Post(cityValue, "archive/decide", body.GetRawText(), archiveManager: true, ct);

    [HttpGet("archive/requests")]
    public Task<IActionResult> Requests([FromQuery] string? cityValue, CancellationToken ct) =>
        ArchiveManagerGet(cityValue, "requests", ct);

    [HttpGet("archive/finished-customers")]
    public Task<IActionResult> Finished([FromQuery] string? cityValue, [FromQuery] int days = 30, CancellationToken ct = default) =>
        ArchiveManagerGet(cityValue, $"requests/finished-customers?days={Math.Clamp(days, 1, 365)}", ct);

    [HttpPost("archive/requests/{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromQuery] string? cityValue, [FromBody] AssignBody? body, CancellationToken ct)
    {
        if (!IsArchiveManager())
            return Forbidden();
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "اختر الفرع." });
        var recipients = await _recipients.ForCityAsync(cityValue, ct);
        var chosen = recipients.FirstOrDefault(r => string.Equals(r.Name, body?.CommitteeUser?.Trim(), StringComparison.Ordinal));
        if (chosen == null)
            return BadRequest(new { message = "اختر مسؤول لجنة المراقبة أو مساعده المسؤول عن هذا الفرع." });
        var json = JsonSerializer.Serialize(new { committeeUser = chosen.Name, note = body?.Note });
        return await Post(cityValue, $"requests/{id}/assign", json, archiveManager: true, ct);
    }

    /// <summary>All branches at once: counts + what waits on the archive manager. A slow/failed branch is reported, not fatal.</summary>
    [HttpGet("archive/overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        if (!IsArchiveManager())
            return Forbidden();
        var actor = TokenService.FromPrincipal(User);
        var branches = await _aggregator.BranchesAsync(ct);
        var rows = await Task.WhenAll(branches.Select(async b =>
        {
            try
            {
                var (status, payload) = await _aggregator.GetOneAsync(actor, b.Value, $"{BranchBase}/overview", ct);
                return new { cityValue = b.Value, cityName = b.Name, ok = status == 200, status, data = status == 200 ? payload : null, error = status == 200 ? null : payload };
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                return new { cityValue = b.Value, cityName = b.Name, ok = false, status = 502, data = (object?)null, error = (object?)new { message = "تعذر الاتصال بالفرع" } };
            }
        }));
        return Ok(new { branches = rows });
    }

    // —— بروفايل الزبون (عرض فقط) ——
    [HttpGet("customers/{cityValue}/{customerId:int}/profile")]
    public Task<IActionResult> CustomerProfile(string cityValue, int customerId, CancellationToken ct) =>
        ArchiveManagerBranchGet(cityValue, $"sales-manager/customers/{customerId}/profile", ct);

    [HttpGet("customers/{cityValue}/{customerId:int}/notes")]
    public Task<IActionResult> CustomerNotes(string cityValue, int customerId, CancellationToken ct) =>
        ArchiveManagerBranchGet(cityValue, $"sales-manager/customers/{customerId}/notes", ct);

    [HttpGet("sales/{cityValue}/{saleId:int}/shop-image")]
    public Task<IActionResult> SaleShopImage(string cityValue, int saleId, CancellationToken ct) =>
        ArchiveManagerBranchFile(cityValue, $"sales-manager/sales/{saleId}/shop-image", ct);

    [HttpGet("customer-documents/{cityValue}/{documentId:int}/file")]
    public Task<IActionResult> CustomerDocumentFile(string cityValue, int documentId, CancellationToken ct) =>
        ArchiveManagerBranchFile(cityValue, $"sales-manager/customer-documents/{documentId}/file", ct);

    private async Task<IActionResult> ArchiveManagerBranchGet(string cityValue, string companyPath, CancellationToken ct)
    {
        if (!IsArchiveManager())
            return Forbidden();
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(actor, cityValue, companyPath, ct);
        return StatusCode(status, payload);
    }

    private async Task<IActionResult> ArchiveManagerBranchFile(string cityValue, string companyPath, CancellationToken ct)
    {
        if (!IsArchiveManager())
            return Forbidden();
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetFileAsync(actor, cityValue, companyPath, ct);
        if (payload is ValueTuple<byte[], string> file)
            return File(file.Item1, string.IsNullOrWhiteSpace(file.Item2) ? "image/jpeg" : file.Item2);
        return StatusCode(status, payload ?? new { message = "الملف غير موجود على السيرفر." });
    }

    // —— لجنة المراقبة ——
    [HttpGet("committee/items")]
    public Task<IActionResult> CommitteeItems([FromQuery] string? cityValue, CancellationToken ct) =>
        CommitteeGet(cityValue, "committee/items", ct);

    [HttpPost("committee/receive")]
    public Task<IActionResult> CommitteeReceive([FromQuery] string? cityValue, [FromBody] JsonElement body, CancellationToken ct) =>
        Post(cityValue, "committee/receive", body.GetRawText(), archiveManager: false, ct);

    [HttpPost("committee/to-archive")]
    public Task<IActionResult> CommitteeToArchive([FromQuery] string? cityValue, [FromBody] JsonElement body, CancellationToken ct) =>
        Post(cityValue, "committee/to-archive", body.GetRawText(), archiveManager: false, ct);

    [HttpPost("committee/return")]
    public Task<IActionResult> CommitteeReturn([FromQuery] string? cityValue, [FromBody] JsonElement body, CancellationToken ct) =>
        Post(cityValue, "committee/return", body.GetRawText(), archiveManager: false, ct);

    [HttpGet("committee/requests")]
    public Task<IActionResult> CommitteeRequests([FromQuery] string? cityValue, CancellationToken ct) =>
        CommitteeGet(cityValue, "committee/requests", ct);

    [HttpPost("committee/requests/{id:int}/accept")]
    public Task<IActionResult> CommitteeAccept(int id, [FromQuery] string? cityValue, CancellationToken ct) =>
        Post(cityValue, $"committee/requests/{id}/accept", "{}", archiveManager: false, ct);

    [HttpPost("committee/requests/{id:int}/give")]
    public Task<IActionResult> CommitteeGive(int id, [FromQuery] string? cityValue, CancellationToken ct) =>
        Post(cityValue, $"committee/requests/{id}/give", "{}", archiveManager: false, ct);

    private bool IsArchiveManager() => SalesRoles.IsArchiveManager(User.FindFirst("UserType")?.Value);

    private bool IsCommittee() => SalesRoles.IsCommittee(User.FindFirst("UserType")?.Value);

    private ObjectResult Forbidden() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "ليس لديك صلاحية لهذا الإجراء." });

    private Task<IActionResult> ArchiveManagerGet(string? cityValue, string path, CancellationToken ct) =>
        IsArchiveManager() ? Get(cityValue, path, ct) : Task.FromResult<IActionResult>(Forbidden());

    private Task<IActionResult> CommitteeGet(string? cityValue, string path, CancellationToken ct) =>
        IsCommittee() ? Get(cityValue, path, ct) : Task.FromResult<IActionResult>(Forbidden());

    private async Task<IActionResult> Get(string? cityValue, string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "اختر الفرع." });
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(actor, cityValue, $"{BranchBase}/{path}", ct);
        return StatusCode(status, payload);
    }

    private async Task<IActionResult> ArchiveManagerFile(string? cityValue, string path, CancellationToken ct)
    {
        if (!IsArchiveManager())
            return Forbidden();
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "اختر الفرع." });
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.SendContentAsync(
            actor, cityValue, $"{BranchBase}/{path}", HttpMethod.Get, null, ct);
        if (status == 200 && payload is ValueTuple<byte[], string> file)
            return File(file.Item1, file.Item2);
        return StatusCode(status == 200 ? 404 : status, status == 200 ? new { message = "لا توجد صورة تسليم." } : payload);
    }

    private async Task<IActionResult> Post(string? cityValue, string path, string json, bool archiveManager, CancellationToken ct)
    {
        if (archiveManager ? !IsArchiveManager() : !IsCommittee())
            return Forbidden();
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "اختر الفرع." });
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.PostAsync(actor, cityValue, $"{BranchBase}/{path}", json, ct);
        return StatusCode(status, payload);
    }
}
