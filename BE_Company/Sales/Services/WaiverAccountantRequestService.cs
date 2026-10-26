using System.Globalization;
using BE_Company.Sales.Authorization;
using BE_Company.Services;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class WaiverAccountantRequestDto
{
    public int RequestId { get; set; }
    public Guid WaiverId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public string? CustomerPhone { get; set; }
    public double ApprovedAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? DecidedByUserName { get; set; }
    public string? DecidedByDisplayName { get; set; }
    public string? RequestNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? DecisionId { get; set; }
}

public interface IWaiverAccountantRequestService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<WaiverAccountantRequestDto> QueueFromDelegatedManagerAsync(
        WaiverApplyRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<WaiverAccountantRequestDto>> ListAsync(
        string? status, int take, int skip, CancellationToken ct = default);
    Task<WaiverAccountantRequestDto?> ApproveAsync(
        int requestId, int reviewerUserId, string? reviewerName, CancellationToken ct = default);
}

/// <summary>
/// After DM approves a waiver amount, queue for main accountant.
/// Accountant approve applies sale-price discount (not a payment) + customer note.
/// </summary>
public sealed class WaiverAccountantRequestService : IWaiverAccountantRequestService
{
    public const string PendingStatus = "Pending";
    public const string AppliedStatus = "Applied";

    private readonly SalesDevelopmentGuard _guard;
    private readonly ISharedCustomerNotesService _notes;

    public WaiverAccountantRequestService(
        SalesDevelopmentGuard guard,
        ISharedCustomerNotesService notes)
    {
        _guard = guard;
        _notes = notes;
    }

