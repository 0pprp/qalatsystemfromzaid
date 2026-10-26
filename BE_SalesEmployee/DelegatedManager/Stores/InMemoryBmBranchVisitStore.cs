using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryBmBranchVisitStore : IBmBranchVisitStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, BmBranchVisit> _visits = new();

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<BmBranchVisit?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_visits.TryGetValue(id, out var v) ? v.Clone() : null);
        }
    }

    public Task<BmBranchVisit?> GetOpenForUserAsync(Guid openedByUserId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var open = _visits.Values.FirstOrDefault(v =>
                v.OpenedByUserId == openedByUserId
                && string.Equals(v.Status, BmBranchVisitStatus.Open, StringComparison.Ordinal));
            return Task.FromResult(open?.Clone());
        }
    }

    public Task<IReadOnlyList<BmBranchVisit>> ListAsync(
        string? cityValue,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var q = _visits.Values.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(cityValue))
                q = q.Where(v => string.Equals(v.CityValue, cityValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (fromUtc is not null)
                q = q.Where(v => v.ArrivedAtUtc >= fromUtc.Value);
            if (toUtc is not null)
                q = q.Where(v => v.ArrivedAtUtc <= toUtc.Value);
            var list = q
                .OrderByDescending(v => v.ArrivedAtUtc)
                .Take(Math.Clamp(take <= 0 ? 100 : take, 1, 500))
                .Select(v => v.Clone())
                .ToList();
            return Task.FromResult<IReadOnlyList<BmBranchVisit>>(list);
        }
    }

    public Task<BmBranchVisit> InsertAsync(BmBranchVisit visit, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = visit.Clone();
            if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
            foreach (var m in row.Members) m.VisitId = row.Id;
            foreach (var r in row.DelegateRatings) r.VisitId = row.Id;
            foreach (var a in row.Attachments) a.VisitId = row.Id;
            _visits[row.Id] = row;
            return Task.FromResult(row.Clone());
        }
    }

    public Task<BmBranchVisit?> UpdateAsync(BmBranchVisit visit, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_visits.ContainsKey(visit.Id)) return Task.FromResult<BmBranchVisit?>(null);
            var row = visit.Clone();
            _visits[row.Id] = row;
            return Task.FromResult<BmBranchVisit?>(row.Clone());
        }
    }

    public Task UpsertDelegateRatingAsync(BmBranchVisitDelegateRating rating, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_visits.TryGetValue(rating.VisitId, out var visit))
                return Task.CompletedTask;
            visit.DelegateRatings.RemoveAll(r => r.DelegateId == rating.DelegateId);
            visit.DelegateRatings.Add(rating.Clone());
            visit.UpdatedAtUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    public Task AddAttachmentAsync(BmBranchVisitAttachment attachment, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_visits.TryGetValue(attachment.VisitId, out var visit))
                return Task.CompletedTask;
            var row = attachment.Clone();
            if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
            visit.Attachments.Add(row);
            visit.UpdatedAtUtc = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }

    public Task<BmBranchVisitAttachment?> GetAttachmentAsync(Guid visitId, Guid attachmentId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_visits.TryGetValue(visitId, out var visit))
                return Task.FromResult<BmBranchVisitAttachment?>(null);
            var att = visit.Attachments.FirstOrDefault(a => a.Id == attachmentId);
            return Task.FromResult(att?.Clone());
        }
    }
}
