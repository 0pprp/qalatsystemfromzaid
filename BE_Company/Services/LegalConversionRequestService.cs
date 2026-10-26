using BE_Company.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Services;

public sealed class LegalConversionRequestDto
{
    public int RequestId { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public int RequestedByUserId { get; set; }
    public string? RequestedByName { get; set; }
    public string Status { get; set; } = "Pending";
    public string? RequestNote { get; set; }
    public int? WeekDecisionId { get; set; }
    public int? ApprovedDecisionId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public double? WeekPaid { get; set; }
    public double? PaidPercent { get; set; }

    // Customer detail (same family as متابعة المندوبين / View_Customers)
    public int? DelegateId { get; set; }
    public string? DelegateName { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public string? ShopName { get; set; }
    public string? SaleName { get; set; }
    public string? ReceiptName { get; set; }
    public DateTime? DateSaleDevice { get; set; }
    public double? AmountTotalSales { get; set; }
    public double? AmountDaySales { get; set; }
    public double? ReceiptsTotal { get; set; }
    public double? AmountRemaining { get; set; }
    public string? ItemsNames { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public double? LastPaymentAmount { get; set; }
    public int? CountReceiptDevice { get; set; }
    public int? NumberOfDayDevice { get; set; }
    public int? ReceiptRateDevice { get; set; }
    public bool IsLegal { get; set; }
}

public interface ILegalConversionRequestService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<CustomerWeekDecisionGetDTO> SubmitFromBranchManagerAsync(int customerId, int userId, string? note, CancellationToken ct = default);
    Task<IReadOnlyList<LegalConversionRequestDto>> ListAsync(string? status, int take, int skip, CancellationToken ct = default);
    Task<int> CountAsync(string? status, CancellationToken ct = default);
    Task<LegalConversionRequestDto?> ApproveAsync(int requestId, int reviewerUserId, string? reviewNote, CancellationToken ct = default);
    Task<LegalConversionRequestDto?> RejectAsync(int requestId, int reviewerUserId, string? reviewNote, CancellationToken ct = default);
}

/// <summary>
/// BM "قانونية" → pending request for main accountant.
/// Leaves BM WeakWeek list (Pending blocks SP) but IsLegal stays false so delegate list keeps the customer until Approved.
/// </summary>
public sealed class LegalConversionRequestService : ILegalConversionRequestService
{
    public const string PendingStatus = "Pending";
    public const string ApprovedStatus = "Approved";
    public const string RejectedStatus = "Rejected";
    public const string RequestDecisionType = "طلب قانونية";
    public const string FinalDecisionType = "قانونية";
    /// <summary>Deploy marker — list enrich + gateway UserID=0 Activities FK fix.</summary>
    public const string DeployMarker = "LegalConversionListEnrich_V1";

    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private readonly string _cs;

    public LegalConversionRequestService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("Missing DataBaseConnection.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady)
            return;

        await using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF OBJECT_ID(N'dbo.LegalConversionRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LegalConversionRequests (
        RequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LegalConversionRequests PRIMARY KEY,
        CustomerID INT NOT NULL,
        RequestedByUserID INT NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        RequestNote NVARCHAR(MAX) NULL,
        WeekDecisionId INT NULL,
        ApprovedDecisionId INT NULL,
        ReviewedByUserID INT NULL,
        ReviewNote NVARCHAR(MAX) NULL,
        RequestedAt DATETIME2 NOT NULL CONSTRAINT DF_LCR_RequestedAt DEFAULT (SYSUTCDATETIME()),
        ReviewedAt DATETIME2 NULL,
        WeekPaid FLOAT NULL,
        PaidPercent FLOAT NULL
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_LegalConversion_PendingCustomer'
      AND object_id = OBJECT_ID(N'dbo.LegalConversionRequests')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_LegalConversion_PendingCustomer
        ON dbo.LegalConversionRequests (CustomerID)
        WHERE Status = N'Pending';
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_LegalConversion_Status_RequestedAt'
      AND object_id = OBJECT_ID(N'dbo.LegalConversionRequests')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_LegalConversion_Status_RequestedAt
        ON dbo.LegalConversionRequests (Status, RequestedAt DESC)
        INCLUDE (CustomerID, RequestedByUserID);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_LegalConversion_RequestedAt'
      AND object_id = OBJECT_ID(N'dbo.LegalConversionRequests')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_LegalConversion_RequestedAt
        ON dbo.LegalConversionRequests (RequestedAt DESC);
END
""", cancellationToken: ct));

        lock (SchemaGate)
        {
            _schemaReady = true;
        }
    }

    public async Task<CustomerWeekDecisionGetDTO> SubmitFromBranchManagerAsync(
        int customerId,
        int userId,
        string? note,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        if (customerId <= 0)
            throw new ArgumentException("يجب تحديد الزبون.");

        await using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        await using var tx = connection.BeginTransaction();

        var alreadyLegal = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN ISNULL(IsLegal,0)=1 THEN 1 ELSE 0 END FROM dbo.Customers WHERE CustomerID=@Id",
            new { Id = customerId }, tx, cancellationToken: ct));
        if (alreadyLegal == 1)
            throw new InvalidOperationException("الزبون مؤشّر قانونية مسبقاً.");

        var existing = await connection.QueryFirstOrDefaultAsync<LegalConversionRequestDto>(new CommandDefinition("""
SELECT TOP 1
    RequestId, CustomerID AS CustomerId, RequestedByUserID AS RequestedByUserId,
    Status, RequestNote, WeekDecisionId, ApprovedDecisionId,
    ReviewedByUserID AS ReviewedByUserId, ReviewNote, RequestedAt, ReviewedAt,
    WeekPaid, PaidPercent
FROM dbo.LegalConversionRequests WITH (UPDLOCK, ROWLOCK)
WHERE CustomerID = @CustomerId AND Status = N'Pending';
""", new { CustomerId = customerId }, tx, cancellationToken: ct));

        if (existing != null)
        {
            tx.Commit();
            return await LoadDecisionDtoAsync(connection, existing.WeekDecisionId, ct)
                   ?? BuildSyntheticDecision(customerId, userId, note, existing.WeekPaid, existing.PaidPercent);
        }

        var weekStart = DateTime.Today.AddDays(-7);
        var weekEnd = DateTime.Today.AddDays(-1);
        var weekPaid = await connection.ExecuteScalarAsync<double?>(new CommandDefinition("""
SELECT ROUND(SUM(ISNULL(AmountDenar, 0)), -3)
FROM dbo.View_ReceiptCustomerDate
WHERE CustomerID = @CustomerId
  AND CAST(PaymentDate AS DATE) >= @WeekStart
  AND CAST(PaymentDate AS DATE) <= @WeekEnd;
""", new { CustomerId = customerId, WeekStart = weekStart, WeekEnd = weekEnd }, tx, cancellationToken: ct)) ?? 0;

        var amountTotal = await connection.ExecuteScalarAsync<double?>(new CommandDefinition("""
SELECT TOP 1 ISNULL(AmountTotalSales, 0)
FROM dbo.View_CustomerWeekPaymentDevice
WHERE CustomerID = @CustomerId;
""", new { CustomerId = customerId }, tx, cancellationToken: ct)) ?? 0;

        var paidPercent = amountTotal > 0 ? Math.Round(weekPaid * 100.0 / amountTotal, 2) : 0;
        var customerName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT ISNULL(CustomerName, N'') FROM dbo.Customers WHERE CustomerID=@CustomerId",
            new { CustomerId = customerId }, tx, cancellationToken: ct)) ?? "";

        var decisionId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.CustomerWeekDecisions
    (CustomerID, UserID, DecisionType, WeekPaid, AmountTotalSales, PaidPercent,
     WeekStartDate, WeekEndDate, SnoozeUntil, Note, CreatedDate)
VALUES
    (@CustomerId, @UserId, N'طلب قانونية', @WeekPaid, @AmountTotal, @PaidPercent,
     @WeekStart, @WeekEnd, NULL, @Note, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);
""", new
        {
            CustomerId = customerId,
            UserId = userId,
            WeekPaid = weekPaid,
            AmountTotal = amountTotal,
            PaidPercent = paidPercent,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Note = string.IsNullOrWhiteSpace(note) ? (string?)null : note.Trim(),
        }, tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.DecisionNotifications (DecisionID, IsRead, CreatedDate)
VALUES (@DecisionId, 0, GETDATE());
""", new { DecisionId = decisionId }, tx, cancellationToken: ct));

        var requestId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.LegalConversionRequests
    (CustomerID, RequestedByUserID, Status, RequestNote, WeekDecisionId, WeekPaid, PaidPercent, RequestedAt)
VALUES
    (@CustomerId, @UserId, N'Pending', @Note, @DecisionId, @WeekPaid, @PaidPercent, SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);
""", new
        {
            CustomerId = customerId,
            UserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? (string?)null : note.Trim(),
            DecisionId = decisionId,
            WeekPaid = weekPaid,
            PaidPercent = paidPercent,
        }, tx, cancellationToken: ct));

        if (await TableExistsAsync(connection, tx, "Activities", ct))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
VALUES (@UserId, @Desc, GETUTCDATE(), 'false', NEWID());
""", new
            {
                UserId = userId,
                Desc = $"طلب تحويل قانونية للزبون {customerName} (#{requestId})",
            }, tx, cancellationToken: ct));
        }

        tx.Commit();

        var dto = await LoadDecisionDtoAsync(connection, decisionId, ct)
                  ?? BuildSyntheticDecision(customerId, userId, note, weekPaid, paidPercent);
        // BM apps expect DecisionType قانونية in UI copy; keep IsLegal=false until accountant approves.
        dto.DecisionType = FinalDecisionType;
        dto.IsLegal = false;
        dto.Note = string.IsNullOrWhiteSpace(dto.Note)
            ? "بانتظار موافقة المحاسب الرئيسي"
            : dto.Note;
        return dto;
    }

    public async Task<IReadOnlyList<LegalConversionRequestDto>> ListAsync(
        string? status,
        int take,
        int skip,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        take = Math.Clamp(take <= 0 ? 100 : take, 1, 500);
        skip = Math.Max(0, skip);

        await using var connection = new SqlConnection(_cs);
        _ = DeployMarker;
        var rows = await connection.QueryAsync<LegalConversionRequestDto>(new CommandDefinition("""
SELECT
    r.RequestId,
    r.CustomerID AS CustomerId,
    ISNULL(vc.CustomerName, c.CustomerName) AS CustomerName,
    ISNULL(vc.PhoneNumber, c.PhoneNumber) AS PhoneNumber,
    r.RequestedByUserID AS RequestedByUserId,
    u1.UserName AS RequestedByName,
    r.Status,
    r.RequestNote,
    r.WeekDecisionId,
    r.ApprovedDecisionId,
    r.ReviewedByUserID AS ReviewedByUserId,
    u2.UserName AS ReviewedByName,
    r.ReviewNote,
    r.RequestedAt,
    r.ReviewedAt,
    r.WeekPaid,
    r.PaidPercent,
    c.DelegateID AS DelegateId,
    d.DelegateName AS DelegateName,
    ISNULL(c.Address, N'') AS Address,
    ISNULL(c.Notes, N'') AS Notes,
    ISNULL(c.ShopName, N'') AS ShopName,
    CAST(NULL AS NVARCHAR(200)) AS SaleName,
    CAST(NULL AS NVARCHAR(200)) AS ReceiptName,
    vc.DateSaleDevice AS DateSaleDevice,
    CAST(ISNULL(vc.AmountTotalSales, 0) AS FLOAT) AS AmountTotalSales,
    CAST(ISNULL(vc.AmountDaySales, 0) AS FLOAT) AS AmountDaySales,
    CAST(ISNULL(vc.ReceiptsTotal, 0) AS FLOAT) AS ReceiptsTotal,
    CAST(ISNULL(vc.AmountRemaining, 0) AS FLOAT) AS AmountRemaining,
    ISNULL(vc.ItemsNames, N'') AS ItemsNames,
    vc.LastPaymentDate AS LastPaymentDate,
    CAST((
        SELECT TOP 1 ROUND(ISNULL(pay.AmountDenar, 0), -3)
        FROM dbo.View_ReceiptCustomerDate pay
        WHERE pay.CustomerID = c.CustomerID
        ORDER BY pay.PaymentDate DESC
    ) AS FLOAT) AS LastPaymentAmount,
    CAST(TRY_CONVERT(INT, vc.CountReceiptDevice) AS INT) AS CountReceiptDevice,
    CAST(ISNULL(vc.NumberOfDayPayment, 0) AS INT) AS NumberOfDayDevice,
    CAST(ISNULL(vc.ReceiptRateDevice, 0) AS INT) AS ReceiptRateDevice,
    CAST(ISNULL(c.IsLegal, 0) AS BIT) AS IsLegal
FROM dbo.LegalConversionRequests r
INNER JOIN dbo.Customers c ON c.CustomerID = r.CustomerID
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
LEFT JOIN dbo.Users u1 ON u1.UserID = r.RequestedByUserID
LEFT JOIN dbo.Users u2 ON u2.UserID = r.ReviewedByUserID
WHERE (@Status IS NULL OR @Status = N'' OR r.Status = @Status)
ORDER BY
    CASE WHEN r.Status = N'Pending' THEN 0 ELSE 1 END,
    r.RequestedAt DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
""", new { Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(), Skip = skip, Take = take },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<int> CountAsync(string? status, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await using var connection = new SqlConnection(_cs);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
SELECT COUNT(1)
FROM dbo.LegalConversionRequests
WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status);
""", new { Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim() }, cancellationToken: ct));
    }

    public async Task<LegalConversionRequestDto?> ApproveAsync(
        int requestId,
        int reviewerUserId,
        string? reviewNote,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        await using var tx = connection.BeginTransaction();

        var req = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition("""
SELECT TOP 1 RequestId, CustomerID, Status, RequestNote, WeekDecisionId, WeekPaid, PaidPercent, RequestedByUserID
FROM dbo.LegalConversionRequests WITH (UPDLOCK, ROWLOCK)
WHERE RequestId = @RequestId;
""", new { RequestId = requestId }, tx, cancellationToken: ct));

        if (req == null)
            return null;
        if (!string.Equals((string)req.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("الطلب ليس بانتظار الموافقة.");

        int customerId = (int)req.CustomerID;
        int requestedBy = (int)req.RequestedByUserID;
        // Gateway monitor auth uses UserID=0 — Activities has FK to Users; resolve a real user.
        var effectiveReviewerId = await ResolveReviewerUserIdAsync(
            connection, tx, reviewerUserId, requestedBy, ct);
        _ = DeployMarker;

        var customerName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT ISNULL(CustomerName,N'') FROM dbo.Customers WHERE CustomerID=@Id",
            new { Id = customerId }, tx, cancellationToken: ct)) ?? "";

        await connection.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.Customers SET IsLegal = 1, UpdatedDate = GETDATE() WHERE CustomerID = @CustomerId;