    private string RequireCs() =>
        _guard.GetSalesConnectionString()
        ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(RequireCs());
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.WaiverAccountantRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WaiverAccountantRequests (
        RequestId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WaiverAccountantRequests PRIMARY KEY,
        WaiverId UNIQUEIDENTIFIER NOT NULL,
        CustomerId INT NOT NULL,
        CustomerName NVARCHAR(256) NULL,
        CustomerPhone NVARCHAR(64) NULL,
        ApprovedAmount FLOAT NOT NULL,
        Status NVARCHAR(32) NOT NULL CONSTRAINT DF_WAR_Status DEFAULT (N'Pending'),
        DecidedByUserName NVARCHAR(128) NULL,
        DecidedByDisplayName NVARCHAR(256) NULL,
        RequestNote NVARCHAR(MAX) NULL,
        RequestedAt DATETIME2 NOT NULL CONSTRAINT DF_WAR_Requested DEFAULT (SYSUTCDATETIME()),
        ReviewedByUserId INT NULL,
        ReviewedByName NVARCHAR(200) NULL,
        ReviewedAt DATETIME2 NULL,
        DecisionId INT NULL,
        CONSTRAINT UX_WAR_WaiverId UNIQUE (WaiverId)
    );
END
""", cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(WaiverApplyRules.ClaimSql, cancellationToken: ct));
    }

    public async Task<WaiverAccountantRequestDto> QueueFromDelegatedManagerAsync(
        WaiverApplyRequest request, CancellationToken ct = default)
    {
        if (!WaiverApplyRules.IsValidWaiverId(request.WaiverId))
        {
            throw new SalesCompleteException(StatusCodes.Status400BadRequest, "معرّف التنازل مطلوب");
        }

        if (request.CustomerId <= 0 || request.Amount <= 0)
        {
            throw new SalesCompleteException(StatusCodes.Status400BadRequest, "بيانات طلب التنازل غير مكتملة");
        }

        await EnsureSchemaAsync(ct);
        await using var connection = new SqlConnection(RequireCs());
        await connection.OpenAsync(ct);

        var existing = await connection.QueryFirstOrDefaultAsync<WaiverAccountantRequestDto>(new CommandDefinition("""
SELECT RequestId, WaiverId, CustomerId, ISNULL(CustomerName, N'') AS CustomerName, CustomerPhone,
       ApprovedAmount, Status, DecidedByUserName, DecidedByDisplayName, RequestNote,
       RequestedAt, ReviewedByUserId, ReviewedByName, ReviewedAt, DecisionId
FROM dbo.WaiverAccountantRequests WHERE WaiverId = @WaiverId;
""", new { WaiverId = request.WaiverId!.Value }, cancellationToken: ct));
        if (existing != null)
        {
            return existing;
        }

        var customerName = (request.CustomerName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(customerName))
        {
            customerName = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT ISNULL(CustomerName, N'') FROM dbo.Customers WHERE CustomerID=@CustomerId",
                new { request.CustomerId }, cancellationToken: ct)) ?? "";
        }

        string? phone = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT PhoneNumber FROM dbo.Customers WHERE CustomerID=@CustomerId",
            new { request.CustomerId }, cancellationToken: ct));

        var amountText = request.Amount.ToString("#,##0.##", CultureInfo.InvariantCulture);
        var requestNote = string.IsNullOrWhiteSpace(request.Note)
            ? $"طلب تنازل بمبلغ {amountText} بانتظار المحاسب الرئيسي"
            : request.Note.Trim();

        await using var tx = await connection.BeginTransactionAsync(ct);
        try
        {
            var requestId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.WaiverAccountantRequests
    (WaiverId, CustomerId, CustomerName, CustomerPhone, ApprovedAmount, Status,
     DecidedByUserName, DecidedByDisplayName, RequestNote, RequestedAt)
VALUES
    (@WaiverId, @CustomerId, @CustomerName, @CustomerPhone, @ApprovedAmount, N'Pending',
     @DecidedByUserName, @DecidedByDisplayName, @RequestNote, SYSUTCDATETIME());
SELECT CAST(SCOPE_IDENTITY() AS INT);
""", new
            {
                WaiverId = request.WaiverId.Value,
                request.CustomerId,
                CustomerName = customerName,
                CustomerPhone = phone,
                ApprovedAmount = request.Amount,
                DecidedByUserName = request.DecidedByUserName,
                DecidedByDisplayName = request.DecidedByDisplayName,
                RequestNote = requestNote
            }, tx, cancellationToken: ct));

            // Hide from WeakWeek board until accountant applies (or forever once تنازل is written).
            var userId = await ResolveBranchUserIdAsync(connection, tx, request, ct);
            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Saturday);
            if (weekStart > today) weekStart = weekStart.AddDays(-7);
            var weekEnd = weekStart.AddDays(6);

            await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.CustomerWeekDecisions
    (CustomerID, UserID, DecisionType, WeekPaid, AmountTotalSales, PaidPercent,
     WeekStartDate, WeekEndDate, SnoozeUntil, Note, CreatedDate)
VALUES
    (@CustomerId, @UserId, @DecisionType, 0, 0, 0,
     @WeekStart, @WeekEnd, NULL, @Note, GETDATE());
""", new
            {
                request.CustomerId,
                UserId = userId,
                DecisionType = WaiverDiscountRules.PendingDecisionType,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                Note = requestNote
            }, tx, cancellationToken: ct));

            await tx.CommitAsync(ct);

            return (await connection.QueryFirstAsync<WaiverAccountantRequestDto>(new CommandDefinition("""
SELECT RequestId, WaiverId, CustomerId, ISNULL(CustomerName, N'') AS CustomerName, CustomerPhone,
       ApprovedAmount, Status, DecidedByUserName, DecidedByDisplayName, RequestNote,
       RequestedAt, ReviewedByUserId, ReviewedByName, ReviewedAt, DecisionId
FROM dbo.WaiverAccountantRequests WHERE RequestId = @RequestId;
""", new { RequestId = requestId }, cancellationToken: ct)));
        }
        catch (Exception ex) when (WaiverApplyRules.IsUniqueKeyViolation(ex))
        {
            await tx.RollbackAsync(ct);
            return (await connection.QueryFirstAsync<WaiverAccountantRequestDto>(new CommandDefinition("""
