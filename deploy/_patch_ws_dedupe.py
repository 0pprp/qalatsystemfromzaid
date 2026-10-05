from pathlib import Path

# Add FindPendingWrongSaleByCustomerId to store + CreateAsync dedupe
iface = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\BE_SalesEmployee\DelegatedManager\Stores\ISalesExceptionStore.cs")
t = iface.read_text(encoding="utf-8")
needle = "    Task<SalesExceptionRequest?> FindActiveBySalesRequestIdAsync(int salesRequestId, CancellationToken ct = default);"
insert = needle + """
    /// <summary>Pending WrongSale notice for same customer (idempotent intake).</summary>
    Task<SalesExceptionRequest?> FindPendingWrongSaleByCustomerIdAsync(int customerId, CancellationToken ct = default);"""
if "FindPendingWrongSaleByCustomerIdAsync" not in t:
    if needle not in t:
        raise SystemExit("iface needle missing")
    iface.write_text(t.replace(needle, insert, 1), encoding="utf-8")
    print("iface ok")
else:
    print("iface already")

sql = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\BE_SalesEmployee\DelegatedManager\Stores\SqlSalesExceptionStore.cs")
t = sql.read_text(encoding="utf-8")
if "FindPendingWrongSaleByCustomerIdAsync" not in t:
    # append before private sealed class or at end of class methods near FindActive
    marker = "    public async Task<SalesExceptionRequest?> FindActiveBySalesRequestIdAsync(int salesRequestId, CancellationToken ct = default)"
    if marker not in t:
        raise SystemExit("sql marker missing")
    # find end of FindActive method - insert after it
    idx = t.find(marker)
    # find next public/private after method body - crude: after first closing of method
    rest = t[idx:]
    # insert new method before CountByStatus or after FindActive block ends at next "    public"
    next_pub = rest.find("\n    public ", 10)
    if next_pub < 0:
        raise SystemExit("next public missing")
    insert_at = idx + next_pub
    method = '''
    public async Task<SalesExceptionRequest?> FindPendingWrongSaleByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition("""
            SELECT TOP 1 ''' + "Id, CityValue, CityName, CustomerId, CustomerName, CustomerPhone, SalesRequestId,\n        RequestingManagerUserName, RequestingManagerDisplayName, Reason, TargetApproverType, Status,\n        DecisionMakerUserName, DecisionMakerDisplayName, DecisionNote, RequestedAtUtc, DecidedAtUtc, ReadAtUtc,\n        BranchCustomerNotePosted, AssignmentConsumed, AssignedEmployeeId, AssignedEmployeeName, AssignedAtUtc" + '''
            FROM dbo.SalesExceptionRequests
            WHERE CustomerId = @customerId
              AND Status = N\'Pending\'
              AND (Reason LIKE N\'%مبيعات خاطئة%\' OR Reason LIKE N\'%تقييم المتجر%\')
            ORDER BY RequestedAtUtc DESC;
            """, new { customerId }, cancellationToken: ct));
    }

'''
    # Fix the method properly without broken string
    method = r'''
    public async Task<SalesExceptionRequest?> FindPendingWrongSaleByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition("""
            SELECT TOP 1
                Id, CityValue, CityName, CustomerId, CustomerName, CustomerPhone, SalesRequestId,
                RequestingManagerUserName, RequestingManagerDisplayName, Reason, TargetApproverType, Status,
                DecisionMakerUserName, DecisionMakerDisplayName, DecisionNote, RequestedAtUtc, DecidedAtUtc, ReadAtUtc,
                BranchCustomerNotePosted, AssignmentConsumed, AssignedEmployeeId, AssignedEmployeeName, AssignedAtUtc
            FROM dbo.SalesExceptionRequests
            WHERE CustomerId = @customerId
              AND Status = N'Pending'
              AND (Reason LIKE N'%مبيعات خاطئة%' OR Reason LIKE N'%تقييم المتجر%')
            ORDER BY RequestedAtUtc DESC;
            """, new { customerId }, cancellationToken: ct));
    }

'''
    t = t[:insert_at] + method + t[insert_at:]
    sql.write_text(t, encoding="utf-8")
    print("sql ok")
else:
    print("sql already")

mem = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\BE_SalesEmployee\DelegatedManager\Stores\InMemorySalesExceptionStore.cs")
t = mem.read_text(encoding="utf-8")
if "FindPendingWrongSaleByCustomerIdAsync" not in t:
    marker = "    public Task<SalesExceptionRequest?> FindActiveBySalesRequestIdAsync(int salesRequestId, CancellationToken ct = default)"
    if marker not in t:
        raise SystemExit("mem marker missing")
    idx = t.find(marker)
    rest = t[idx:]
    next_pub = rest.find("\n    public ", 10)
    if next_pub < 0:
        next_pub = rest.find("\n    private ", 10)
    insert_at = idx + next_pub
    method = '''
    public Task<SalesExceptionRequest?> FindPendingWrongSaleByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var row = _rows
                .Where(r => r.CustomerId == customerId
                            && string.Equals(r.Status, ExceptionStatuses.Pending, StringComparison.Ordinal)
                            && IsWrongSaleReason(r.Reason))
                .OrderByDescending(r => r.RequestedAtUtc)
                .FirstOrDefault();
            return Task.FromResult(row);
        }
    }

'''
    mem.write_text(t[:insert_at] + method + t[insert_at:], encoding="utf-8")
    print("mem ok")
else:
    print("mem already")

svc = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\BE_SalesEmployee\DelegatedManager\Services\SalesExceptionService.cs")
t = svc.read_text(encoding="utf-8")
old = '''        if (input.SalesRequestId is int salesRequestId and > 0)
        {
            var active = await _store.FindActiveBySalesRequestIdAsync(salesRequestId, ct);
            if (active is not null)
            {
                return SalesExceptionResult.Conflict("يوجد طلب استثناء نشط لهذا الطلب", active);
            }
        }

        var now = DateTime.UtcNow;
'''
new = '''        if (input.SalesRequestId is int salesRequestId and > 0)
        {
            var active = await _store.FindActiveBySalesRequestIdAsync(salesRequestId, ct);
            if (active is not null)
            {
                return SalesExceptionResult.Conflict("يوجد طلب استثناء نشط لهذا الطلب", active);
            }
        }

        // WrongSale from first-collection / follower auth: idempotent on customer (retry-safe).
        var isWrongSaleIntake = reason.Contains("مبيعات خاطئة", StringComparison.Ordinal)
                                || reason.Contains("تقييم المتجر", StringComparison.Ordinal);
        if (isWrongSaleIntake && input.CustomerId is int wrongCustomerId and > 0
            && (input.SalesRequestId is null or <= 0))
        {
            var existingWrong = await _store.FindPendingWrongSaleByCustomerIdAsync(wrongCustomerId, ct);
            if (existingWrong is not null)
            {
                return SalesExceptionResult.Success(existingWrong);
            }
        }

        var now = DateTime.UtcNow;
'''
if old not in t:
    raise SystemExit("CreateAsync block missing")
if "FindPendingWrongSaleByCustomerIdAsync" not in t:
    svc.write_text(t.replace(old, new, 1), encoding="utf-8")
    print("service ok")
else:
    print("service already")
