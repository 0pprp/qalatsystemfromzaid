using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class ArchiveRequestRowDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public int? ListId { get; set; }
    public string? ListName { get; set; }
    public int FollowerUserId { get; set; }
    public string? FollowerName { get; set; }
    public string Status { get; set; } = "Requested";
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? SentByName { get; set; }
    public string? SendNote { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
    public string? CommitteeUser { get; set; }
    public DateTime? AssignedAtUtc { get; set; }
    /// <summary>Follower's reason/note (mandatory when the customer was not finished).</summary>
    public string? FollowerNote { get; set; }
    /// <summary>Current remaining debt (IQD, rounded to thousands); 0 or less = finished.</summary>
    public decimal Remaining { get; set; }
}

public sealed class ArchiveRequestsBoardDto
{
    public List<ArchiveRequestRowDto> Requested { get; set; } = [];
    /// <summary>Assigned to a committee member (ToCommittee or WithCommittee).</summary>
    public List<ArchiveRequestRowDto> AtCommittee { get; set; } = [];
    public List<ArchiveRequestRowDto> Sent { get; set; } = [];
    public List<ArchiveRequestRowDto> Received { get; set; } = [];
}

public sealed class FinishedCustomerRowDto
{
    public int FollowerUserId { get; set; }
    public string? FollowerName { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ListName { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? RequestStatus { get; set; }
}

public sealed class FinishedFollowerGroupDto
{
    public int FollowerUserId { get; set; }
    public string? FollowerName { get; set; }
    public int Total { get; set; }
    public int Requested { get; set; }
    public List<FinishedCustomerRowDto> Customers { get; set; } = [];
}

public sealed class ArchiveRequestException : Exception
{
    public ArchiveRequestException(int statusCode, string message) : base(message) => StatusCode = statusCode;
    public int StatusCode { get; }
}

public interface IFollowerArchiveRequestsService
{
    Task<ArchiveRequestsBoardDto> BoardAsync(CancellationToken ct);
    Task<ArchiveRequestRowDto> AssignAsync(int requestId, string committeeUser, string? actorName, string? note, CancellationToken ct);
    Task<IReadOnlyList<ArchiveRequestRowDto>> CommitteeListAsync(string committeeUser, CancellationToken ct);
    Task<ArchiveRequestRowDto> CommitteeAcceptAsync(string committeeUser, int requestId, CancellationToken ct);
    Task<ArchiveRequestRowDto> CommitteeGiveAsync(string committeeUser, int requestId, CancellationToken ct);
    Task<IReadOnlyList<FinishedFollowerGroupDto>> FinishedByFollowerAsync(int days, CancellationToken ct);
}

/// <summary>
/// Lifecycle: Requested (follower) → ToCommittee (archive manager picks a committee member) →
/// WithCommittee (member accepted) → Sent (member handed it to the follower) → Received (follower).
/// Archive-manager / committee side of follower archive requests (dbo.FollowerArchiveRequests in the branch DB).
/// The follower app writes requests through BE_DelegateWebApplication against the same table.
/// </summary>
public sealed class FollowerArchiveRequestsService : IFollowerArchiveRequestsService
{
    public const int ReceivedWindowDays = 30;
    public const int MaxNoteLength = 500;

    // Keep in sync with BE_DelegateWebApplication FollowerArchiveRequestsService.SchemaSql.
    private const string SchemaSql = @"
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
IF COL_LENGTH(N'dbo.FollowerArchiveRequests', N'CommitteeUser') IS NULL
    ALTER TABLE dbo.FollowerArchiveRequests ADD
        CommitteeUser NVARCHAR(200) NULL,
        AssignedAtUtc DATETIME2 NULL,
        AssignedByName NVARCHAR(250) NULL,
        CommitteeAcceptedAtUtc DATETIME2 NULL;
IF COL_LENGTH(N'dbo.FollowerArchiveRequests', N'FollowerNote') IS NULL
    ALTER TABLE dbo.FollowerArchiveRequests ADD FollowerNote NVARCHAR(500) NULL;";

    private const string RowSelect = @"
SELECT R.Id, R.CustomerId, R.CustomerName, R.PhoneNumber, R.ListId, R.ListName,
       R.FollowerUserId, COALESCE(NULLIF(LTRIM(RTRIM(U.UserName)), N''), R.FollowerName) AS FollowerName,
       R.Status, R.RequestedAtUtc, R.SentAtUtc, R.SentByName, R.SendNote, R.ReceivedAtUtc,
       R.CommitteeUser, R.AssignedAtUtc, R.FollowerNote,
       ROUND(ISNULL(SA.Total, 0) - ISNULL(PA.Total, 0), -3) AS Remaining
FROM dbo.FollowerArchiveRequests R
LEFT JOIN dbo.Users U ON U.UserID = R.FollowerUserId
OUTER APPLY (
    SELECT ROUND(ISNULL(SUM(V.AmountTotalSalesDenar), 0), -3) AS Total
    FROM dbo.View_CustomersSalesDelegate V WHERE V.CustomerID = R.CustomerId
) SA
OUTER APPLY (
    SELECT ROUND(ISNULL(SUM(V.AmountDenar), 0), -3) AS Total
    FROM dbo.View_CustomersPaymentsDelegate V WHERE V.CustomerID = R.CustomerId
) PA";

    private readonly string _cs;

    public FollowerArchiveRequestsService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("Missing connection string DataBaseConnection.");
    }

    public async Task<ArchiveRequestsBoardDto> BoardAsync(CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        var rows = (await c.QueryAsync<ArchiveRequestRowDto>(new CommandDefinition($@"
{RowSelect}
WHERE R.Status IN (N'Requested', N'ToCommittee', N'WithCommittee', N'Sent')
   OR (R.Status = N'Received' AND R.ReceivedAtUtc >= DATEADD(day, -@Days, SYSUTCDATETIME()))
ORDER BY R.RequestedAtUtc;",
            new { Days = ReceivedWindowDays }, cancellationToken: ct))).ToList();

        return new ArchiveRequestsBoardDto
        {
            Requested = rows.Where(r => r.Status == "Requested").ToList(),
            AtCommittee = rows.Where(r => r.Status is "ToCommittee" or "WithCommittee").OrderBy(r => r.AssignedAtUtc).ToList(),
            Sent = rows.Where(r => r.Status == "Sent").OrderBy(r => r.SentAtUtc).ToList(),
            Received = rows.Where(r => r.Status == "Received").OrderByDescending(r => r.ReceivedAtUtc).ToList(),
        };
    }

    public async Task<ArchiveRequestRowDto> AssignAsync(
        int requestId, string committeeUser, string? actorName, string? note, CancellationToken ct)
    {
        var user = CleanName(committeeUser) ?? throw new ArchiveRequestException(StatusCodes.Status400BadRequest, "اختر مسؤول اللجنة أو مساعده");
        var cleanNote = (note ?? string.Empty).Trim();
        if (cleanNote.Length > MaxNoteLength)
        {
            throw new ArchiveRequestException(StatusCodes.Status400BadRequest, $"الحد الأقصى للملاحظة {MaxNoteLength} حرف");
        }

        await using var c = await OpenAsync(ct);
        // Re-assigning is allowed until the committee member accepts it.
        var affected = await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerArchiveRequests
SET Status = N'ToCommittee', CommitteeUser = @User, AssignedAtUtc = SYSUTCDATETIME(),
    AssignedByName = @Actor, SendNote = @Note
WHERE Id = @Id AND Status IN (N'Requested', N'ToCommittee');",
            new { Id = requestId, User = user, Actor = CleanName(actorName), Note = cleanNote.Length == 0 ? null : cleanNote },
            cancellationToken: ct));
        return await AfterUpdateAsync(c, requestId, affected, "لا يمكن إسناد هذا الطلب الآن — استلمه مسؤول اللجنة أو المتابع مسبقاً", ct);
    }

    public async Task<IReadOnlyList<ArchiveRequestRowDto>> CommitteeListAsync(string committeeUser, CancellationToken ct)
    {
        var user = CleanName(committeeUser) ?? throw new ArchiveRequestException(StatusCodes.Status401Unauthorized, "تعذر تحديد المستخدم");
        await using var c = await OpenAsync(ct);
        var rows = await c.QueryAsync<ArchiveRequestRowDto>(new CommandDefinition($@"
{RowSelect}
WHERE R.CommitteeUser = @User
  AND (R.Status IN (N'ToCommittee', N'WithCommittee', N'Sent')
       OR (R.Status = N'Received' AND R.ReceivedAtUtc >= DATEADD(day, -@Days, SYSUTCDATETIME())))
ORDER BY CASE R.Status WHEN N'ToCommittee' THEN 0 WHEN N'WithCommittee' THEN 1 WHEN N'Sent' THEN 2 ELSE 3 END,
         R.AssignedAtUtc;",
            new { User = user, Days = ReceivedWindowDays }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ArchiveRequestRowDto> CommitteeAcceptAsync(string committeeUser, int requestId, CancellationToken ct)
    {
        var user = CleanName(committeeUser) ?? throw new ArchiveRequestException(StatusCodes.Status401Unauthorized, "تعذر تحديد المستخدم");
        await using var c = await OpenAsync(ct);
        var affected = await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerArchiveRequests
SET Status = N'WithCommittee', CommitteeAcceptedAtUtc = SYSUTCDATETIME()
WHERE Id = @Id AND CommitteeUser = @User AND Status = N'ToCommittee';",
            new { Id = requestId, User = user }, cancellationToken: ct));
        return await AfterUpdateAsync(c, requestId, affected, "هذا الطلب ليس بانتظار استلامك", ct);
    }

    public async Task<ArchiveRequestRowDto> CommitteeGiveAsync(string committeeUser, int requestId, CancellationToken ct)
    {
        var user = CleanName(committeeUser) ?? throw new ArchiveRequestException(StatusCodes.Status401Unauthorized, "تعذر تحديد المستخدم");
        await using var c = await OpenAsync(ct);
        var affected = await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerArchiveRequests
SET Status = N'Sent', SentAtUtc = SYSUTCDATETIME(), SentByUserId = NULL, SentByName = @User
WHERE Id = @Id AND CommitteeUser = @User AND Status = N'WithCommittee';",
            new { Id = requestId, User = user }, cancellationToken: ct));
        return await AfterUpdateAsync(c, requestId, affected, "استلم الأرشيف أولاً قبل تسليمه للمتابع", ct);
    }

    private static async Task<ArchiveRequestRowDto> AfterUpdateAsync(
        SqlConnection c, int requestId, int affected, string conflictMessage, CancellationToken ct)
    {
        var row = await c.QueryFirstOrDefaultAsync<ArchiveRequestRowDto>(new CommandDefinition(
            $"{RowSelect} WHERE R.Id = @Id;", new { Id = requestId }, cancellationToken: ct))
            ?? throw new ArchiveRequestException(StatusCodes.Status404NotFound, "الطلب غير موجود");
        if (affected == 0)
        {
            throw new ArchiveRequestException(StatusCodes.Status409Conflict, conflictMessage);
        }

        return row;
    }

    private static string? CleanName(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length < 2)
        {
            return null;
        }

        return text.Length <= 200 ? text : text[..200];
    }

    public async Task<IReadOnlyList<FinishedFollowerGroupDto>> FinishedByFollowerAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        await using var c = await OpenAsync(ct);
        var hasLists = await c.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.FollowerUserLists', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct)) == 1;
        if (!hasLists)
        {
            return [];
        }

        // Iraq calendar day (UTC+3) so the window matches PaymentDate as entered on the field.
        var since = DateTime.UtcNow.AddHours(3).Date.AddDays(-days);
        var rows = await c.QueryAsync<FinishedCustomerRowDto>(new CommandDefinition(@"
SELECT
    U.UserID AS FollowerUserId,
    LTRIM(RTRIM(U.UserName)) AS FollowerName,
    C.CustomerID AS CustomerId,
    C.CustomerName,
    C.PhoneNumber,
    LTRIM(RTRIM(ISNULL(D.DelegateName, N''))) AS ListName,
    P.LastPaymentDate AS FinishedAt,
    R.Status AS RequestStatus
FROM dbo.FollowerUserLists FUL
INNER JOIN dbo.Users U ON U.UserID = FUL.UserId
    AND ISNULL(U.UserState, 1) = 1
    AND (U.UserType = N'متابع' OR U.UserType LIKE N'متابع%')
INNER JOIN dbo.Customers C ON C.DelegateID = FUL.ListId AND ISNULL(C.CustomerState, 1) = 1
LEFT JOIN dbo.Delegates D ON D.DelegateID = C.DelegateID
OUTER APPLY (
    SELECT MAX(V.PaymentDate) AS LastPaymentDate,
           ROUND(ISNULL(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal
    FROM dbo.View_CustomersPaymentsDelegate V
    WHERE V.CustomerID = C.CustomerID
) P
OUTER APPLY (
    SELECT ROUND(ISNULL(SUM(V.AmountTotalSalesDenar), 0), -3) AS AmountTotalSales
    FROM dbo.View_CustomersSalesDelegate V
    WHERE V.CustomerID = C.CustomerID
) S
LEFT JOIN dbo.FollowerArchiveRequests R ON R.CustomerId = C.CustomerID
WHERE P.LastPaymentDate >= @Since
  AND ISNULL(S.AmountTotalSales, 0) > 0
  AND ROUND(ISNULL(S.AmountTotalSales, 0) - ISNULL(P.ReceiptsTotal, 0), -3) <= 0;",
            new { Since = since }, commandTimeout: 120, cancellationToken: ct));

        return rows
            .GroupBy(r => r.FollowerUserId)
            .Select(g =>
            {
                var customers = g
                    .GroupBy(r => r.CustomerId)
                    .Select(x => x.First())
                    .OrderBy(r => r.RequestStatus == null ? 0 : 1)
                    .ThenByDescending(r => r.FinishedAt)
                    .ToList();
                return new FinishedFollowerGroupDto
                {
                    FollowerUserId = g.Key,
                    FollowerName = g.First().FollowerName,
                    Total = customers.Count,
                    Requested = customers.Count(r => r.RequestStatus != null),
                    Customers = customers,
                };
            })
            .OrderByDescending(g => g.Total - g.Requested)
            .ThenBy(g => g.FollowerName)
            .ToList();
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var c = new SqlConnection(_cs);
        await c.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        return c;
    }
}
