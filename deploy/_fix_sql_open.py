from pathlib import Path
p = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\BE_SalesEmployee\DelegatedManager\Stores\SqlSalesExceptionStore.cs")
t = p.read_text(encoding="utf-8")
start = t.find("public async Task<SalesExceptionRequest?> FindPendingWrongSaleByCustomerIdAsync")
if start < 0:
    raise SystemExit("method not found")
# replace from method start through its closing brace before next method
end = t.find("\n    public ", start + 10)
if end < 0:
    end = t.find("\n    private ", start + 10)
method = '''public async Task<SalesExceptionRequest?> FindPendingWrongSaleByCustomerIdAsync(int customerId, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition($"""
            SELECT TOP (1) {Columns}
            FROM dbo.SalesExceptionRequests
            WHERE CustomerId = @customerId
              AND Status = N'Pending'
              AND {WrongSaleReasonSql}
            ORDER BY RequestedAtUtc DESC;
            """, new { customerId }, cancellationToken: ct));
    }

    '''
t = t[:start] + method + t[end+1:]
p.write_text(t, encoding="utf-8")
print("fixed method")
