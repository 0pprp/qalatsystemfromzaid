using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.BranchManager)]
[Route("api/branch-manager/assigned-tasks")]
[ApiController]
public sealed class BranchManagerAssignedTasksController : ControllerBase
{
    private readonly DirectorAssignedTaskService _svc;

    public BranchManagerAssignedTasksController(DirectorAssignedTaskService svc)
    {
        _svc = svc;
    }

    public sealed class NoteBody
    {
        public string? Note { get; set; }
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox([FromQuery] string? cityValue, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null)
            return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });

        var rows = await _svc.InboxForBmAsync(actor.Value.Key, ct, cityValue);
        var enriched = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            var atts = await _svc.ListAttachmentsAsync(row.Id, ct);
            enriched.Add(ToDto(row, atts));
        }

        return Ok(enriched);
    }

    [HttpGet("{id:guid}/attachments/{fileId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid fileId, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null)
            return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });

        var detail = await _svc.GetDetailAsync(id, ct);
        if (detail is null)
            return NotFound(new { message = "المهمة غير موجودة." });
        if (!string.Equals(detail.Task.CurrentHolderKey, actor.Value.Key, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(detail.Task.AssigneeKey, actor.Value.Key, StringComparison.OrdinalIgnoreCase))
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
        if (actor is null)
            return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });

        var (ok, error, task) = await _svc.AcceptAsync(
            id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            actor.Value.Key,
            actor.Value.DisplayName,
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
        if (actor is null)
            return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });

        var (ok, error, task) = await _svc.RejectAsync(
            id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            actor.Value.Key,
            actor.Value.DisplayName,
            body.Note,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر رفض المهمة." });
        return Ok(ToDto(task));
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        var actor = Actor();
        if (actor is null)
            return Unauthorized(new { message = "تعذر تحديد هوية مستخدم الفرع." });

        var (ok, error, task) = await _svc.CompleteAsync(
            id,
            DirectorAssignedTaskAssigneeKind.BranchManager,
            actor.Value.Key,
            actor.Value.DisplayName,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر إنجاز المهمة." });
        return Ok(ToDto(task));
    }

    private (string Key, string DisplayName)? Actor()
    {
        var gw = TokenService.FromPrincipal(User);
        var key = (gw.UserID ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key) || key == "0")
            return null;

        var display = string.IsNullOrWhiteSpace(gw.UserName) ? key : gw.UserName;
        return (key, display);
    }

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
        assigneeCityValue = t.AssigneeCityValue,
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
