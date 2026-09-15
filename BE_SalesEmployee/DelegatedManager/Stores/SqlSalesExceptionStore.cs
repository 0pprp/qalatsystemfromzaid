using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

/// <summary>
/// Gateway SQL implementation over dbo.SalesExceptionRequests / dbo.SalesExceptionAudit.
/// Decisions use a guarded UPDATE so two approvers cannot both win.
/// </summary>
public sealed class SqlSalesExceptionStore : ISalesExceptionStore
{
    private const string Columns = """
        Id, CityValue, CityName, CustomerId, CustomerName, CustomerPhone, SalesRequestId,
        RequestingManagerUserName, RequestingManagerDisplayName, Reason, TargetApproverType, Status,
        RequestedAtUtc, DecidedAtUtc, DecisionMakerUserName, DecisionMakerDisplayName, DecisionNote,
        BranchCustomerNotePosted
        """;

    private const string InsertAuditSql = """
        INSERT INTO dbo.SalesExceptionAudit
            (ExceptionRequestId, ActorUserName, ActorDisplayName, ActorRole, PreviousStatus, NewStatus, DecisionNote, CityValue, CreatedAtUtc)
        VALUES
            (@ExceptionRequestId, @ActorUserName, @ActorDisplayName, @ActorRole, @PreviousStatus, @NewStatus, @DecisionNote, @CityValue, @CreatedAtUtc);
        """;

    private readonly SqlGatewayConnectionFactory _factory;

    public SqlSalesExceptionStore(SqlGatewayConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<SalesExceptionRequest> CreateAsync(
        SalesExceptionRequest request,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default)
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
        row.BranchCustomerNotePosted = false;

        var auditRow = audit.Clone();
        auditRow.ExceptionRequestId = row.Id;
        if (auditRow.CreatedAtUtc == default)
        {
            auditRow.CreatedAtUtc = row.RequestedAtUtc;
        }

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO dbo.SalesExceptionRequests ({Columns})
            VALUES (@Id, @CityValue, @CityName, @CustomerId, @CustomerName, @CustomerPhone, @SalesRequestId,
                    @RequestingManagerUserName, @RequestingManagerDisplayName, @Reason, @TargetApproverType, @Status,
                    @RequestedAtUtc, @DecidedAtUtc, @DecisionMakerUserName, @DecisionMakerDisplayName, @DecisionNote,
                    @BranchCustomerNotePosted);
            """, row, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(InsertAuditSql, auditRow, cancellationToken: ct));
        return row;
    }

    public async Task<PagedResult<SalesExceptionRequest>> QueryAsync(SalesExceptionQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        var parameters = new DynamicParameters();
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Status", Trimmed(query.Status));
        parameters.Add("CityValue", Trimmed(query.CityValue));
        parameters.Add("TargetApproverType", Trimmed(query.TargetApproverType));
        parameters.Add("RequestingManagerUserName", Trimmed(query.RequestingManagerUserName));

        const string where = """
            WHERE (@Status IS NULL OR Status = @Status)
              AND (@CityValue IS NULL OR CityValue = @CityValue)
              AND (@TargetApproverType IS NULL OR TargetApproverType = @TargetApproverType)
              AND (@RequestingManagerUserName IS NULL OR RequestingManagerUserName = @RequestingManagerUserName)
            """;

        using var connection = await _factory.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.SalesExceptionRequests
            {where}
            ORDER BY RequestedAtUtc DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM dbo.SalesExceptionRequests {where};
            """, parameters, cancellationToken: ct));

        var items = (await grid.ReadAsync<SalesExceptionRequest>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<SalesExceptionRequest>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<SalesExceptionRequest?> GetAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.SalesExceptionRequests WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SalesExceptionAuditEntry>> GetAuditAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<SalesExceptionAuditEntry>(new CommandDefinition("""
            SELECT Id, ExceptionRequestId, ActorUserName, ActorDisplayName, ActorRole,
                   PreviousStatus, NewStatus, DecisionNote, CityValue, CreatedAtUtc
            FROM dbo.SalesExceptionAudit
            WHERE ExceptionRequestId = @id
            ORDER BY CreatedAtUtc, Id;
            """, new { id }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<SalesExceptionRequest?> TryDecideAsync(
        Guid id,
        string expectedCurrentStatus,
        SalesExceptionDecision decision,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.SalesExceptionRequests
            SET Status = @NewStatus,
                DecidedAtUtc = @DecidedAtUtc,
                DecisionMakerUserName = @DecisionMakerUserName,
                DecisionMakerDisplayName = @DecisionMakerDisplayName,
                DecisionNote = @DecisionNote
            WHERE Id = @Id AND Status = @ExpectedStatus;
            """, new
        {
            Id = id,
            ExpectedStatus = expectedCurrentStatus,
            decision.NewStatus,
            decision.DecidedAtUtc,
            decision.DecisionMakerUserName,
            decision.DecisionMakerDisplayName,
            decision.DecisionNote
        }, cancellationToken: ct));

        if (affected == 0)
        {
            return null;
        }

        var auditRow = audit.Clone();
        auditRow.ExceptionRequestId = id;
        if (auditRow.CreatedAtUtc == default)
        {
            auditRow.CreatedAtUtc = decision.DecidedAtUtc;
        }
        await connection.ExecuteAsync(new CommandDefinition(InsertAuditSql, auditRow, cancellationToken: ct));

        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.SalesExceptionRequests WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task MarkBranchNotePostedAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.SalesExceptionRequests SET BranchCustomerNotePosted = 1 WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(string? cityValue, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<StatusCountRow>(new CommandDefinition("""
            SELECT Status, COUNT(1) AS Total
            FROM dbo.SalesExceptionRequests
            WHERE (@cityValue IS NULL OR CityValue = @cityValue)
            GROUP BY Status;
            """, new { cityValue = Trimmed(cityValue) }, cancellationToken: ct));

        return rows.ToDictionary(r => r.Status, r => r.Total, StringComparer.Ordinal);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class StatusCountRow
    {
        public string Status { get; set; } = "";
        public int Total { get; set; }
    }
}