SELECT RequestId, WaiverId, CustomerId, ISNULL(CustomerName, N'') AS CustomerName, CustomerPhone,
       ApprovedAmount, Status, DecidedByUserName, DecidedByDisplayName, RequestNote,
       RequestedAt, ReviewedByUserId, ReviewedByName, ReviewedAt, DecisionId
FROM dbo.WaiverAccountantRequests WHERE WaiverId = @WaiverId;
""", new { WaiverId = request.WaiverId!.Value }, cancellationToken: ct)));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<WaiverAccountantRequestDto>> ListAsync(
        string? status, int take, int skip, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        take = Math.Clamp(take <= 0 ? 100 : take, 1, 500);
        skip = Math.Max(0, skip);
        await using var connection = new SqlConnection(RequireCs());
        var rows = await connection.QueryAsync<WaiverAccountantRequestDto>(new CommandDefinition("""
SELECT RequestId, WaiverId, CustomerId, ISNULL(CustomerName, N'') AS CustomerName, CustomerPhone,
       ApprovedAmount, Status, DecidedByUserName, DecidedByDisplayName, RequestNote,
       RequestedAt, ReviewedByUserId, ReviewedByName, ReviewedAt, DecisionId
FROM dbo.WaiverAccountantRequests
WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
ORDER BY RequestedAt DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
""", new { Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(), Skip = skip, Take = take },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<WaiverAccountantRequestDto?> ApproveAsync(
        int requestId, int reviewerUserId, string? reviewerName, CancellationToken ct = default)
    {
        if (requestId <= 0 || reviewerUserId <= 0)
        {
            throw new SalesCompleteException(StatusCodes.Status400BadRequest, "بيانات الموافقة غير مكتملة");
        }

        await EnsureSchemaAsync(ct);
        await using var connection = new SqlConnection(RequireCs());
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        try
        {
            var row = await connection.QueryFirstOrDefaultAsync<WaiverAccountantRequestDto>(new CommandDefinition("""
SELECT RequestId, WaiverId, CustomerId, ISNULL(CustomerName, N'') AS CustomerName, CustomerPhone,
       ApprovedAmount, Status, DecidedByUserName, DecidedByDisplayName, RequestNote,
       RequestedAt, ReviewedByUserId, ReviewedByName, ReviewedAt, DecisionId
FROM dbo.WaiverAccountantRequests WITH (UPDLOCK, ROWLOCK)
WHERE RequestId = @RequestId;
""", new { RequestId = requestId }, tx, cancellationToken: ct));

            if (row is null)
            {
                return null;
            }

            if (!string.Equals(row.Status, PendingStatus, StringComparison.Ordinal))
            {
                throw new SalesCompleteException(StatusCodes.Status409Conflict, "تمت معالجة هذا الطلب مسبقاً");
            }

            // Idempotency claim on SalesWaiverApplications
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    WaiverApplyRules.TryClaimSql,
                    new
                    {
                        WaiverId = row.WaiverId,
                        row.CustomerId,
                        Amount = row.ApprovedAmount,
                        ActorEmployeeId = reviewerUserId
                    },
                    tx,
                    cancellationToken: ct));
            }
            catch (Exception ex) when (WaiverApplyRules.IsUniqueKeyViolation(ex))
            {
                await tx.RollbackAsync(ct);
                return await connection.QueryFirstOrDefaultAsync<WaiverAccountantRequestDto>(new CommandDefinition("""
SELECT RequestId, WaiverId, CustomerId, ISNULL(CustomerName, N'') AS CustomerName, CustomerPhone,
       ApprovedAmount, Status, DecidedByUserName, DecidedByDisplayName, RequestNote,
       RequestedAt, ReviewedByUserId, ReviewedByName, ReviewedAt, DecisionId
