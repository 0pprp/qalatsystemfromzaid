using System.Threading;
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
    private int _schemaReady;

    private const string Columns = """
        Id, CityValue, CityName, CustomerId, CustomerName, CustomerPhone, SalesRequestId,
        RequestingManagerUserName, RequestingManagerDisplayName, Reason, TargetApproverType, Status,
        RequestedAtUtc, ReadAtUtc, DecidedAtUtc, DecisionMakerUserName, DecisionMakerDisplayName, DecisionNote,
        BranchCustomerNotePosted, AssignmentConsumed, AssignedEmployeeId, AssignedEmployeeName,
        AssignedAtUtc, AssignedByManagerUserName,
        BranchManagerReviewedAtUtc, BranchManagerReviewerUserName, BranchManagerReviewerDisplayName, BranchManagerReviewNote
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

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _schemaReady, 1, 0) == 1)
        {
            return;
        }

        try
        {
            using var connection = await _factory.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition("""
IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'ReadAtUtc') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD ReadAtUtc DATETIME2(3) NULL;
IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'BranchManagerReviewedAtUtc') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD BranchManagerReviewedAtUtc DATETIME2(3) NULL;
IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'BranchManagerReviewerUserName') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD BranchManagerReviewerUserName NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'BranchManagerReviewerDisplayName') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD BranchManagerReviewerDisplayName NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'BranchManagerReviewNote') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD BranchManagerReviewNote NVARCHAR(2000) NULL;
""", cancellationToken: ct));
        }
        catch
        {
            Interlocked.Exchange(ref _schemaReady, 0);
            throw;
        }
    }

    public async Task<SalesExceptionRequest> CreateAsync(
        SalesExceptionRequest request,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
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
        row.ReadAtUtc = null;
        row.BranchCustomerNotePosted = false;
        row.AssignmentConsumed = false;
        row.AssignedEmployeeId = null;
        row.AssignedEmployeeName = null;
        row.AssignedAtUtc = null;
        row.AssignedByManagerUserName = null;
        row.BranchManagerReviewedAtUtc = null;
        row.BranchManagerReviewerUserName = null;
        row.BranchManagerReviewerDisplayName = null;
        row.BranchManagerReviewNote = null;

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
                    @RequestedAtUtc, @ReadAtUtc, @DecidedAtUtc, @DecisionMakerUserName, @DecisionMakerDisplayName, @DecisionNote,
                    @BranchCustomerNotePosted, @AssignmentConsumed, @AssignedEmployeeId, @AssignedEmployeeName,
                    @AssignedAtUtc, @AssignedByManagerUserName,
                    @BranchManagerReviewedAtUtc, @BranchManagerReviewerUserName, @BranchManagerReviewerDisplayName, @BranchManagerReviewNote);
            """, row, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(InsertAuditSql, auditRow, cancellationToken: ct));
        return row;
    }

    public async Task<PagedResult<SalesExceptionRequest>> QueryAsync(SalesExceptionQuery query, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
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
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.SalesExceptionRequests WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<SalesExceptionRequest?> MarkReadAsync(Guid id, DateTime readAtUtc, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.SalesExceptionRequests
            SET ReadAtUtc = COALESCE(ReadAtUtc, @readAtUtc)
            WHERE Id = @id;
            """, new { id, readAtUtc }, cancellationToken: ct));
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
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<StatusCountRow>(new CommandDefinition("""
            SELECT Status, COUNT(1) AS Total
            FROM dbo.SalesExceptionRequests
            WHERE (@cityValue IS NULL OR CityValue = @cityValue)
            GROUP BY Status;
            """, new { cityValue = Trimmed(cityValue) }, cancellationToken: ct));

        return rows.ToDictionary(r => r.Status, r => r.Total, StringComparer.Ordinal);
    }

    private const string WrongSaleReasonSql = """
        (Reason LIKE N'%مبيعات خاطئة%' OR Reason LIKE N'%تقييم المتجر%')
        """;

    public async Task<IReadOnlyDictionary<string, int>> CountHoldByStatusAsync(string? cityValue, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<StatusCountRow>(new CommandDefinition($"""
            SELECT Status, COUNT(1) AS Total
            FROM dbo.SalesExceptionRequests
            WHERE (@cityValue IS NULL OR CityValue = @cityValue)
              AND NOT {WrongSaleReasonSql}
            GROUP BY Status;
            """, new { cityValue = Trimmed(cityValue) }, cancellationToken: ct));

        return rows.ToDictionary(r => r.Status, r => r.Total, StringComparer.Ordinal);
    }

    public async Task<int> CountUnreadWrongSalesAsync(string? cityValue, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT COUNT(1)
            FROM dbo.SalesExceptionRequests
            WHERE (@cityValue IS NULL OR CityValue = @cityValue)
              AND ReadAtUtc IS NULL
              AND {WrongSaleReasonSql};
            """, new { cityValue = Trimmed(cityValue) }, cancellationToken: ct));
    }

    public async Task<PagedResult<SalesExceptionRequest>> QueryWrongSalesForBranchManagerAsync(
        string? cityValue,
        bool reviewed,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var parameters = new DynamicParameters();
        parameters.Add("Offset", (normalizedPage - 1) * normalizedSize);
        parameters.Add("PageSize", normalizedSize);
        parameters.Add("CityValue", Trimmed(cityValue));
        parameters.Add("Reviewed", reviewed ? 1 : 0);

        var where = $"""
            WHERE {WrongSaleReasonSql}
              AND (@CityValue IS NULL OR CityValue = @CityValue)
              AND (
                    (@Reviewed = 1 AND BranchManagerReviewedAtUtc IS NOT NULL)
                 OR (@Reviewed = 0 AND BranchManagerReviewedAtUtc IS NULL)
                  )
            """;

        using var connection = await _factory.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.SalesExceptionRequests
            {where}
            ORDER BY
                CASE WHEN @Reviewed = 1 THEN BranchManagerReviewedAtUtc ELSE RequestedAtUtc END DESC,
                Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM dbo.SalesExceptionRequests {where};
            """, parameters, cancellationToken: ct));

        var items = (await grid.ReadAsync<SalesExceptionRequest>()).ToList();
        var total = await grid.ReadSingleAsync<int>();
        return new PagedResult<SalesExceptionRequest>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedSize,
            TotalCount = total
        };
    }

    public async Task<SalesExceptionRequest?> TryMarkBranchManagerReviewedAsync(
        Guid id,
        string reviewerUserName,
        string reviewerDisplayName,
        string reviewNote,
        DateTime reviewedAtUtc,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition($"""
            UPDATE dbo.SalesExceptionRequests
            SET BranchManagerReviewedAtUtc = @reviewedAtUtc,
                BranchManagerReviewerUserName = @reviewerUserName,
                BranchManagerReviewerDisplayName = @reviewerDisplayName,
                BranchManagerReviewNote = @reviewNote
            WHERE Id = @id
              AND BranchManagerReviewedAtUtc IS NULL
              AND {WrongSaleReasonSql};
            """, new
        {
            id,
            reviewedAtUtc,
            reviewerUserName,
            reviewerDisplayName,
            reviewNote
        }, cancellationToken: ct));

        if (affected == 0)
        {
            return null;
        }

        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.SalesExceptionRequests WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<SalesExceptionRequest?> FindActiveBySalesRequestIdAsync(int salesRequestId, CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition($"""
            SELECT TOP (1) {Columns}
            FROM dbo.SalesExceptionRequests
            WHERE SalesRequestId = @salesRequestId
              AND (
                    Status = N'Pending'
                    OR (Status = N'Approved' AND AssignmentConsumed = 0)
                  )
            ORDER BY RequestedAtUtc DESC;
            """, new { salesRequestId }, cancellationToken: ct));
    }

    public async Task<SalesExceptionRequest?> FindPendingWrongSaleByCustomerIdAsync(int customerId, CancellationToken ct = default)
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

        public async Task<SalesExceptionRequest?> TryConsumeAsync(
        Guid id,
        int employeeId,
        string? employeeName,
        string? assignedByManagerUserName,
        DateTime assignedAtUtc,
        SalesExceptionAuditEntry audit,
        CancellationToken ct = default)
    {
        using var connection = await _factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.SalesExceptionRequests
            SET AssignmentConsumed = 1,
                AssignedEmployeeId = @employeeId,
                AssignedEmployeeName = @employeeName,
                AssignedAtUtc = @assignedAtUtc,
                AssignedByManagerUserName = @assignedByManagerUserName
            WHERE Id = @id
              AND Status = N'Approved'
              AND AssignmentConsumed = 0;
            """, new
        {
            id,
            employeeId,
            employeeName,
            assignedAtUtc,
            assignedByManagerUserName
        }, cancellationToken: ct));

        if (affected == 0)
        {
            return null;
        }

        var auditRow = audit.Clone();
        auditRow.ExceptionRequestId = id;
        if (auditRow.CreatedAtUtc == default)
        {
            auditRow.CreatedAtUtc = assignedAtUtc;
        }
        await connection.ExecuteAsync(new CommandDefinition(InsertAuditSql, auditRow, cancellationToken: ct));

        return await connection.QuerySingleOrDefaultAsync<SalesExceptionRequest>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.SalesExceptionRequests WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class StatusCountRow
    {
        public string Status { get; set; } = "";
        public int Total { get; set; }
    }
}
