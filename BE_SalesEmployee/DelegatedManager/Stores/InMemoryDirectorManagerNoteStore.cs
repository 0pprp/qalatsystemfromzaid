using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryDirectorManagerNoteStore : IDirectorManagerNoteStore
{
    private readonly object _gate = new();
    private readonly List<DirectorManagerNote> _rows = new();

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<DirectorManagerNote> CreateAsync(DirectorManagerNote note, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = note.Clone();
            if (row.Id == Guid.Empty)
                row.Id = Guid.NewGuid();
            if (row.CreatedAtUtc == default)
                row.CreatedAtUtc = DateTime.UtcNow;
            row.Status = ComplaintStatuses.Unread;
            row.ReadAtUtc = null;
            _rows.Add(row);
            return Task.FromResult(row.Clone());
        }
    }

    public Task<PagedResult<DirectorManagerNote>> QueryAsync(DirectorNoteInboxQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        lock (_gate)
        {
            IEnumerable<DirectorManagerNote> rows = _rows;
            if (query.UnreadOnly)
                rows = rows.Where(r => string.Equals(r.Status, ComplaintStatuses.Unread, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(query.CityValue))
                rows = rows.Where(r => string.Equals(r.CityValue, query.CityValue.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.SourceApp))
                rows = rows.Where(r => string.Equals(r.SourceApp, query.SourceApp.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                rows = rows.Where(r =>
                    r.Subject.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Body.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.SenderDisplayName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = rows.Reverse().OrderByDescending(r => r.CreatedAtUtc).ToList();
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Clone()).ToList();
            return Task.FromResult(new PagedResult<DirectorManagerNote>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = ordered.Count
            });
        }
    }

    public Task<DirectorManagerNote?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_rows.FirstOrDefault(r => r.Id == id)?.Clone());
    }

    public Task<DirectorManagerNote?> MarkReadAsync(Guid id, DateTime readAtUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            if (row is null)
                return Task.FromResult<DirectorManagerNote?>(null);
            if (!string.Equals(row.Status, ComplaintStatuses.Read, StringComparison.Ordinal))
            {
                row.Status = ComplaintStatuses.Read;
                row.ReadAtUtc = readAtUtc;
            }
            return Task.FromResult<DirectorManagerNote?>(row.Clone());
        }
    }

    public Task<int> MarkAllReadAsync(DateTime readAtUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var affected = 0;
            foreach (var row in _rows.Where(r => string.Equals(r.Status, ComplaintStatuses.Unread, StringComparison.Ordinal)))
            {
                row.Status = ComplaintStatuses.Read;
                row.ReadAtUtc = readAtUtc;
                affected++;
            }
            return Task.FromResult(affected);
        }
    }

    public Task<int> CountUnreadAsync(CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_rows.Count(r => string.Equals(r.Status, ComplaintStatuses.Unread, StringComparison.Ordinal)));
    }
}
