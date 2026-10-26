using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IDirectorAssignedTaskStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<DirectorAssignedTask> CreateAsync(DirectorAssignedTask task, CancellationToken ct = default);

    Task<DirectorAssignedTask?> GetAsync(Guid id, CancellationToken ct = default);

    Task UpdateAsync(DirectorAssignedTask task, CancellationToken ct = default);

    Task<IReadOnlyList<DirectorAssignedTask>> ListAllSortedAsync(CancellationToken ct = default);

    Task<DirectorAssignedTaskEvent> AddEventAsync(DirectorAssignedTaskEvent evt, CancellationToken ct = default);

    Task<IReadOnlyList<DirectorAssignedTaskEvent>> ListEventsAsync(Guid taskId, CancellationToken ct = default);

    Task<DirectorAssignedTaskAttachment> AddAttachmentAsync(
        DirectorAssignedTaskAttachment attachment,
        CancellationToken ct = default);

    Task<IReadOnlyList<DirectorAssignedTaskAttachment>> ListAttachmentsAsync(
        Guid taskId,
        CancellationToken ct = default);

    Task<DirectorAssignedTaskAttachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken ct = default);

    Task<IReadOnlyList<DirectorAssignedTask>> ListInboxByHolderAsync(
        string holderKind,
        string holderKey,
        CancellationToken ct = default);

    /// <summary>
    /// Tasks originally assigned to a Delegated Manager (including ones transferred to a BM).
    /// </summary>
    Task<IReadOnlyList<DirectorAssignedTask>> ListInboxForDelegatedAssigneeAsync(
        string assigneeKey,
        CancellationToken ct = default);
}