FROM dbo.WaiverAccountantRequests WHERE RequestId = @RequestId;
""", new { RequestId = requestId }, cancellationToken: ct));
            }

            var (saleId, saleTotal, paidTotal) = await LoadSaleTotalsAsync(connection, tx, row.CustomerId, ct);
            var discount = WaiverDiscountRules.CapDiscount(saleTotal, paidTotal, row.ApprovedAmount);
            if (discount <= 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status400BadRequest,
                    "لا يمكن تطبيق خصم: لا يوجد متبقي على سعر البيع");
            }

            if (saleId > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.CustomersSales
SET AmountTotalSales = AmountTotalSales - (@Discount / 1448.0)
WHERE CustomerSaleID = @SaleId;
""", new { SaleId = saleId, Discount = discount }, tx, cancellationToken: ct));
            }

            var iraqToday = DateTime.UtcNow.AddHours(3).Date;
            var noteText = WaiverDiscountRules.BuildCustomerNote(row.CustomerName, discount, iraqToday);

            var today = DateTime.Today;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Saturday);
            if (weekStart > today) weekStart = weekStart.AddDays(-7);
            var weekEnd = weekStart.AddDays(6);
            var paidPercent = saleTotal > 0 ? Math.Round(discount * 100.0 / saleTotal, 2) : 0;

            var decisionId = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
INSERT INTO dbo.CustomerWeekDecisions
    (CustomerID, UserID, DecisionType, WeekPaid, AmountTotalSales, PaidPercent,
     WeekStartDate, WeekEndDate, SnoozeUntil, Note, CreatedDate)
VALUES
    (@CustomerId, @UserId, @DecisionType, @WeekPaid, @AmountTotal, @PaidPercent,
     @WeekStart, @WeekEnd, NULL, @Note, GETDATE());
