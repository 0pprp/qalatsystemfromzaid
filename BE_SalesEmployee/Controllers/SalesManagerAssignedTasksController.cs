using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Authorize(Policy = SalesPolicies.Director)]
[Route("api/sales-manager/assigned-tasks")]
[ApiController]
[RequestSizeLimit(32 * 1024 * 1024)]
public sealed class SalesManagerAssignedTasksController : ControllerBase
{
    private readonly DirectorAssignedTaskService _svc;
    private readonly DelegatedManagerAccountService _dmAccounts;
    private readonly IBranchManagerAppUserStore _bmUsers;

    public SalesManagerAssignedTasksController(
        DirectorAssignedTaskService svc,
        DelegatedManagerAccountService dmAccounts,
        IBranchManagerAppUserStore bmUsers)
    {
        _svc = svc;
        _dmAccounts = dmAccounts;
        _bmUsers = bmUsers;
    }

    public sealed class UpsertAssignedTaskRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateOnly DueDate { get; set; }
        /// <summary>Optional absolute due instant (UTC). Alternative to dueHour/dueMinute/duePeriod.</summary>
        public DateTime? DueAtUtc { get; set; }
        /// <summary>1–12 when pairing with DueDate + DuePeriod.</summary>
        public int? DueHour { get; set; }
        /// <summary>0–59 when pairing with DueDate + DuePeriod.</summary>
        public int? DueMinute { get; set; }
        /// <summary>"صباح" or "مساء".</summary>
        public string? DuePeriod { get; set; }
        public byte Priority { get; set; } = DirectorAssignedTaskPriority.Medium;
        public string? AssigneeKind { get; set; }
        public string? AssigneeKey { get; set; }
        public string? AssigneeDisplayName { get; set; }
        public string? AssigneeCityValue { get; set; }
    }

    [HttpGet("assignee-options")]
    public async Task<IActionResult> AssigneeOptions(CancellationToken ct)
    {
        var dm = _dmAccounts.ListAccounts()
            .Select(a => new
            {
                kind = DirectorAssignedTaskAssigneeKind.DelegatedManager,
                key = a.UserName,
                displayName = a.DisplayName,
                userName = a.UserName
            })
            .ToList();

        var bmRows = await _bmUsers.ListAsync(ct);
        var branchManagers = bmRows
            .Where(u => u.IsActive)
            .Select(u => new
            {
                kind = DirectorAssignedTaskAssigneeKind.BranchManager,
                key = u.Id.ToString("D"),
                displayName = u.DisplayName,
                userName = u.UserName,
                cityValues = u.Cities.Select(c => c.CityValue).ToList(),
                cities = u.Cities
                    .Select(c => new
                    {
                        cityValue = c.CityValue,
                        cityName = string.IsNullOrWhiteSpace(c.CityName) ? c.CityValue : c.CityName,
                    })
                    .ToList(),
            })
            .ToList();

        // Followers: director FE can also load per-city; gateway returns empty list here as
        // fan-out is optional. FE merges city followers into the same dropdown.
        return Ok(new
        {
            delegatedManagers = dm,
            branchManagers,
            fieldCommitteeFollowers = Array.Empty<object>(),
        });
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var rows = await _svc.ListForDirectorAsync(ct);
        return Ok(rows.Select(r => ToListDto(r.Task, r.IsOverdue)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var detail = await _svc.GetDetailAsync(id, ct);
        return detail is null
            ? NotFound(new { message = "المهمة غير موجودة." })
            : Ok(ToDetailDto(detail));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertAssignedTaskRequest? body, CancellationToken ct)
    {
        body ??= new UpsertAssignedTaskRequest();
        var actor = TokenService.FromPrincipal(User);
        var (ok, error, task) = await _svc.CreateAsync(
            body.Title,
            body.Description,
            body.DueDate,
            body.Priority,
            body.AssigneeKind,
            body.AssigneeKey,
            body.AssigneeDisplayName,
            body.AssigneeCityValue,
            DirectorUserId(actor),
            actor.UserName,
            body.DueAtUtc,
            body.DueHour,
            body.DueMinute,
            body.DuePeriod,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر إنشاء المهمة." });
        return Ok(ToTaskDto(task, isOverdue: false));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertAssignedTaskRequest? body, CancellationToken ct)
    {
        body ??= new UpsertAssignedTaskRequest();
        var actor = TokenService.FromPrincipal(User);
        var (ok, error, task) = await _svc.UpdateReturnedAsync(
            id,
            body.Title,
            body.Description,
            body.DueDate,
            body.Priority,
            body.AssigneeKind,
            body.AssigneeKey,
            body.AssigneeDisplayName,
            body.AssigneeCityValue,
            DirectorUserId(actor),
            actor.UserName,
            body.DueAtUtc,
            body.DueHour,
            body.DueMinute,
            body.DuePeriod,
            ct);
        if (!ok || task is null)
            return BadRequest(new { message = error ?? "تعذر تحديث المهمة." });
        return Ok(ToTaskDto(task, isOverdue: false));
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 32 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(Guid id, [FromForm] IFormFile? file, CancellationToken ct)
    {
        file ??= Request.Form.Files.FirstOrDefault(f => f.Length > 0);
        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "الملف مطلوب." });

        var actor = TokenService.FromPrincipal(User);
        var (ok, error, attachment) = await _svc.UploadAttachmentAsync(id, file, actor.UserName, ct);
        if (!ok || attachment is null)
            return BadRequest(new { message = error ?? "تعذر رفع المرفق." });
        return Ok(ToAttachmentDto(attachment));
    }

    [HttpGet("{id:guid}/attachments/{fileId:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id, Guid fileId, CancellationToken ct)
    {
        var (ok, error, fullPath, contentType, fileName) = await _svc.DownloadAttachmentAsync(id, fileId, ct);
        if (!ok || fullPath is null)
            return NotFound(new { message = error ?? "المرفق غير موجود." });
        return PhysicalFile(
            fullPath,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            fileName);
    }

    private static string DirectorUserId(GatewayUser actor) =>
        string.IsNullOrWhiteSpace(actor.UserID) || actor.UserID == "0"
            ? actor.UserName
            : actor.UserID;

    private static object ToListDto(DirectorAssignedTask t, bool isOverdue) => new
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
        isOverdue,
        createdAtUtc = t.CreatedAtUtc,
        updatedAtUtc = t.UpdatedAtUtc,
        transferredAtUtc = t.TransferredAtUtc,
        branchCompletedAtUtc = t.BranchCompletedAtUtc,
        delegatedCompletedAtUtc = t.DelegatedCompletedAtUtc,
        delegatedConfirmedAtUtc = t.DelegatedConfirmedAtUtc
    };

    private static object ToTaskDto(DirectorAssignedTask t, bool isOverdue) => ToListDto(t, isOverdue);

    private static object ToDetailDto(DirectorAssignedTaskDetail d) => new
    {
        task = ToListDto(d.Task, d.IsOverdue),
        events = d.Events.Select(ToEventDto),
        attachments = d.Attachments.Select(ToAttachmentDto)
    };

    private static object ToEventDto(DirectorAssignedTaskEvent e) => new
    {
        id = e.Id,
        taskId = e.TaskId,
        eventType = e.EventType,
        atUtc = e.AtUtc,
        actorKind = e.ActorKind,
        actorKey = e.ActorKey,
        actorDisplayName = e.ActorDisplayName,
        actorRoleLabel = e.ActorRoleLabel,
        targetKind = e.TargetKind,
        targetKey = e.TargetKey,
        targetDisplayName = e.TargetDisplayName,
        targetRoleLabel = e.TargetRoleLabel,
        note = e.Note
    };

    private static object ToAttachmentDto(DirectorAssignedTaskAttachment a) => new
    {
        id = a.Id,
        taskId = a.TaskId,
        fileName = a.FileName,
        contentType = a.ContentType,
        sizeBytes = a.SizeBytes,
        uploadedAtUtc = a.UploadedAtUtc,
        uploadedBy = a.UploadedBy
    };
}
