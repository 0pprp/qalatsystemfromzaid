using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryDirectorAssignedTaskStore : IDirectorAssignedTaskStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, DirectorAssignedTask> _tasks = new();
    private readonly List<DirectorAssignedTaskEvent> _events = new();
    private readonly Dictionary<Guid, DirectorAssignedTaskAttachment> _attachments = new();

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<DirectorAssignedTask> CreateAsync(DirectorAssignedTask task, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = task.Clone();
            if (row.Id == Guid.Empty)
                row.Id = Guid.NewGuid();
            var now = DateTime.UtcNow;
            if (row.CreatedAtUtc == default) row.CreatedAtUtc = now;
            if (row.UpdatedAtUtc == default) row.UpdatedAtUtc = now;
            _tasks[row.Id] = row.Clone();
            return Task.FromResult(row.Clone());
        }
    }

    public Task<DirectorAssignedTask?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_tasks.TryGetValue(id, out var row) ? row.Clone() : null);
    }

    public Task UpdateAsync(DirectorAssignedTask task, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_tasks.ContainsKey(task.Id))
                throw new KeyNotFoundException($"Director assigned task {task.Id} not found");
            var row = task.Clone();
            row.UpdatedAtUtc = DateTime.UtcNow;
            _tasks[row.Id] = row;
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<DirectorAssignedTask>> ListAllSortedAsync(CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = SortTasks(_tasks.Values).Select(t => t.Clone()).ToList();
            return Task.FromResult<IReadOnlyList<DirectorAssignedTask>>(list);
        }
    }

    public Task<DirectorAssignedTaskEvent> AddEventAsync(DirectorAssignedTaskEvent evt, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = evt.Clone();
            if (row.Id == Guid.Empty)
                row.Id = Guid.NewGuid();
            if (row.AtUtc == default)
                row.AtUtc = DateTime.UtcNow;
            _events.Add(row.Clone());
            return Task.FromResult(row.Clone());
        }
    }

    public Task<IReadOnlyList<DirectorAssignedTaskEvent>> ListEventsAsync(Guid taskId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = _events
                .Where(e => e.TaskId == taskId)
                .OrderBy(e => e.AtUtc)
                .ThenBy(e => e.Id)
                .Select(e => e.Clone())
                .ToList();
            return Task.FromResult<IReadOnlyList<DirectorAssignedTaskEvent>>(list);
        }
    }

    public Task<DirectorAssignedTaskAttachment> AddAttachmentAsync(
        DirectorAssignedTaskAttachment attachment,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = attachment.Clone();
            if (row.Id == Guid.Empty)
                row.Id = Guid.NewGuid();
            if (row.UploadedAtUtc == default)
                row.UploadedAtUtc = DateTime.UtcNow;
            _attachments[row.Id] = row.Clone();
            return Task.FromResult(row.Clone());
        }
    }

    public Task<IReadOnlyList<DirectorAssignedTaskAttachment>> ListAttachmentsAsync(
        Guid taskId,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var list = _attachments.Values
                .Where(a => a.TaskId == taskId)
                .OrderBy(a => a.UploadedAtUtc)
                .ThenBy(a => a.Id)
                .Select(a => a.Clone())
                .ToList();
            return Task.FromResult<IReadOnlyList<DirectorAssignedTaskAttachment>>(list);
        }
    }

    public Task<DirectorAssignedTaskAttachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(
                _attachments.TryGetValue(attachmentId, out var row) ? row.Clone() : null);
    }

    public Task<IReadOnlyList<DirectorAssignedTask>> ListInboxByHolderAsync(
        string holderKind,
        string holderKey,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var kind = (holderKind ?? "").Trim();
            var key = (holderKey ?? "").Trim();
            var matchSuffix = key.IndexOf('|') < 0;
            var suffix = "|" + key;
            var list = SortTasks(_tasks.Values.Where(t =>
                    string.Equals(t.CurrentHolderKind, kind, StringComparison.Ordinal)
                    && (string.Equals(t.CurrentHolderKey, key, StringComparison.Ordinal)
                        || (matchSuffix
                            && (t.CurrentHolderKey ?? "").EndsWith(suffix, StringComparison.Ordinal)))))
                .Select(t => t.Clone())
                .ToList();
            return Task.FromResult<IReadOnlyList<DirectorAssignedTask>>(list);
        }
    }

    public Task<IReadOnlyList<DirectorAssignedTask>> ListInboxForDelegatedAssigneeAsync(
        string assigneeKey,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var key = (assigneeKey ?? "").Trim();
            var list = SortTasks(_tasks.Values.Where(t =>
                    string.Equals(
                        t.AssigneeKind,
                        DirectorAssignedTaskAssigneeKind.DelegatedManager,
                        StringComparison.Ordinal)
                    && string.Equals(t.AssigneeKey, key, StringComparison.Ordinal)))
                .Select(t => t.Clone())
                .ToList();
            return Task.FromResult<IReadOnlyList<DirectorAssignedTask>>(list);
        }
    }

    private static IEnumerable<DirectorAssignedTask> SortTasks(IEnumerable<DirectorAssignedTask> tasks) =>
        tasks.OrderBy(
            t => t,
            Comparer<DirectorAssignedTask>.Create((a, b) =>
                DirectorAssignedTaskRules.CompareSortOrder(a.Priority, a.DueDate, b.Priority, b.DueDate)));
}