IF OBJECT_ID(N'dbo.CustomerWeekPaymentSnapshot', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerWeekPaymentSnapshot', N'IsLegal') IS NOT NULL
    UPDATE dbo.CustomerWeekPaymentSnapshot SET IsLegal = 1 WHERE CustomerID = @CustomerId;
""", new { CustomerId = customerId }, tx, cancellationToken: ct));

        var weekStart = DateTime.Today.AddDays(-7);
        var weekEnd = DateTime.Today.AddDays(-1);
        var approvedDecisionId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.CustomerWeekDecisions
    (CustomerID, UserID, DecisionType, WeekPaid, AmountTotalSales, PaidPercent,
     WeekStartDate, WeekEndDate, SnoozeUntil, Note, CreatedDate)
VALUES
    (@CustomerId, @ReviewerId, N'قانونية', @WeekPaid, 0, @PaidPercent,
     @WeekStart, @WeekEnd, NULL, @Note, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);
""", new
        {
            CustomerId = customerId,
            ReviewerId = effectiveReviewerId,
            WeekPaid = (double?)req.WeekPaid ?? 0,
            PaidPercent = (double?)req.PaidPercent ?? 0,
            WeekStart = weekStart,
            WeekEnd = weekEnd,
            Note = string.IsNullOrWhiteSpace(reviewNote)
                ? $"موافقة على طلب قانونية #{requestId}"
                : reviewNote.Trim(),
        }, tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.DecisionNotifications (DecisionID, IsRead, CreatedDate)
VALUES (@DecisionId, 0, GETDATE());
""", new { DecisionId = approvedDecisionId }, tx, cancellationToken: ct));

        await connection.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.LegalConversionRequests SET
    Status = N'Approved',
    ReviewedByUserID = @ReviewerId,
    ReviewNote = @ReviewNote,
    ReviewedAt = SYSUTCDATETIME(),
    ApprovedDecisionId = @ApprovedDecisionId
WHERE RequestId = @RequestId;
""", new
        {
            RequestId = requestId,
            ReviewerId = effectiveReviewerId,
            ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim(),
            ApprovedDecisionId = approvedDecisionId,
        }, tx, cancellationToken: ct));

        await TryInsertActivityAsync(
            connection, tx, effectiveReviewerId,
            $"موافقة تحويل قانونية للزبون {customerName} (طلب #{requestId})", ct);

        tx.Commit();
        return await ListByIdAsync(requestId, ct);
    }

    public async Task<LegalConversionRequestDto?> RejectAsync(
        int requestId,
        int reviewerUserId,
        string? reviewNote,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        await using var tx = connection.BeginTransaction();

        var req = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition("""
SELECT TOP 1 RequestId, CustomerID, Status, RequestedByUserID
FROM dbo.LegalConversionRequests WITH (UPDLOCK, ROWLOCK)
WHERE RequestId = @RequestId;
""", new { RequestId = requestId }, tx, cancellationToken: ct));

        if (req == null)
            return null;
        if (!string.Equals((string)req.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("الطلب ليس بانتظار الموافقة.");

        int customerId = (int)req.CustomerID;
        int requestedBy = (int)req.RequestedByUserID;
        var effectiveReviewerId = await ResolveReviewerUserIdAsync(
            connection, tx, reviewerUserId, requestedBy, ct);

        var customerName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT ISNULL(CustomerName,N'') FROM dbo.Customers WHERE CustomerID=@Id",
            new { Id = customerId }, tx, cancellationToken: ct)) ?? "";

        await connection.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.LegalConversionRequests SET
    Status = N'Rejected',
    ReviewedByUserID = @ReviewerId,
    ReviewNote = @ReviewNote,
    ReviewedAt = SYSUTCDATETIME()
