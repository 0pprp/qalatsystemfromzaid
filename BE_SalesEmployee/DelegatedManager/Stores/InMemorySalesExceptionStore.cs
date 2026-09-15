using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>Default store so demo and tests run without gateway SQL. Registered as a singleton.</summary>
public sealed class InMemorySalesExceptionStore : ISalesExceptionStore
{
    private readonly object _gate = new();
    private readonly List<SalesExceptionRequest> _rows = new();
    private readonly List<SalesExceptionAuditEntry> _audit = new();
    private long _auditId;

    public Task<SalesExceptionRequest> CreateAsync(
        SalesExceptionRequest request,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = request.Clone();
            if (row.Id == Guid.Empty)
            {
                row.Id = Guid.NewGuid();
            }
            if (row.RequestedAtUtc == default)
            {
                row.RequestedAtUtc = DateTime.UtcNow;
            }
            row.Status = ExceptionStatuses.Pending;
            row.DecidedAtUtc = null;
            row.DecisionMakerUserName = null;
            row.DecisionMakerDisplayName = null;
            row.DecisionNote = null;
            row.BranchCustomerNotePosted = false;
            _rows.Add(row);
            AppendAudit(audit, row.Id, row.RequestedAtUtc);
            return Task.FromResult(row.Clone());
        }
    }

    public Task<PagedResult<SalesExceptionRequest>> QueryAsync(SalesExceptionQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        lock (_gate)
        {
            IEnumerable<SalesExceptionRequest> rows = _rows;

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                rows = rows.Where(r => string.Equals(r.Status, query.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(query.CityValue))
            {
                rows = rows.Where(r => string.Equals(r.CityValue, query.CityValue.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(query.TargetApproverType))
            {
                rows = rows.Where(r => string.Equals(r.TargetApproverType, query.TargetApproverType.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(query.RequestingManagerUserName))
            {
                rows = rows.Where(r => string.Equals(
                    r.RequestingManagerUserName,
                    query.RequestingManagerUserName.Trim(),
                    StringComparison.OrdinalIgnoreCase));
            }

            // Reverse first so the stable sort breaks equal timestamps by newest insert.
            var ordered = rows
                .Reverse()
                .OrderByDescending(r => r.RequestedAtUtc)
                .ToList();

            var items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => r.Clone())
                .ToList();

            return Task.FromResult(new PagedResult<SalesExceptionRequest>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = ordered.Count
            });
        }
    }

    public Task<SalesExceptionRequest?> GetAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_rows.FirstOrDefault(r => r.Id == id)?.Clone());
        }
    }

    public Task<IReadOnlyList<SalesExceptionAuditEntry>> GetAuditAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<SalesExceptionAuditEntry> rows = _audit
                .Where(a => a.ExceptionRequestId == id)
                .OrderBy(a => a.CreatedAtUtc)
                .ThenBy(a => a.Id)
                .Select(a => a.Clone())
                .ToList();
            return Task.FromResult(rows);
        }
    }

    public Task<SalesExceptionRequest?> TryDecideAsync(
        Guid id,
        string expectedCurrentStatus,
        SalesExceptionDecision decision,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            if (row is null || !string.Equals(row.Status, expectedCurrentStatus, StringComparison.Ordinal))
            {
                return Task.FromResult<SalesExceptionRequest?>(null);
            }

            row.Status = decision.NewStatus;
            row.DecidedAtUtc = decision.DecidedAtUtc;
            row.DecisionMakerUserName = decision.DecisionMakerUserName;
            row.DecisionMakerDisplayName = decision.DecisionMakerDisplayName;
            row.DecisionNote = decision.DecisionNote;
            AppendAudit(audit, id, decision.DecidedAtUtc);
            return Task.FromResult<SalesExceptionRequest?>(row.Clone());
        }
    }

    public Task MarkBranchNotePostedAsync(Guid id, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows.FirstOrDefault(r => r.Id == id);
            if (row is not null)
            {
                row.BranchCustomerNotePosted = true;
            }
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(string? cityValue, CancellationToken ct = default)
    {
        lock (_gate)
        {
            IEnumerable<SalesExceptionRequest> rows = _rows;
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                rows = rows.Where(r => string.Equals(r.CityValue, cityValue.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            IReadOnlyDictionary<string, int> counts = rows
                .GroupBy(r => r.Status, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            return Task.FromResult(counts);
        }
    }

    private void AppendAudit(SalesExceptionAuditEntry audit, Guid requestId, DateTime fallbackCreatedAtUtc)
    {
        var entry = audit.Clone();
        entry.Id = ++_auditId;
        entry.ExceptionRequestId = requestId;
        if (entry.CreatedAtUtc == default)
        {
            entry.CreatedAtUtc = fallbackCreatedAtUtc;
        }
        _audit.Add(entry);
    }
}
