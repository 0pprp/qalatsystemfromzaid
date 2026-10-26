using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public interface IBmBranchVisitStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<BmBranchVisit?> GetAsync(Guid id, CancellationToken ct = default);
    Task<BmBranchVisit?> GetOpenForUserAsync(Guid openedByUserId, CancellationToken ct = default);
    Task<IReadOnlyList<BmBranchVisit>> ListAsync(
        string? cityValue,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct = default);
    Task<BmBranchVisit> InsertAsync(BmBranchVisit visit, CancellationToken ct = default);
    Task<BmBranchVisit?> UpdateAsync(BmBranchVisit visit, CancellationToken ct = default);
    Task UpsertDelegateRatingAsync(BmBranchVisitDelegateRating rating, CancellationToken ct = default);
    Task AddAttachmentAsync(BmBranchVisitAttachment attachment, CancellationToken ct = default);
    Task<BmBranchVisitAttachment?> GetAttachmentAsync(Guid visitId, Guid attachmentId, CancellationToken ct = default);
}
