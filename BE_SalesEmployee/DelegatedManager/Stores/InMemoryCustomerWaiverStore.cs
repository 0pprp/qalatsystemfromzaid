using System.Collections.Concurrent;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class InMemoryCustomerWaiverStore : ICustomerWaiverStore
{
    private readonly ConcurrentDictionary<Guid, CustomerWaiverRequest> _rows = new();
    private readonly ConcurrentDictionary<Guid, List<CustomerWaiverAuditEntry>> _audit = new();
    private long _auditSeq;

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<CustomerWaiverRequest> CreateAsync(
        CustomerWaiverRequest request,
        CustomerWaiverAuditEntry audit,
        CancellationToken ct = default)
    {
        var row = request.Clone();
        if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
        if (row.RequestedAtUtc == default) row.RequestedAtUtc = DateTime.UtcNow;
        row.Status = "Pending";
        row.PaymentPosted = false;
        row.AccountantNotified = false;
        _rows[row.Id] = row;

        var auditRow = audit.Clone();
        auditRow.Id = Interlocked.Increment(ref _auditSeq);
        auditRow.WaiverId = row.Id;
        if (auditRow.CreatedAtUtc == default) auditRow.CreatedAtUtc = row.RequestedAtUtc;
        _audit.AddOrUpdate(row.Id, _ => [auditRow], (_, list) =>
        {
            list.Add(auditRow);
            return list;
        });
        return Task.FromResult(row.Clone());
    }

    public Task<PagedResult<CustomerWaiverRequest>> QueryAsync(CustomerWaiverQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        var filtered = _rows.Values
            .Where(r => string.IsNullOrWhiteSpace(query.Status) ||
                        string.Equals(r.Status, query.Status.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrWhiteSpace(query.CityValue) ||
                        string.Equals(r.CityValue, query.CityValue.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrWhiteSpace(query.BranchManagerUserName) ||
                        string.Equals(r.BranchManagerUserName, query.BranchManagerUserName.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.RequestedAtUtc)
            .ThenByDescending(r => r.Id)
            .ToList();

        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Clone()).ToList();
        return Task.FromResult(new PagedResult<CustomerWaiverRequest>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = filtered.Count
        });
    }

    public Task<CustomerWaiverRequest?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_rows.TryGetValue(id, out var row) ? row.Clone() : null);

    public Task<IReadOnlyList<CustomerWaiverAuditEntry>> GetAuditAsync(Guid id, CancellationToken ct = default)
    {
        if (!_audit.TryGetValue(id, out var list))
        {
            return Task.FromResult<IReadOnlyList<CustomerWaiverAuditEntry>>([]);
        }

        return Task.FromResult<IReadOnlyList<CustomerWaiverAuditEntry>>(
            list.OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id).Select(a => a.Clone()).ToList());
    }

    public Task<CustomerWaiverRequest?> TryDecideAsync(
        Guid id,
        string expectedCurrentStatus,
        CustomerWaiverDecision decision,
        CustomerWaiverAuditEntry audit,
        CancellationToken ct = default)
    {
        if (!_rows.TryGetValue(id, out var row)) return Task.FromResult<CustomerWaiverRequest?>(null);
        if (!string.Equals(row.Status, expectedCurrentStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<CustomerWaiverRequest?>(null);
        }

        row.Status = decision.NewStatus;
        row.DecidedAtUtc = decision.DecidedAtUtc;
        row.DecisionMakerUserName = decision.DecisionMakerUserName;
        row.DecisionMakerDisplayName = decision.DecisionMakerDisplayName;
        row.DecisionNote = decision.DecisionNote;
        row.ApprovedAmount = decision.ApprovedAmount;

        var auditRow = audit.Clone();
        auditRow.Id = Interlocked.Increment(ref _auditSeq);
        auditRow.WaiverId = id;
        if (auditRow.CreatedAtUtc == default) auditRow.CreatedAtUtc = decision.DecidedAtUtc;
        _audit.AddOrUpdate(id, _ => [auditRow], (_, list) =>
        {
            list.Add(auditRow);
            return list;
        });
        return Task.FromResult<CustomerWaiverRequest?>(row.Clone());
    }

    public Task MarkPaymentPostedAsync(Guid id, CancellationToken ct = default)
    {
        if (_rows.TryGetValue(id, out var row)) row.PaymentPosted = true;
        return Task.CompletedTask;
    }

    public Task MarkAccountantNotifiedAsync(Guid id, CancellationToken ct = default)
    {
        if (_rows.TryGetValue(id, out var row)) row.AccountantNotified = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        string? cityValue,
        string? branchManagerUserName,
        CancellationToken ct = default)
    {
        var dict = _rows.Values
            .Where(r => string.IsNullOrWhiteSpace(cityValue) ||
                        string.Equals(r.CityValue, cityValue.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(r => string.IsNullOrWhiteSpace(branchManagerUserName) ||
                        string.Equals(r.BranchManagerUserName, branchManagerUserName.Trim(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.Status, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return Task.FromResult<IReadOnlyDictionary<string, int>>(dict);
    }
}
