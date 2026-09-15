using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Default store so demo and tests run without gateway SQL. Registered as a singleton.</summary>
public sealed class InMemoryCentralComplaintsStore : ICentralComplaintsStore
{
    private readonly object _gate = new();
    private readonly List<CentralComplaint> _rows = new();

    public Task<CentralComplaint> CreateAsync(CentralComplaint complaint, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = complaint.Clone();
            if (row.Id == Guid.Empty)
            {
                row.Id = Guid.NewGuid();
            }
            if (row.CreatedAtUtc == default)
            {
                row.CreatedAtUtc = DateTime.UtcNow;
            }
            row.Status = ComplaintStatuses.Unread;
            row.ReadAtUtc = null;
            _rows.Add(row);
            return Task.FromResult(row.Clone());
        }
    }

    public Task<PagedResult<CentralComplaint>> QueryAsync(ComplaintInboxQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        lock (_gate)
        {
            IEnumerable<CentralComplaint> rows = _rows;

            if (query.UnreadOnly)
            {
                rows = rows.Where(r => string.Equals(r.Status, ComplaintStatuses.Unread, StringComparison.Ordinal));
            }
            if (!string.IsNullOrWhiteSpace(query.CityValue))
            {
                rows = rows.Where(r => string.Equals(r.CityValue, query.CityValue.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(query.SourceApp))
            {
                rows = rows.Where(r => string.Equals(r.SourceApp, query.SourceApp.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var term = query.Search.Trim();
                rows = rows.Where(r =>
                    r.Subject.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.Body.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.SenderDisplayName.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            // Reverse first so the stable sort breaks equal timestamps by newest insert.
            var ordered = rows
                .Reverse()
                .OrderByDescending(r => r.CreatedAtUtc)
                .ToList();

            var items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => r.Clone())
                .ToList();

            return Task.FromResult(new PagedResult<CentralComplaint>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = ordered.Count
            });
        }
    }

    public Task<CentralComplaint?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_rows.FirstOrDefault(r => r.Id == id)?.Clone());
        }
    }

    public Task<CentralComplaint?> MarkReadAsync(Guid id, DateTime readAtUtc, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            if (row is null)
            {
                return Task.FromResult<CentralComplaint?>(null);
            }

            if (!string.Equals(row.Status, ComplaintStatuses.Read, StringComparison.Ordinal))
            {
                row.Status = ComplaintStatuses.Read;
                row.ReadAtUtc = readAtUtc;
            }

            return Task.FromResult<CentralComplaint?>(row.Clone());
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
        {
            return Task.FromResult(_rows.Count(r => string.Equals(r.Status, ComplaintStatuses.Unread, StringComparison.Ordinal)));
        }
    }
}
