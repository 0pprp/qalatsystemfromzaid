using BE_SalesEmployee.DelegatedManager.Models;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class SqlCustomerWaiverStore : ICustomerWaiverStore
{
    private const string Columns = """
        Id, CityValue, CityName, CustomerId, CustomerName, CustomerPhone,
        BranchManagerUserName, BranchManagerDisplayName, BranchManagerUserId, Note, Status,
        RequestedAtUtc, DecidedAtUtc, DecisionMakerUserName, DecisionMakerDisplayName, DecisionNote,
        ApprovedAmount, PaymentPosted, AccountantNotified
        """;

    private readonly SqlGatewayConnectionFactory _factory;
    private int _schemaReady;

    public SqlCustomerWaiverStore(SqlGatewayConnectionFactory factory)
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
IF OBJECT_ID(N'dbo.CustomerWaiverRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerWaiverRequests
    (
        Id                              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomerWaiverRequests PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        CityValue                       NVARCHAR(64)     NOT NULL,
        CityName                        NVARCHAR(128)    NULL,
        CustomerId                      INT              NOT NULL,
        CustomerName                    NVARCHAR(256)    NULL,
        CustomerPhone                   NVARCHAR(64)     NULL,
        BranchManagerUserName           NVARCHAR(128)    NOT NULL,
        BranchManagerDisplayName        NVARCHAR(256)    NOT NULL,
        BranchManagerUserId             INT              NULL,
        Note                            NVARCHAR(MAX)    NOT NULL,
        Status                          NVARCHAR(32)     NOT NULL CONSTRAINT DF_CustomerWaiver_Status DEFAULT (N'Pending'),
        RequestedAtUtc                  DATETIME2(3)     NOT NULL CONSTRAINT DF_CustomerWaiver_Requested DEFAULT (SYSUTCDATETIME()),
        DecidedAtUtc                    DATETIME2(3)     NULL,
        DecisionMakerUserName           NVARCHAR(128)    NULL,
        DecisionMakerDisplayName        NVARCHAR(256)    NULL,
        DecisionNote                    NVARCHAR(MAX)    NULL,
        ApprovedAmount                  FLOAT            NULL,
        PaymentPosted                   BIT              NOT NULL CONSTRAINT DF_CustomerWaiver_PaymentPosted DEFAULT (0),
        AccountantNotified              BIT              NOT NULL CONSTRAINT DF_CustomerWaiver_AcctNotified DEFAULT (0),
        CONSTRAINT CK_CustomerWaiver_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected'))
    );
END

IF OBJECT_ID(N'dbo.CustomerWaiverAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerWaiverAudit
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerWaiverAudit PRIMARY KEY,
        WaiverId            UNIQUEIDENTIFIER NOT NULL,
        Action              NVARCHAR(64)     NOT NULL,
        ActorUserName       NVARCHAR(128)    NULL,
        ActorDisplayName    NVARCHAR(256)    NULL,
        DetailJson          NVARCHAR(MAX)    NULL,
        CreatedAtUtc        DATETIME2(3)     NOT NULL CONSTRAINT DF_CustomerWaiverAudit_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CustomerWaiverAudit_Request FOREIGN KEY (WaiverId) REFERENCES dbo.CustomerWaiverRequests(Id)
    );
END
""", cancellationToken: ct));
        }
        catch
        {
            Interlocked.Exchange(ref _schemaReady, 0);
            throw;
        }
    }

    public async Task<CustomerWaiverRequest> CreateAsync(
        CustomerWaiverRequest request,
        CustomerWaiverAuditEntry audit,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var row = request.Clone();
        if (row.Id == Guid.Empty) row.Id = Guid.NewGuid();
        if (row.RequestedAtUtc == default) row.RequestedAtUtc = DateTime.UtcNow;
        row.Status = "Pending";
        row.PaymentPosted = false;
        row.AccountantNotified = false;

        var auditRow = audit.Clone();
        auditRow.WaiverId = row.Id;
        if (auditRow.CreatedAtUtc == default) auditRow.CreatedAtUtc = row.RequestedAtUtc;

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO dbo.CustomerWaiverRequests ({Columns})
            VALUES (@Id, @CityValue, @CityName, @CustomerId, @CustomerName, @CustomerPhone,
                    @BranchManagerUserName, @BranchManagerDisplayName, @BranchManagerUserId, @Note, @Status,
                    @RequestedAtUtc, @DecidedAtUtc, @DecisionMakerUserName, @DecisionMakerDisplayName, @DecisionNote,
                    @ApprovedAmount, @PaymentPosted, @AccountantNotified);
            """, row, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO dbo.CustomerWaiverAudit
                (WaiverId, Action, ActorUserName, ActorDisplayName, DetailJson, CreatedAtUtc)
            VALUES
                (@WaiverId, @Action, @ActorUserName, @ActorDisplayName, @DetailJson, @CreatedAtUtc);
            """, auditRow, cancellationToken: ct));
        return row;
    }

    public async Task<PagedResult<CustomerWaiverRequest>> QueryAsync(CustomerWaiverQuery query, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        var parameters = new DynamicParameters();
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);
        parameters.Add("Status", Trimmed(query.Status));
        parameters.Add("CityValue", Trimmed(query.CityValue));
        parameters.Add("BranchManagerUserName", Trimmed(query.BranchManagerUserName));

        const string where = """
            WHERE (@Status IS NULL OR Status = @Status)
              AND (@CityValue IS NULL OR CityValue = @CityValue)
              AND (@BranchManagerUserName IS NULL OR BranchManagerUserName = @BranchManagerUserName)
            """;

        using var connection = await _factory.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT {Columns}
            FROM dbo.CustomerWaiverRequests
            {where}
            ORDER BY RequestedAtUtc DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(1) FROM dbo.CustomerWaiverRequests {where};
            """, parameters, cancellationToken: ct));

        var items = (await grid.ReadAsync<CustomerWaiverRequest>()).ToList();
        var total = await grid.ReadSingleAsync<int>();
        return new PagedResult<CustomerWaiverRequest>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<CustomerWaiverRequest?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<CustomerWaiverRequest>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.CustomerWaiverRequests WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CustomerWaiverAuditEntry>> GetAuditAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<CustomerWaiverAuditEntry>(new CommandDefinition("""
            SELECT Id, WaiverId, Action, ActorUserName, ActorDisplayName, DetailJson, CreatedAtUtc
            FROM dbo.CustomerWaiverAudit
            WHERE WaiverId = @id
            ORDER BY CreatedAtUtc, Id;
            """, new { id }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<CustomerWaiverRequest?> TryDecideAsync(
        Guid id,
        string expectedCurrentStatus,
        CustomerWaiverDecision decision,
        CustomerWaiverAuditEntry audit,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.CustomerWaiverRequests
            SET Status = @NewStatus,
                DecidedAtUtc = @DecidedAtUtc,
                DecisionMakerUserName = @DecisionMakerUserName,
                DecisionMakerDisplayName = @DecisionMakerDisplayName,
                DecisionNote = @DecisionNote,
                ApprovedAmount = @ApprovedAmount
            WHERE Id = @Id AND Status = @ExpectedStatus;
            """, new
        {
            Id = id,
            ExpectedStatus = expectedCurrentStatus,
            decision.NewStatus,
            decision.DecidedAtUtc,
            decision.DecisionMakerUserName,
            decision.DecisionMakerDisplayName,
            decision.DecisionNote,
            decision.ApprovedAmount
        }, cancellationToken: ct));

        if (affected == 0) return null;

        var auditRow = audit.Clone();
        auditRow.WaiverId = id;
        if (auditRow.CreatedAtUtc == default) auditRow.CreatedAtUtc = decision.DecidedAtUtc;
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO dbo.CustomerWaiverAudit
                (WaiverId, Action, ActorUserName, ActorDisplayName, DetailJson, CreatedAtUtc)
            VALUES
                (@WaiverId, @Action, @ActorUserName, @ActorDisplayName, @DetailJson, @CreatedAtUtc);
            """, auditRow, cancellationToken: ct));

        return await connection.QuerySingleOrDefaultAsync<CustomerWaiverRequest>(new CommandDefinition($"""
            SELECT {Columns} FROM dbo.CustomerWaiverRequests WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task MarkPaymentPostedAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.CustomerWaiverRequests SET PaymentPosted = 1 WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task MarkAccountantNotifiedAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.CustomerWaiverRequests SET AccountantNotified = 1 WHERE Id = @id;
            """, new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        string? cityValue,
        string? branchManagerUserName,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var connection = await _factory.OpenAsync(ct);
        var rows = await connection.QueryAsync<StatusCountRow>(new CommandDefinition("""
            SELECT Status, COUNT(1) AS Total
            FROM dbo.CustomerWaiverRequests
            WHERE (@cityValue IS NULL OR CityValue = @cityValue)
              AND (@branchManagerUserName IS NULL OR BranchManagerUserName = @branchManagerUserName)
            GROUP BY Status;
            """, new
        {
            cityValue = Trimmed(cityValue),
            branchManagerUserName = Trimmed(branchManagerUserName)
        }, cancellationToken: ct));

        return rows.ToDictionary(r => r.Status, r => r.Total, StringComparer.OrdinalIgnoreCase);
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class StatusCountRow
    {
        public string Status { get; set; } = "";
        public int Total { get; set; }
    }
}
