using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Inbox for مسؤول اللجنة (follower) assigned tasks — keyed by cityValue|userId.
/// Auth: client passes holderKey matching assignee key from director create.
/// </summary>
[AllowAnonymous]
[Route("api/mobile-follower-assigned-tasks")]
[ApiController]
public sealed class MobileFollowerAssignedTasksController : ControllerBase
{
    private readonly DirectorAssignedTaskService _svc;

    public MobileFollowerAssignedTasksController(DirectorAssignedTaskService svc) => _svc = svc;

    public sealed class NoteBody
    {
        public string? Note { get; set; }
    }

    public sealed class ActorBody
    {
        public string? HolderKey { get; set; }
        public string? DisplayName { get; set; }
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox([FromQuery] string? holderKey, CancellationToken ct)
    {
        var key = (holderKey ?? "").Trim();
        if (key.Length == 0)
            return BadRequest(new { message = "holderKey مطلوب." });

        var rows = await _svc.InboxForFollowerAsync(key, ct);
        var hasUnread = rows.Any(t =>
            string.Equals(t.Status, DirectorAssignedTaskStatus.Sent, StringComparison.Ordinal));
        return Ok(new
        {
            hasUnread,
            updatedAtUtc = rows.Count == 0
                ? (DateTime?)null
                : rows.Max(t => t.UpdatedAtUtc),
            tasks = rows.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                description = t.Description,
                status = t.Status,
                dueDate = t.DueDate,
                dueAtUtc = t.DueAtUtc,
                priority = t.Priority,
                assigneeKey = t.AssigneeKey,
                currentHolderKey = t.CurrentHolderKey,
                createdAtUtc = t.CreatedAtUtc,
                updatedAtUtc = t.UpdatedAtUtc,
            }),
        });
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, [FromBody] ActorBody? body, CancellationToken ct)
    {
        var actor = ParseActor(body);
        if (actor is null) return BadRequest(new { message = "holderKey مطلوب." });
        var (ok, error, task) = await _svc.AcceptAsync(
            id,
            DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower,
            actor.Value.Key,
            actor.Value.Display,
            ct);
        return ok ? Ok(new { id = task!.Id, status = task.Status }) : BadRequest(new { message = error });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] NoteBody? body, [FromQuery] string? holderKey, [FromQuery] string? displayName, CancellationToken ct)
    {
        var key = (holderKey ?? "").Trim();
        if (key.Length == 0) return BadRequest(new { message = "holderKey مطلوب." });
        var (ok, error, task) = await _svc.RejectAsync(
            id,
            DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower,
            key,
            string.IsNullOrWhiteSpace(displayName) ? key : displayName.Trim(),
            body?.Note,
            ct);
        return ok ? Ok(new { id = task!.Id, status = task.Status }) : BadRequest(new { message = error });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] ActorBody? body, CancellationToken ct)
    {
        var actor = ParseActor(body);
        if (actor is null) return BadRequest(new { message = "holderKey مطلوب." });
        var (ok, error, task) = await _svc.CompleteAsync(
            id,
            DirectorAssignedTaskAssigneeKind.FieldCommitteeFollower,
            actor.Value.Key,
            actor.Value.Display,
            ct);
        return ok ? Ok(new { id = task!.Id, status = task.Status }) : BadRequest(new { message = error });
    }

    private static (string Key, string Display)? ParseActor(ActorBody? body)
    {
        var key = (body?.HolderKey ?? "").Trim();
        if (key.Length == 0) return null;
        var display = string.IsNullOrWhiteSpace(body?.DisplayName) ? key : body!.DisplayName!.Trim();
        return (key, display);
    }
}
