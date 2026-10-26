using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.DelegatedManager)]
[Route("api/delegated-manager/assigned-tasks")]
[ApiController]
public sealed class DelegatedManagerAssignedTasksController : ControllerBase
{
    private readonly DirectorAssignedTaskService _svc;
    private readonly DelegatedManagerAccountService _dmAccounts;
    private readonly IBranchManagerAppUserStore _bmUsers;

    public DelegatedManagerAssignedTasksController(
        DirectorAssignedTaskService svc,
        DelegatedManagerAccountService dmAccounts,
        IBranchManagerAppUserStore bmUsers)
    {
        _svc = svc;
        _dmAccounts = dmAccounts;
        _bmUsers = bmUsers;
    }

    public sealed class NoteBody
    {
        public string? Note { get; set; }
    }

    public sealed class TransferBody
    {
        public Guid? TargetUserId { get; set; }
        public string? TargetKey { get; set; }
        public string? TargetDisplayName { get; set; }
        public string? Note { get; set; }
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox(CancellationToken ct)
    {
        var actor = Actor();
        var rows = await _svc.InboxForDmAsync(actor.Key, ct);
        var enriched = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            var atts = await _svc.ListAttachmentsAsync(row.Id, ct);
            enriched.Add(ToDto(row, atts));
        }

        return Ok(enriched);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var actor = Actor();
        var detail = await _svc.GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound(new { message = "المهمة غير موجودة." });
        if (!IsVisibleToHolder(detail.Task, actor.Key))
            return Forbid();
        return Ok(ToDto(detail.Task, detail.Attachments));
    }