WHERE RequestId = @RequestId;
""", new
        {
            RequestId = requestId,
            ReviewerId = effectiveReviewerId,
            ReviewNote = string.IsNullOrWhiteSpace(reviewNote) ? null : reviewNote.Trim(),
        }, tx, cancellationToken: ct));

        await TryInsertActivityAsync(
            connection, tx, effectiveReviewerId,
            $"رفض طلب قانونية للزبون {customerName} (طلب #{requestId})", ct);

        tx.Commit();
        return await ListByIdAsync(requestId, ct);
    }

    private async Task<LegalConversionRequestDto?> ListByIdAsync(int requestId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_cs);
        return await connection.QueryFirstOrDefaultAsync<LegalConversionRequestDto>(new CommandDefinition("""
SELECT
    r.RequestId,
    r.CustomerID AS CustomerId,
    ISNULL(vc.CustomerName, c.CustomerName) AS CustomerName,
    ISNULL(vc.PhoneNumber, c.PhoneNumber) AS PhoneNumber,
    r.RequestedByUserID AS RequestedByUserId,
    u1.UserName AS RequestedByName,
    r.Status,
    r.RequestNote,
    r.WeekDecisionId,
    r.ApprovedDecisionId,
    r.ReviewedByUserID AS ReviewedByUserId,
    u2.UserName AS ReviewedByName,
    r.ReviewNote,
    r.RequestedAt,
    r.ReviewedAt,
    r.WeekPaid,
    r.PaidPercent,
    c.DelegateID AS DelegateId,
    d.DelegateName AS DelegateName,
    ISNULL(c.Address, N'') AS Address,
    ISNULL(c.Notes, N'') AS Notes,
    ISNULL(c.ShopName, N'') AS ShopName,
    CAST(NULL AS NVARCHAR(200)) AS SaleName,
    CAST(NULL AS NVARCHAR(200)) AS ReceiptName,
    vc.DateSaleDevice AS DateSaleDevice,
    CAST(ISNULL(vc.AmountTotalSales, 0) AS FLOAT) AS AmountTotalSales,
    CAST(ISNULL(vc.AmountDaySales, 0) AS FLOAT) AS AmountDaySales,
    CAST(ISNULL(vc.ReceiptsTotal, 0) AS FLOAT) AS ReceiptsTotal,
    CAST(ISNULL(vc.AmountRemaining, 0) AS FLOAT) AS AmountRemaining,
    ISNULL(vc.ItemsNames, N'') AS ItemsNames,
    vc.LastPaymentDate AS LastPaymentDate,
    CAST((
        SELECT TOP 1 ROUND(ISNULL(pay.AmountDenar, 0), -3)
        FROM dbo.View_ReceiptCustomerDate pay
        WHERE pay.CustomerID = c.CustomerID
        ORDER BY pay.PaymentDate DESC
    ) AS FLOAT) AS LastPaymentAmount,
    CAST(TRY_CONVERT(INT, vc.CountReceiptDevice) AS INT) AS CountReceiptDevice,
    CAST(ISNULL(vc.NumberOfDayPayment, 0) AS INT) AS NumberOfDayDevice,
    CAST(ISNULL(vc.ReceiptRateDevice, 0) AS INT) AS ReceiptRateDevice,
    CAST(ISNULL(c.IsLegal, 0) AS BIT) AS IsLegal
