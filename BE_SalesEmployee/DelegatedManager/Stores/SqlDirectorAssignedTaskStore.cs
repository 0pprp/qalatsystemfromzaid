using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class SqlDirectorAssignedTaskStore : IDirectorAssignedTaskStore
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private const string TaskColumns = """
        Id, Title, Description, DueDate, DueAtUtc, Priority, Status, CreatedByUserId, CreatedByUserName,
        AssigneeKind, AssigneeKey, AssigneeDisplayName, AssigneeCityValue, RejectNote,
        CurrentHolderKind, CurrentHolderKey, CreatedAtUtc, UpdatedAtUtc, TransferredAtUtc,
        BranchCompletedAtUtc, DelegatedCompletedAtUtc, DelegatedConfirmedAtUtc
        """;

    private const string EventColumns = """
        Id, TaskId, EventType, AtUtc, ActorKind, ActorKey, ActorDisplayName, ActorRoleLabel,
        TargetKind, TargetKey, TargetDisplayName, TargetRoleLabel, Note
        """;

    private const string AttachmentColumns = """
        Id, TaskId, FileName, StoredFileName, ContentType, SizeBytes, UploadedAtUtc, UploadedBy
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlDirectorAssignedTaskStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady) return;

        lock (SchemaGate)
        {
            if (_schemaReady) return;
        }

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.DirectorAssignedTasks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DirectorAssignedTasks (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DirectorAssignedTasks PRIMARY KEY,
        Title NVARCHAR(512) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL CONSTRAINT DF_DAT_Description DEFAULT (N''),
        DueDate DATE NOT NULL,
        Priority TINYINT NOT NULL,
        Status NVARCHAR(64) NOT NULL,
        CreatedByUserId NVARCHAR(64) NOT NULL,
        CreatedByUserName NVARCHAR(128) NOT NULL,
        AssigneeKind NVARCHAR(32) NOT NULL,
        AssigneeKey NVARCHAR(128) NOT NULL,
        AssigneeDisplayName NVARCHAR(256) NOT NULL,
        AssigneeCityValue NVARCHAR(64) NULL,
        RejectNote NVARCHAR(MAX) NULL,
        CurrentHolderKind NVARCHAR(32) NOT NULL,
        CurrentHolderKey NVARCHAR(128) NOT NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DAT_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DAT_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        BranchCompletedAtUtc DATETIME2(3) NULL,
        DelegatedCompletedAtUtc DATETIME2(3) NULL,
        DelegatedConfirmedAtUtc DATETIME2(3) NULL,
        DueAtUtc DATETIME2(3) NULL,
        TransferredAtUtc DATETIME2(3) NULL
    );
    CREATE INDEX IX_DirectorAssignedTasks_Holder
        ON dbo.DirectorAssignedTasks (CurrentHolderKind, CurrentHolderKey, Priority, DueDate);
    CREATE INDEX IX_DirectorAssignedTasks_PriorityDue
        ON dbo.DirectorAssignedTasks (Priority, DueDate, Id);
END

IF OBJECT_ID(N'dbo.DirectorAssignedTaskEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DirectorAssignedTaskEvents (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DirectorAssignedTaskEvents PRIMARY KEY,
        TaskId UNIQUEIDENTIFIER NOT NULL,
        EventType NVARCHAR(32) NOT NULL,
        AtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DATE_AtUtc DEFAULT (SYSUTCDATETIME()),
        ActorKind NVARCHAR(32) NULL,
        ActorKey NVARCHAR(128) NULL,
        ActorDisplayName NVARCHAR(256) NULL,
        ActorRoleLabel NVARCHAR(128) NULL,
        TargetKind NVARCHAR(32) NULL,
        TargetKey NVARCHAR(128) NULL,
        TargetDisplayName NVARCHAR(256) NULL,
        TargetRoleLabel NVARCHAR(128) NULL,
        Note NVARCHAR(MAX) NULL,
        CONSTRAINT FK_DirectorAssignedTaskEvents_Task
            FOREIGN KEY (TaskId) REFERENCES dbo.DirectorAssignedTasks(Id)
    );
    CREATE INDEX IX_DirectorAssignedTaskEvents_Task
        ON dbo.DirectorAssignedTaskEvents (TaskId, AtUtc, Id);
END

IF OBJECT_ID(N'dbo.DirectorAssignedTaskAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DirectorAssignedTaskAttachments (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DirectorAssignedTaskAttachments PRIMARY KEY,
        TaskId UNIQUEIDENTIFIER NOT NULL,
        FileName NVARCHAR(260) NOT NULL,
        StoredFileName NVARCHAR(260) NOT NULL,
        ContentType NVARCHAR(128) NOT NULL,
        SizeBytes BIGINT NOT NULL,
        UploadedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_DATA_UploadedAtUtc DEFAULT (SYSUTCDATETIME()),
        UploadedBy NVARCHAR(128) NOT NULL,
        CONSTRAINT FK_DirectorAssignedTaskAttachments_Task
            FOREIGN KEY (TaskId) REFERENCES dbo.DirectorAssignedTasks(Id)
    );
    CREATE INDEX IX_DirectorAssignedTaskAttachments_Task
        ON dbo.DirectorAssignedTaskAttachments (TaskId, UploadedAtUtc, Id);
END

IF OBJECT_ID(N'dbo.DirectorAssignedTasks', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DirectorAssignedTasks', N'Description') IS NULL
BEGIN
    ALTER TABLE dbo.DirectorAssignedTasks
        ADD Description NVARCHAR(MAX) NOT NULL
            CONSTRAINT DF_DAT_Description_Add DEFAULT (N'');
END

IF OBJECT_ID(N'dbo.DirectorAssignedTasks', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DirectorAssignedTasks', N'DueAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.DirectorAssignedTasks ADD DueAtUtc DATETIME2(3) NULL;
END

IF OBJECT_ID(N'dbo.DirectorAssignedTasks', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.DirectorAssignedTasks', N'TransferredAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.DirectorAssignedTasks ADD TransferredAtUtc DATETIME2(3) NULL;
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<DirectorAssignedTask> CreateAsync(DirectorAssignedTask task, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var row = NormalizeTask(task);
        if (row.Id == Guid.Empty)
            row.Id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        if (row.CreatedAtUtc == default) row.CreatedAtUtc = now;
        if (row.UpdatedAtUtc == default) row.UpdatedAtUtc = now;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO dbo.DirectorAssignedTasks ({TaskColumns})
            VALUES (@Id, @Title, @Description, @DueDate, @DueAtUtc, @Priority, @Status, @CreatedByUserId, @CreatedByUserName,
                    @AssigneeKind, @AssigneeKey, @AssigneeDisplayName, @AssigneeCityValue, @RejectNote,
                    @CurrentHolderKind, @CurrentHolderKey, @CreatedAtUtc, @UpdatedAtUtc, @TransferredAtUtc,
                    @BranchCompletedAtUtc, @DelegatedCompletedAtUtc, @DelegatedConfirmedAtUtc);
            """, row, cancellationToken: ct));
        return row;
    }

    public async Task<DirectorAssignedTask?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<DirectorAssignedTask>(new CommandDefinition($"""
            SELECT {TaskColumns}
            FROM dbo.DirectorAssignedTasks
            WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task UpdateAsync(DirectorAssignedTask task, CancellationToken ct = default)
    {
        if (task.Id == Guid.Empty)
            throw new ArgumentException("Id is required", nameof(task));

        await EnsureSchemaAsync(ct);
        var row = NormalizeTask(task);
        row.UpdatedAtUtc = DateTime.UtcNow;

        using var connection = await _factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.DirectorAssignedTasks SET
                Title = @Title,
                Description = @Description,
                DueDate = @DueDate,
                DueAtUtc = @DueAtUtc,
                Priority = @Priority,
                Status = @Status,
                CreatedByUserId = @CreatedByUserId,
                CreatedByUserName = @CreatedByUserName,
                AssigneeKind = @AssigneeKind,
                AssigneeKey = @AssigneeKey,
                AssigneeDisplayName = @AssigneeDisplayName,
                AssigneeCityValue = @AssigneeCityValue,
                RejectNote = @RejectNote,
                CurrentHolderKind = @CurrentHolderKind,
                CurrentHolderKey = @CurrentHolderKey,
                UpdatedAtUtc = @UpdatedAtUtc,
                TransferredAtUtc = @TransferredAtUtc,
                BranchCompletedAtUtc = @BranchCompletedAtUtc,
                DelegatedCompletedAtUtc = @DelegatedCompletedAtUtc,
                DelegatedConfirmedAtUtc = @DelegatedConfirmedAtUtc
            WHERE Id = @Id;
            """, row, cancellationToken: ct));

        if (affected == 0)
            throw new KeyNotFoundException($"Director assigned task {task.Id} not found");
    }

    public async Task<IReadOnlyList<DirectorAssignedTask>> ListAllSortedAsync(CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = (await connection.QueryAsync<DirectorAssignedTask>(new CommandDefinition($"""
            SELECT {TaskColumns}
            FROM dbo.DirectorAssignedTasks;
            """, cancellationToken: ct))).ToList();
        return SortTasks(rows);
    }

    public async Task<DirectorAssignedTaskEvent> AddEventAsync(DirectorAssignedTaskEvent evt, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var row = NormalizeEvent(evt);
        if (row.Id == Guid.Empty)
            row.Id = Guid.NewGuid();
        if (row.AtUtc == default)
            row.AtUtc = DateTime.UtcNow;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO dbo.DirectorAssignedTaskEvents ({EventColumns})
            VALUES (@Id, @TaskId, @EventType, @AtUtc, @ActorKind, @ActorKey, @ActorDisplayName, @ActorRoleLabel,
                    @TargetKind, @TargetKey, @TargetDisplayName, @TargetRoleLabel, @Note);
            """, row, cancellationToken: ct));
        return row;
    }

    public async Task<IReadOnlyList<DirectorAssignedTaskEvent>> ListEventsAsync(
        Guid taskId,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<DirectorAssignedTaskEvent>(new CommandDefinition($"""
            SELECT {EventColumns}
            FROM dbo.DirectorAssignedTaskEvents
            WHERE TaskId = @taskId
            ORDER BY AtUtc ASC, Id ASC;
            """, new { taskId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<DirectorAssignedTaskAttachment> AddAttachmentAsync(
        DirectorAssignedTaskAttachment attachment,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var row = NormalizeAttachment(attachment);
        if (row.Id == Guid.Empty)
            row.Id = Guid.NewGuid();
        if (row.UploadedAtUtc == default)
            row.UploadedAtUtc = DateTime.UtcNow;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO dbo.DirectorAssignedTaskAttachments ({AttachmentColumns})
            VALUES (@Id, @TaskId, @FileName, @StoredFileName, @ContentType, @SizeBytes, @UploadedAtUtc, @UploadedBy);
            """, row, cancellationToken: ct));
        return row;
    }

    public async Task<IReadOnlyList<DirectorAssignedTaskAttachment>> ListAttachmentsAsync(
        Guid taskId,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<DirectorAssignedTaskAttachment>(new CommandDefinition($"""
            SELECT {AttachmentColumns}
            FROM dbo.DirectorAssignedTaskAttachments
            WHERE TaskId = @taskId
            ORDER BY UploadedAtUtc ASC, Id ASC;
            """, new { taskId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<DirectorAssignedTaskAttachment?> GetAttachmentAsync(
        Guid attachmentId,
        CancellationToken ct = default)
    {
        if (attachmentId == Guid.Empty) return null;
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<DirectorAssignedTaskAttachment>(new CommandDefinition($"""
            SELECT {AttachmentColumns}
            FROM dbo.DirectorAssignedTaskAttachments
            WHERE Id = @attachmentId;
            """, new { attachmentId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DirectorAssignedTask>> ListInboxByHolderAsync(
        string holderKind,
        string holderKey,
        CancellationToken ct = default)
    {
        var kind = (holderKind ?? "").Trim();
        var key = (holderKey ?? "").Trim();
        if (kind.Length == 0 || key.Length == 0)
            return Array.Empty<DirectorAssignedTask>();

        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        // Exact key, or (for follower userId-only) keys ending with "|{userId}".
        var matchSuffix = key.IndexOf('|') < 0;
        var rows = (await connection.QueryAsync<DirectorAssignedTask>(new CommandDefinition($"""
            SELECT {TaskColumns}
            FROM dbo.DirectorAssignedTasks
            WHERE CurrentHolderKind = @kind
              AND (
                    CurrentHolderKey = @key
                 OR (@matchSuffix = 1 AND CurrentHolderKey LIKE '%|' + @key)
              );
            """, new { kind, key, matchSuffix }, cancellationToken: ct))).ToList();
        if (matchSuffix)
        {
            var suffix = "|" + key;
            rows = rows
                .Where(t => string.Equals(t.CurrentHolderKey, key, StringComparison.Ordinal)
                    || (t.CurrentHolderKey ?? "").EndsWith(suffix, StringComparison.Ordinal))
                .ToList();
        }

        return SortTasks(rows);
    }

    public async Task<IReadOnlyList<DirectorAssignedTask>> ListInboxForDelegatedAssigneeAsync(
        string assigneeKey,
        CancellationToken ct = default)
    {
        var key = (assigneeKey ?? "").Trim();
        if (key.Length == 0)
            return Array.Empty<DirectorAssignedTask>();

        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var kind = DirectorAssignedTaskAssigneeKind.DelegatedManager;
        var rows = (await connection.QueryAsync<DirectorAssignedTask>(new CommandDefinition($"""
            SELECT {TaskColumns}
            FROM dbo.DirectorAssignedTasks
            WHERE AssigneeKind = @kind AND AssigneeKey = @key;
            """, new { kind, key }, cancellationToken: ct))).ToList();
        return SortTasks(rows);
    }

    private static IReadOnlyList<DirectorAssignedTask> SortTasks(IEnumerable<DirectorAssignedTask> tasks) =>
        tasks
            .OrderBy(
                t => t,
                Comparer<DirectorAssignedTask>.Create((a, b) =>
                    DirectorAssignedTaskRules.CompareSortOrder(a.Priority, a.DueDate, b.Priority, b.DueDate)))
            .ToList();

    private static DirectorAssignedTask NormalizeTask(DirectorAssignedTask task)
    {
        var row = task.Clone();
        row.Title = (row.Title ?? "").Trim();
        row.Description = row.Description ?? "";
        row.Status = (row.Status ?? "").Trim();
        row.CreatedByUserId = (row.CreatedByUserId ?? "").Trim();
        row.CreatedByUserName = (row.CreatedByUserName ?? "").Trim();
        row.AssigneeKind = (row.AssigneeKind ?? "").Trim();
        row.AssigneeKey = (row.AssigneeKey ?? "").Trim();
        row.AssigneeDisplayName = (row.AssigneeDisplayName ?? "").Trim();
        row.AssigneeCityValue = string.IsNullOrWhiteSpace(row.AssigneeCityValue)
            ? null
            : row.AssigneeCityValue.Trim();
        row.RejectNote = string.IsNullOrWhiteSpace(row.RejectNote) ? null : row.RejectNote.Trim();
        row.CurrentHolderKind = (row.CurrentHolderKind ?? "").Trim();
        row.CurrentHolderKey = (row.CurrentHolderKey ?? "").Trim();
        return row;
    }

    private static DirectorAssignedTaskEvent NormalizeEvent(DirectorAssignedTaskEvent evt)
    {
        var row = evt.Clone();
        row.EventType = (row.EventType ?? "").Trim();
        row.ActorKind = TrimOrNull(row.ActorKind);
        row.ActorKey = TrimOrNull(row.ActorKey);
        row.ActorDisplayName = TrimOrNull(row.ActorDisplayName);
        row.ActorRoleLabel = TrimOrNull(row.ActorRoleLabel);
        row.TargetKind = TrimOrNull(row.TargetKind);
        row.TargetKey = TrimOrNull(row.TargetKey);
        row.TargetDisplayName = TrimOrNull(row.TargetDisplayName);
        row.TargetRoleLabel = TrimOrNull(row.TargetRoleLabel);
        row.Note = TrimOrNull(row.Note);
        return row;
    }

    private static DirectorAssignedTaskAttachment NormalizeAttachment(DirectorAssignedTaskAttachment attachment)
    {
        var row = attachment.Clone();
        row.FileName = (row.FileName ?? "").Trim();
        row.StoredFileName = (row.StoredFileName ?? "").Trim();
        row.ContentType = (row.ContentType ?? "").Trim();
        row.UploadedBy = (row.UploadedBy ?? "").Trim();
        return row;
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
