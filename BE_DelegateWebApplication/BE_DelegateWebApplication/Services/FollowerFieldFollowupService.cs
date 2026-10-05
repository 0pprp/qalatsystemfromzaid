using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public sealed class FollowerFieldFollowupService : IFollowerFieldFollowupService
{
    private readonly string _cs;
    private static int _schemaReady;

    public FollowerFieldFollowupService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await using var c = new SqlConnection(_cs);
        await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerFieldFollowups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerFieldFollowups (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerFieldFollowups PRIMARY KEY,
        FollowerUserId INT NOT NULL,
        ListId INT NOT NULL,
        ListName NVARCHAR(200) NULL,
        CustomerId INT NOT NULL,
        CustomerName NVARCHAR(200) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_FollowerFieldFollowups_Status DEFAULT (N'Pending'),
        CompletionNote NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerFieldFollowups_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CompletedAtUtc DATETIME2 NULL
    );
    CREATE INDEX IX_FollowerFieldFollowups_Follower_Status
        ON dbo.FollowerFieldFollowups (FollowerUserId, Status) INCLUDE (ListId, CustomerId);
END
", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task<FollowerFieldFollowupRow> AddAsync(
        int followerUserId,
        int listId,
        string? listName,
        int customerId,
        string? customerName,
        string? phone,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (followerUserId <= 0 || listId <= 0 || customerId <= 0)
        {
            throw new InvalidOperationException("بيانات النقل غير كاملة");
        }

        await using var c = new SqlConnection(_cs);
        var existing = await c.QueryFirstOrDefaultAsync<FollowerFieldFollowupRow>(new CommandDefinition(@"
SELECT TOP 1 *
FROM dbo.FollowerFieldFollowups
WHERE FollowerUserId = @FollowerUserId
  AND ListId = @ListId
  AND CustomerId = @CustomerId
  AND Status = N'Pending';",
            new { FollowerUserId = followerUserId, ListId = listId, CustomerId = customerId },
            cancellationToken: ct));
        if (existing != null)
        {
            return existing;
        }

        var id = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.FollowerFieldFollowups
    (FollowerUserId, ListId, ListName, CustomerId, CustomerName, PhoneNumber, Status, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES
    (@FollowerUserId, @ListId, @ListName, @CustomerId, @CustomerName, @Phone, N'Pending', SYSUTCDATETIME());",
            new
            {
                FollowerUserId = followerUserId,
                ListId = listId,
                ListName = string.IsNullOrWhiteSpace(listName) ? null : listName.Trim(),
                CustomerId = customerId,
                CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()
            },
            cancellationToken: ct));

        return (await GetByIdAsync(c, id, ct))!;
    }

    public async Task<IReadOnlyList<FollowerFieldFollowupRow>> ListPendingAsync(
        int followerUserId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        var rows = await c.QueryAsync<FollowerFieldFollowupRow>(new CommandDefinition(@"
SELECT *
FROM dbo.FollowerFieldFollowups
WHERE FollowerUserId = @FollowerUserId AND Status = N'Pending'
ORDER BY ListName, CustomerName, Id;",
            new { FollowerUserId = followerUserId },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<FollowerFieldFollowupRow> CompleteAsync(
        int id,
        int followerUserId,
        string? note,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        if (!FollowerFieldFollowupRules.IsValidCompletionNote(note))
        {
            throw new InvalidOperationException("الملاحظة إجبارية قبل اعتماد تمت المتابعة");
        }

        var normalized = FollowerFieldFollowupRules.NormalizeNote(note);
        await using var c = new SqlConnection(_cs);
        var row = await GetByIdAsync(c, id, ct);
        if (row is null || row.FollowerUserId != followerUserId)
        {
            throw new InvalidOperationException("المتابعة غير موجودة");
        }

        if (!string.Equals(row.Status, FollowerFieldFollowupRules.StatusPending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("هذه المتابعة مكتملة مسبقاً");
        }

        await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerFieldFollowups
SET Status = N'Completed',
    CompletionNote = @Note,
    CompletedAtUtc = SYSUTCDATETIME()
WHERE Id = @Id AND FollowerUserId = @FollowerUserId AND Status = N'Pending';",
            new { Id = id, FollowerUserId = followerUserId, Note = normalized },
            cancellationToken: ct));

        return (await GetByIdAsync(c, id, ct))!;
    }

    public async Task<FollowerFieldFollowupRow> RecordCompleteAsync(
        int followerUserId,
        int listId,
        string? listName,
        int customerId,
        string? customerName,
        string? phone,
        string? note,
        CancellationToken ct)
    {
        var pending = await AddAsync(followerUserId, listId, listName, customerId, customerName, phone, ct);
        return await CompleteAsync(pending.Id, followerUserId, note, ct);
    }

    public async Task<IReadOnlyList<FollowerFieldFollowupLatestNote>> ListLatestCompletedNotesAsync(
        int followerUserId,
        int listId,
        CancellationToken ct)
    {
        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        var rows = await c.QueryAsync<FollowerFieldFollowupLatestNote>(new CommandDefinition(@"
SELECT CustomerId, Note, CompletedAtUtc
FROM (
    SELECT
        CustomerId,
        CompletionNote AS Note,
        CompletedAtUtc,
        ROW_NUMBER() OVER (PARTITION BY CustomerId ORDER BY CompletedAtUtc DESC, Id DESC) AS rn
    FROM dbo.FollowerFieldFollowups
    WHERE FollowerUserId = @FollowerUserId
      AND ListId = @ListId
      AND Status = N'Completed'
      AND CompletionNote IS NOT NULL
) x
WHERE rn = 1;",
            new { FollowerUserId = followerUserId, ListId = listId },
            cancellationToken: ct));
        return rows.ToList();
    }

    private static async Task<FollowerFieldFollowupRow?> GetByIdAsync(
        SqlConnection c,
        int id,
        CancellationToken ct)
    {
        return await c.QueryFirstOrDefaultAsync<FollowerFieldFollowupRow>(new CommandDefinition(
            "SELECT TOP 1 * FROM dbo.FollowerFieldFollowups WHERE Id = @Id",
            new { Id = id },
            cancellationToken: ct));
    }
}