FROM dbo.LegalConversionRequests r
INNER JOIN dbo.Customers c ON c.CustomerID = r.CustomerID
LEFT JOIN dbo.View_Customers vc ON vc.CustomerID = c.CustomerID
LEFT JOIN dbo.Delegates d ON d.DelegateID = c.DelegateID
LEFT JOIN dbo.Users u1 ON u1.UserID = r.RequestedByUserID
LEFT JOIN dbo.Users u2 ON u2.UserID = r.ReviewedByUserID
WHERE r.RequestId = @RequestId;
""", new { RequestId = requestId }, cancellationToken: ct));
    }

    private static async Task<CustomerWeekDecisionGetDTO?> LoadDecisionDtoAsync(
        SqlConnection connection,
        int? decisionId,
        CancellationToken ct)
    {
        if (decisionId is null or <= 0)
            return null;
        return await connection.QueryFirstOrDefaultAsync<CustomerWeekDecisionGetDTO>(new CommandDefinition("""
SELECT
    D.DecisionID, D.CustomerID, D.UserID, D.DecisionType, D.WeekPaid, D.AmountTotalSales, D.PaidPercent,
    D.WeekStartDate, D.WeekEndDate, D.SnoozeUntil, D.Note, D.CreatedDate,
    C.CustomerName, C.PhoneNumber, U.UserName, U.UserType,
    ISNULL(C.IsLegal, 0) AS IsLegal, ISNULL(C.IsFakeSale, 0) AS IsFakeSale
