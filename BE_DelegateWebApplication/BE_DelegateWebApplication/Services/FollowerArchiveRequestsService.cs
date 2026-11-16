using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

/// <summary>
/// Archive request lifecycle: Requested (follower) → ToCommittee → WithCommittee (archive manager → committee member,
/// shown to the follower as Requested) → Sent (committee member) → Received (follower).
/// </summary>
public static class FollowerArchiveStatuses
{
    public const string Requested = "Requested";
    public const string Sent = "Sent";
    public const string Received = "Received";
}

public sealed class FollowerFinishedCustomerDto
{
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public int? ListId { get; set; }
    public string? ListName { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? RequestId { get; set; }
    public string? RequestStatus { get; set; }
    /// <summary>Remaining debt (IQD, rounded to thousands).</summary>
    public decimal Remaining { get; set; }
    public bool IsFinished { get; set; }
}

public sealed class FollowerArchiveRequestDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public int? ListId { get; set; }
    public string? ListName { get; set; }
    public string Status { get; set; } = FollowerArchiveStatuses.Requested;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? SentByName { get; set; }
    public string? SendNote { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public string? FollowerNote { get; set; }
}

public sealed class FollowerArchiveException : Exception
{
    public FollowerArchiveException(int statusCode, string message) : base(message) => StatusCode = statusCode;
    public int StatusCode { get; }
}

public interface IFollowerArchiveRequestsService
{
    Task<IReadOnlyList<FollowerFinishedCustomerDto>> ListFinishedAsync(int followerUserId, string? search, CancellationToken ct, bool onlyFinished = true);
    Task<IReadOnlyList<FollowerArchiveRequestDto>> ListMineAsync(int followerUserId, CancellationToken ct);
    Task<FollowerArchiveRequestDto> CreateAsync(int followerUserId, string? followerName, int customerId, string? note, CancellationToken ct);
    Task<FollowerArchiveRequestDto> MarkReceivedAsync(int followerUserId, int requestId, CancellationToken ct);
}

/// <summary>
/// Follower side of the finished-customer paperwork handover. Lives in the branch DB,
/// shared with BE_Company (accountant side) which runs the same idempotent schema.
/// </summary>
public sealed class FollowerArchiveRequestsService : IFollowerArchiveRequestsService
{
    public const int MaxFinishedRows = 500;
    public const int MaxNoteLength = 500;
    public const int MinUnfinishedNoteLength = 3;

    internal const string SchemaSql = @"
IF OBJECT_ID(N'dbo.FollowerArchiveRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerArchiveRequests (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerArchiveRequests PRIMARY KEY,
        CustomerId INT NOT NULL,
        CustomerName NVARCHAR(250) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        ListId INT NULL,
        ListName NVARCHAR(250) NULL,
        FollowerUserId INT NOT NULL,
        FollowerName NVARCHAR(250) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_FollowerArchiveRequests_Status DEFAULT (N'Requested'),
        RequestedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerArchiveRequests_Requested DEFAULT (SYSUTCDATETIME()),
        SentAtUtc DATETIME2 NULL,
        SentByUserId INT NULL,
        SentByName NVARCHAR(250) NULL,
        SendNote NVARCHAR(500) NULL,
        ReceivedAtUtc DATETIME2 NULL
    );
    CREATE UNIQUE INDEX UX_FollowerArchiveRequests_Customer ON dbo.FollowerArchiveRequests(CustomerId);
    CREATE INDEX IX_FollowerArchiveRequests_Follower ON dbo.FollowerArchiveRequests(FollowerUserId, Status);
    CREATE INDEX IX_FollowerArchiveRequests_Status ON dbo.FollowerArchiveRequests(Status, RequestedAtUtc);
END;
IF COL_LENGTH(N'dbo.FollowerArchiveRequests', N'FollowerNote') IS NULL
    ALTER TABLE dbo.FollowerArchiveRequests ADD FollowerNote NVARCHAR(500) NULL;";