SELECT CAST(SCOPE_IDENTITY() AS INT);
""", new
            {
                row.CustomerId,
                UserId = reviewerUserId,
                DecisionType = WaiverDiscountRules.FinalDecisionType,
                WeekPaid = discount,
                AmountTotal = saleTotal,
                PaidPercent = paidPercent,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                Note = noteText
            }, tx, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.DecisionNotifications (DecisionID, IsRead, CreatedDate)
VALUES (@DecisionId, 0, GETDATE());
""", new { DecisionId = decisionId }, tx, cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(
                WaiverApplyRules.CompleteDiscountClaimSql,
                new { WaiverId = row.WaiverId, DecisionId = decisionId },
                tx,
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.WaiverAccountantRequests
SET Status = N'Applied',
    ReviewedByUserId = @ReviewerUserId,
    ReviewedByName = @ReviewedByName,
    ReviewedAt = SYSUTCDATETIME(),
    DecisionId = @DecisionId
WHERE RequestId = @RequestId AND Status = N'Pending';
""", new
            {
                RequestId = requestId,
                ReviewerUserId = reviewerUserId,
                ReviewedByName = string.IsNullOrWhiteSpace(reviewerName) ? "محاسب رئيسي" : reviewerName.Trim(),
                DecisionId = decisionId
            }, tx, cancellationToken: ct));

            await tx.CommitAsync(ct);

            // Customer note outside TX (own connection) — best-effort after financial commit.
            try
            {
                await _notes.AddAsync(row.CustomerId, noteText, reviewerUserId, reviewerName, ct);
            }
            catch
            {
                // Financial apply already committed; note failure must not roll back discount.
            }

            return await connection.QueryFirstAsync<WaiverAccountantRequestDto>(new CommandDefinition("""
SELECT RequestId, WaiverId, CustomerId, ISNULL(CustomerName, N'') AS CustomerName, CustomerPhone,
       ApprovedAmount, Status, DecidedByUserName, DecidedByDisplayName, RequestNote,
       RequestedAt, ReviewedByUserId, ReviewedByName, ReviewedAt, DecisionId
FROM dbo.WaiverAccountantRequests WHERE RequestId = @RequestId;
""", new { RequestId = requestId }, cancellationToken: ct));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task<(int SaleId, double SaleTotal, double PaidTotal)> LoadSaleTotalsAsync(
        SqlConnection connection, System.Data.Common.DbTransaction tx, int customerId, CancellationToken ct)
    {
        double saleTotal = 0;
        double paidTotal = 0;
        int saleId = 0;

        try
        {
            var row = await connection.QueryFirstOrDefaultAsync<(int SaleId, double SaleTotal)>(new CommandDefinition("""
SELECT TOP 1 CustomerSaleID AS SaleId, ISNULL(AmountTotalSales, 0) * 1448.0 AS SaleTotal
FROM dbo.CustomersSales
WHERE CustomerID = @CustomerId AND ISNULL(CustomerSaleState, 1) = 1
ORDER BY CustomerSaleID DESC;
""", new { CustomerId = customerId }, tx, cancellationToken: ct));
            saleId = row.SaleId;
            saleTotal = row.SaleTotal;
        }
        catch (SqlException)
        {
            saleTotal = 0;
        }

        if (saleTotal <= 0)
        {
            try
            {
                saleTotal = await connection.ExecuteScalarAsync<double?>(new CommandDefinition("""
SELECT ISNULL(SUM(ISNULL(AmountTotalSalesDenar, 0)), 0)
FROM dbo.View_CustomersSales WHERE CustomerID = @CustomerId;
""", new { CustomerId = customerId }, tx, cancellationToken: ct)) ?? 0;
            }
            catch (SqlException)
            {
                saleTotal = 0;
            }
        }

        try
        {
            paidTotal = await connection.ExecuteScalarAsync<double?>(new CommandDefinition("""
SELECT ISNULL(SUM(ISNULL(AmountDenar, 0)), 0)
FROM dbo.View_CustomersPaymentsDelegate WHERE CustomerID = @CustomerId;
""", new { CustomerId = customerId }, tx, cancellationToken: ct)) ?? 0;
        }
        catch (SqlException)
        {
            try
            {
                paidTotal = await connection.ExecuteScalarAsync<double?>(new CommandDefinition("""
SELECT ISNULL(SUM(ISNULL(PaymentAmount, 0)) * 1448.0, 0)
FROM dbo.CustomersPayments WHERE CustomerID = @CustomerId;
""", new { CustomerId = customerId }, tx, cancellationToken: ct)) ?? 0;
            }
            catch (SqlException)
            {
                paidTotal = 0;
            }
        }

        return (saleId, saleTotal, paidTotal);
    }

    private static async Task<int> ResolveBranchUserIdAsync(
        SqlConnection connection,
        System.Data.Common.DbTransaction tx,
        WaiverApplyRequest request,
        CancellationToken ct)
    {
        int? userId = null;
        if (!string.IsNullOrWhiteSpace(request.DecidedByUserName))
        {
            userId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
SELECT TOP 1 UserID FROM dbo.Users
WHERE UserName = @UserName AND ISNULL(UserState, 1) = 1 ORDER BY UserID;
""", new { UserName = request.DecidedByUserName.Trim() }, tx, cancellationToken: ct));
        }

        if (userId is null or <= 0)
        {
            userId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
SELECT TOP 1 UserID FROM dbo.Customers WHERE CustomerID = @CustomerId AND UserID IS NOT NULL;
""", new { request.CustomerId }, tx, cancellationToken: ct));
        }

        if (userId is null or <= 0)
        {
            userId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("""
SELECT TOP 1 UserID FROM dbo.Users
WHERE ISNULL(UserState, 1) = 1
  AND (UserType LIKE N'%محاسب%' OR UserType LIKE N'%مدير%')
ORDER BY UserID;
""", tx, cancellationToken: ct));
        }

        if (userId is null or <= 0)
        {
            throw new SalesCompleteException(StatusCodes.Status502BadGateway, "تعذر تحديد مستخدم الفرع");
        }

        return userId.Value;
    }
}