    [HttpGet("{id:guid}/attachments/{fileId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid fileId, CancellationToken ct)
    {
        var actor = Actor();
        var detail = await _svc.GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound(new { message = "المهمة غير موجودة." });
        if (!IsVisibleToHolder(detail.Task, actor.Key))
            return Forbid();

        var (ok, error, fullPath, contentType, fileName) = await _svc.DownloadAttachmentAsync(id, fileId, ct);
        if (!ok || string.IsNullOrWhiteSpace(fullPath))
            return NotFound(new { message = error ?? "الملف غير موجود." });
        return PhysicalFile(
            fullPath,
            contentType ?? "application/octet-stream",
            fileName ?? Path.GetFileName(fullPath));
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var actor = Actor();
        var (ok, error, task) = await _svc.AcceptAsync(
            id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            actor.Key,
            actor.DisplayName,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر قبول المهمة." });
        return Ok(ToDto(task));
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] NoteBody? body, CancellationToken ct)
    {
        body ??= new NoteBody();
        var actor = Actor();
        var (ok, error, task) = await _svc.RejectAsync(
            id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            actor.Key,
            actor.DisplayName,
            body.Note,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر رفض المهمة." });
        return Ok(ToDto(task));
    }

    [HttpPost("{id:guid}/transfer")]
    public async Task<IActionResult> Transfer(Guid id, [FromBody] TransferBody? body, CancellationToken ct)
    {
        body ??= new TransferBody();
        var actor = Actor();

        string targetKey;
        string targetDisplay;
        if (body.TargetUserId is Guid bmId)
        {
            var bm = await _bmUsers.GetAsync(bmId, ct);
            if (bm is null || !bm.IsActive)
                return BadRequest(new { message = "مستخدم الفرع غير موجود." });
            targetKey = bm.Id.ToString("D");
            targetDisplay = string.IsNullOrWhiteSpace(body.TargetDisplayName)
                ? bm.DisplayName
                : body.TargetDisplayName.Trim();
        }
        else
        {
            targetKey = (body.TargetKey ?? "").Trim();
            targetDisplay = (body.TargetDisplayName ?? "").Trim();
            if (Guid.TryParse(targetKey, out var parsedId))
            {
                var bm = await _bmUsers.GetAsync(parsedId, ct);
                if (bm is not null)
                {
                    targetKey = bm.Id.ToString("D");
                    if (string.IsNullOrWhiteSpace(targetDisplay))
                        targetDisplay = bm.DisplayName;
                }
            }
        }

        var (ok, error, task) = await _svc.TransferAsync(
            id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            actor.Key,
            actor.DisplayName,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            targetKey,
            targetDisplay,
            body.Note,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر نقل المهمة." });
        return Ok(ToDto(task));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var actor = Actor();
        var existing = await _svc.GetDetailAsync(id, ct);
        if (existing is null)
            return NotFound(new { message = "المهمة غير موجودة." });

        // When awaiting DM confirm after BM completion, complete doubles as confirm.
        if (string.Equals(
                existing.Task.Status,
                DirectorAssignedTaskStatus.AwaitingDelegatedConfirm,
                StringComparison.Ordinal))
        {
            var confirmed = await _svc.ConfirmAsync(
                id,
                DirectorAssignedTaskAssigneeKind.DelegatedManager,
                actor.Key,
                actor.DisplayName,
                ct);
            if (!confirmed.Ok || confirmed.Task is null)
                return BadRequest(new { message = confirmed.Error ?? "تعذر تصديق المهمة." });
            return Ok(ToDto(confirmed.Task));
        }

        var (ok, error, task) = await _svc.CompleteAsync(
            id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            actor.Key,
            actor.DisplayName,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر إنجاز المهمة." });
        return Ok(ToDto(task));
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct)
    {
        var actor = Actor();
        var (ok, error, task) = await _svc.ConfirmAsync(
            id,
            DirectorAssignedTaskAssigneeKind.DelegatedManager,
            actor.Key,
            actor.DisplayName,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر تصديق المهمة." });
        return Ok(ToDto(task));
    }

    private (string Key, string DisplayName) Actor()
    {
        var gw = TokenService.FromPrincipal(User);
        var key = _dmAccounts.ResolveStableKey(gw.UserName);
        var display = string.IsNullOrWhiteSpace(gw.UserName) ? key : gw.UserName;
        foreach (var account in _dmAccounts.ListAccounts())
        {
            if (string.Equals(account.UserName, key, StringComparison.OrdinalIgnoreCase))
            {
                display = account.DisplayName;
                break;
            }
        }

        return (key, display);
    }

    private static bool IsVisibleToHolder(DirectorAssignedTask t, string holderKey) =>
        string.Equals(t.CurrentHolderKey, holderKey, StringComparison.OrdinalIgnoreCase)
        || string.Equals(t.AssigneeKey, holderKey, StringComparison.OrdinalIgnoreCase);

    private static object ToDto(
        DirectorAssignedTask t,
        IReadOnlyList<DirectorAssignedTaskAttachment>? attachments = null) => new
    {
        id = t.Id,
        title = t.Title,
        description = t.Description,
        dueDate = t.DueDate,
        dueAtUtc = t.DueAtUtc,
        priority = t.Priority,
        status = t.Status,
        assigneeKind = t.AssigneeKind,
        assigneeKey = t.AssigneeKey,
        assigneeDisplayName = t.AssigneeDisplayName,
        currentHolderKind = t.CurrentHolderKind,
        currentHolderKey = t.CurrentHolderKey,
        rejectNote = t.RejectNote,
        createdAtUtc = t.CreatedAtUtc,
        updatedAtUtc = t.UpdatedAtUtc,
        transferredAtUtc = t.TransferredAtUtc,
        branchCompletedAtUtc = t.BranchCompletedAtUtc,
        delegatedCompletedAtUtc = t.DelegatedCompletedAtUtc,
        delegatedConfirmedAtUtc = t.DelegatedConfirmedAtUtc,
        attachments = (attachments ?? Array.Empty<DirectorAssignedTaskAttachment>()).Select(a => new
        {
            id = a.Id,
            fileName = a.FileName,
            contentType = a.ContentType,
            sizeBytes = a.SizeBytes,
            uploadedAtUtc = a.UploadedAtUtc,
        }),
    };
}