    /// <summary>Finished = had sales and nothing remains; finish date = last payment.</summary>
    internal const string FinishedCustomersSql = @"
SELECT TOP (@Take)
    C.CustomerID AS CustomerId,
    C.CustomerName,
    C.PhoneNumber,
    C.DelegateID AS ListId,
    LTRIM(RTRIM(ISNULL(D.DelegateName, N''))) AS ListName,
    P.LastPaymentDate AS FinishedAt,
    R.Id AS RequestId,
    CASE WHEN R.Status IN (N'ToCommittee', N'WithCommittee') THEN N'Requested' ELSE R.Status END AS RequestStatus,
    ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) AS Remaining,
    CAST(CASE WHEN ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) <= 0 THEN 1 ELSE 0 END AS BIT) AS IsFinished
FROM dbo.Customers C
LEFT JOIN dbo.Delegates D ON D.DelegateID = C.DelegateID
OUTER APPLY (
    SELECT ROUND(ISNULL(SUM(V.AmountTotalSalesDenar), 0), -3) AS AmountTotalSales
    FROM dbo.View_CustomersSalesDelegate V
    WHERE V.CustomerID = C.CustomerID
) S
OUTER APPLY (
    SELECT MAX(V.PaymentDate) AS LastPaymentDate,
           ROUND(ISNULL(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal
    FROM dbo.View_CustomersPaymentsDelegate V
    WHERE V.CustomerID = C.CustomerID
) P
LEFT JOIN dbo.FollowerArchiveRequests R ON R.CustomerId = C.CustomerID
WHERE C.DelegateID IN (SELECT ful.ListId FROM dbo.FollowerUserLists ful WHERE ful.UserId = @UserId)
  AND ISNULL(C.CustomerState, 1) = 1
  AND ISNULL(S.AmountTotalSales, 0) > 0
  AND (@OnlyFinished = 0 OR ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) <= 0)
  AND (@CustomerId IS NULL OR C.CustomerID = @CustomerId)
  AND (@Search IS NULL OR C.CustomerName LIKE @Search ESCAPE '!' OR C.PhoneNumber LIKE @Search ESCAPE '!')
ORDER BY CASE WHEN ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) <= 0 THEN 0 ELSE 1 END,
         P.LastPaymentDate DESC, C.CustomerName;";

    private const string RequestColumns = @"
    Id, CustomerId, CustomerName, PhoneNumber, ListId, ListName,
    CASE WHEN Status IN (N'ToCommittee', N'WithCommittee') THEN N'Requested' ELSE Status END AS Status,
    RequestedAtUtc, SentAtUtc, SentByName, SendNote, ReceivedAtUtc, FollowerNote";

    private readonly string _cs;

    public FollowerArchiveRequestsService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task<IReadOnlyList<FollowerFinishedCustomerDto>> ListFinishedAsync(
        int followerUserId, string? search, CancellationToken ct, bool onlyFinished = true)
    {
        var q = (search ?? string.Empty).Trim();
        var like = q.Length == 0
            ? null
            : "%" + q.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_").Replace("[", "![") + "%";

        await using var c = await OpenAsync(ct);
        if (!await HasFollowerListsAsync(c, ct))
        {
            return [];
        }

        var rows = await c.QueryAsync<FollowerFinishedCustomerDto>(new CommandDefinition(
            FinishedCustomersSql,
            new { UserId = followerUserId, Take = MaxFinishedRows, CustomerId = (int?)null, Search = like, OnlyFinished = onlyFinished },
            commandTimeout: 60,
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<FollowerArchiveRequestDto>> ListMineAsync(int followerUserId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        var rows = await c.QueryAsync<FollowerArchiveRequestDto>(new CommandDefinition($@"
SELECT TOP (300) {RequestColumns}
FROM dbo.FollowerArchiveRequests
WHERE FollowerUserId = @UserId
ORDER BY CASE WHEN Status = N'Sent' THEN 0 WHEN Status = N'Received' THEN 2 ELSE 1 END,
         COALESCE(ReceivedAtUtc, SentAtUtc, RequestedAtUtc) DESC;",
            new { UserId = followerUserId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<FollowerArchiveRequestDto> CreateAsync(
        int followerUserId, string? followerName, int customerId, string? note, CancellationToken ct)
    {
        if (customerId <= 0)
        {
            throw new FollowerArchiveException(StatusCodes.Status400BadRequest, "اختر الزبون");
        }

        var cleanNote = (note ?? string.Empty).Trim();
        if (cleanNote.Length > MaxNoteLength)
        {
            throw new FollowerArchiveException(StatusCodes.Status400BadRequest, $"الحد الأقصى للملاحظة {MaxNoteLength} حرف");
        }

        await using var c = await OpenAsync(ct);
        if (!await HasFollowerListsAsync(c, ct))
        {
            throw new FollowerArchiveException(StatusCodes.Status403Forbidden, "لا توجد قوائم مسندة لحساب المتابع");
        }

        var customer = await c.QueryFirstOrDefaultAsync<FollowerFinishedCustomerDto>(new CommandDefinition(
            FinishedCustomersSql,
            new { UserId = followerUserId, Take = 1, CustomerId = (int?)customerId, Search = (string?)null, OnlyFinished = false },
            commandTimeout: 30,
            cancellationToken: ct));
        if (customer == null)
        {
            throw new FollowerArchiveException(StatusCodes.Status403Forbidden, "الزبون ليس ضمن قوائمك");
        }

        if (!customer.IsFinished && cleanNote.Length < MinUnfinishedNoteLength)
        {
            throw new FollowerArchiveException(StatusCodes.Status400BadRequest, "الزبون غير منتهٍ — اكتب سبب طلب الأرشيف في الملاحظة");
        }

        if (customer.RequestId is > 0)
        {
            throw new FollowerArchiveException(StatusCodes.Status409Conflict, "تم طلب أرشيف هذا الزبون مسبقاً");
        }

        try
        {
            var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.FollowerArchiveRequests
    (CustomerId, CustomerName, PhoneNumber, ListId, ListName, FollowerUserId, FollowerName, Status, RequestedAtUtc, FollowerNote)
OUTPUT INSERTED.Id
VALUES
    (@CustomerId, @CustomerName, @PhoneNumber, @ListId, @ListName, @FollowerUserId, @FollowerName, N'Requested', SYSUTCDATETIME(), @FollowerNote);",
                new
                {
                    customer.CustomerId,
                    customer.CustomerName,
                    customer.PhoneNumber,
                    customer.ListId,
                    customer.ListName,
                    FollowerUserId = followerUserId,
                    FollowerName = string.IsNullOrWhiteSpace(followerName) ? null : followerName.Trim(),
                    FollowerNote = cleanNote.Length == 0 ? null : cleanNote,
                },
                cancellationToken: ct));
            return await LoadAsync(c, id, ct);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            throw new FollowerArchiveException(StatusCodes.Status409Conflict, "تم طلب أرشيف هذا الزبون مسبقاً");
        }
    }

    public async Task<FollowerArchiveRequestDto> MarkReceivedAsync(int followerUserId, int requestId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        var affected = await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerArchiveRequests
SET Status = N'Received', ReceivedAtUtc = SYSUTCDATETIME()
WHERE Id = @Id AND FollowerUserId = @UserId AND Status = N'Sent';",
            new { Id = requestId, UserId = followerUserId }, cancellationToken: ct));
        if (affected == 0)
        {
            var row = await c.QueryFirstOrDefaultAsync<FollowerArchiveRequestDto>(new CommandDefinition(
                $"SELECT {RequestColumns} FROM dbo.FollowerArchiveRequests WHERE Id = @Id AND FollowerUserId = @UserId;",
                new { Id = requestId, UserId = followerUserId }, cancellationToken: ct));
            if (row == null)
            {
                throw new FollowerArchiveException(StatusCodes.Status404NotFound, "الطلب غير موجود");
            }

            if (row.Status == FollowerArchiveStatuses.Received)
            {
                return row;
            }

            throw new FollowerArchiveException(StatusCodes.Status409Conflict, "لم يُرسل الأرشيف بعد");
        }

        return await LoadAsync(c, requestId, ct);
    }

    private static async Task<FollowerArchiveRequestDto> LoadAsync(SqlConnection c, int id, CancellationToken ct) =>
        await c.QueryFirstAsync<FollowerArchiveRequestDto>(new CommandDefinition(
            $"SELECT {RequestColumns} FROM dbo.FollowerArchiveRequests WHERE Id = @Id;",
            new { Id = id }, cancellationToken: ct));

    private static async Task<bool> HasFollowerListsAsync(SqlConnection c, CancellationToken ct) =>
        await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerUserLists', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct)) == 1;

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var c = new SqlConnection(_cs);
        await c.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        return c;
    }
}