FROM dbo.CustomerWeekDecisions D
INNER JOIN dbo.Customers C ON C.CustomerID = D.CustomerID
INNER JOIN dbo.Users U ON U.UserID = D.UserID
WHERE D.DecisionID = @DecisionId;
""", new { DecisionId = decisionId }, cancellationToken: ct));
    }

    private static CustomerWeekDecisionGetDTO BuildSyntheticDecision(
        int customerId, int userId, string? note, double? weekPaid, double? paidPercent) =>
        new()
        {
            CustomerID = customerId,
            UserID = userId,
            DecisionType = FinalDecisionType,
            Note = note,
            WeekPaid = weekPaid,
            PaidPercent = paidPercent,
            CreatedDate = DateTime.Now,
            IsLegal = false,
            IsFakeSale = false,
        };

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        SqlTransaction tx,
        string name,
        CancellationToken ct) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(@Name, N'U') IS NULL THEN 0 ELSE 1 END",
            new { Name = $"dbo.{name}" }, tx, cancellationToken: ct)) == 1;

    /// <summary>
    /// Monitor via SalesGateway authenticates as UserID=0. Prefer a real Users row
    /// so Activities FK and CustomerWeekDecisions stay valid.
    /// </summary>
    private static async Task<int> ResolveReviewerUserIdAsync(
        SqlConnection connection,
        SqlTransaction tx,
        int reviewerUserId,
        int requestedByUserId,
        CancellationToken ct)
    {
        if (reviewerUserId > 0 && await UserExistsAsync(connection, tx, reviewerUserId, ct))
            return reviewerUserId;

        if (requestedByUserId > 0 && await UserExistsAsync(connection, tx, requestedByUserId, ct))
            return requestedByUserId;

        var monitorId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
SELECT TOP 1 UserID FROM dbo.Users
WHERE UserType IN (N'المراقب', N'Monitor')
ORDER BY UserID;
""", transaction: tx, cancellationToken: ct));
        if (monitorId is > 0)
            return monitorId.Value;

        var anyId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 UserID FROM dbo.Users ORDER BY UserID;",
            transaction: tx, cancellationToken: ct));
        if (anyId is > 0)
            return anyId.Value;

        throw new InvalidOperationException("لا يوجد مستخدم صالح لتسجيل موافقة التحويل القانونية.");
    }

    private static async Task<bool> UserExistsAsync(
        SqlConnection connection,
        SqlTransaction tx,
        int userId,
        CancellationToken ct) =>
        await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Users WHERE UserID=@Id) THEN 1 ELSE 0 END",
            new { Id = userId }, tx, cancellationToken: ct)) == 1;

    private static async Task TryInsertActivityAsync(
        SqlConnection connection,
        SqlTransaction tx,
        int userId,
        string description,
        CancellationToken ct)
    {
        if (userId <= 0)
            return;
        if (!await TableExistsAsync(connection, tx, "Activities", ct))
            return;
        if (!await UserExistsAsync(connection, tx, userId, ct))
            return;

        await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
VALUES (@UserId, @Desc, GETUTCDATE(), 'false', NEWID());
""", new { UserId = userId, Desc = description }, tx, cancellationToken: ct));
    }
}
