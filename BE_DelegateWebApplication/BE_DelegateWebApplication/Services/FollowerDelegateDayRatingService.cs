using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public sealed class FollowerDelegateDayRatingRow
{
    public int Id { get; set; }
    public int FollowerId { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public string? CityValue { get; set; }
    public DateTime IraqDate { get; set; }
    public int Score { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class FollowerDelegateDayRatingMissingDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";
}

public interface IFollowerDelegateDayRatingService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<FollowerDelegateDayRatingMissingDto>> ListMissingForTodayAsync(
        int followerUserId,
        string? cityValue,
        CancellationToken ct = default);

    Task<(bool Ok, string? Error, FollowerDelegateDayRatingRow? Row)> SubmitAsync(
        int followerUserId,
        int listId,
        int score,
        string? note,
        string? cityValue,
        CancellationToken ct = default);
}

public sealed class FollowerDelegateDayRatingService : IFollowerDelegateDayRatingService
{
    private readonly string _cs;
    private static int _schemaReady;
    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    public FollowerDelegateDayRatingService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        await SchemaGate.WaitAsync(ct);
        try
        {
            if (Volatile.Read(ref _schemaReady) == 1) return;
            await using var c = new SqlConnection(_cs);
            await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerDelegateDayRating', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerDelegateDayRating (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerDelegateDayRating PRIMARY KEY,
        FollowerId INT NOT NULL,
        ListId INT NOT NULL,
        CityValue NVARCHAR(100) NULL,
        IraqDate DATE NOT NULL,
        Score INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FDDR_At DEFAULT (SYSUTCDATETIME())
    );
END

IF OBJECT_ID(N'dbo.FollowerDelegateDayRating', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_FDDR_Follower_List_Day'
      AND object_id = OBJECT_ID(N'dbo.FollowerDelegateDayRating'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UX_FDDR_Follower_List_Day
        ON dbo.FollowerDelegateDayRating (FollowerId, ListId, IraqDate)');
END
", cancellationToken: ct));
            Volatile.Write(ref _schemaReady, 1);
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    public async Task<IReadOnlyList<FollowerDelegateDayRatingMissingDto>> ListMissingForTodayAsync(
        int followerUserId,
        string? cityValue,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var iraqToday = FollowerDelegateDayRatingRules.IraqToday(DateTime.UtcNow);
        await using var c = new SqlConnection(_cs);
        var rows = await c.QueryAsync<FollowerDelegateDayRatingMissingDto>(new CommandDefinition(@"
SELECT
    ful.ListId,
    ISNULL(d.DelegateName, N'') AS ListName
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Delegates d ON d.DelegateID = ful.ListId
WHERE ful.UserId = @FollowerUserId
  AND ISNULL(d.FollowerCollectionEnabled, 0) = 1
  AND NOT EXISTS (
        SELECT 1
        FROM dbo.FollowerDelegateDayRating r
        WHERE r.FollowerId = @FollowerUserId
          AND r.ListId = ful.ListId
          AND r.IraqDate = @IraqDate
  )
ORDER BY d.DelegateName;",
            new { FollowerUserId = followerUserId, IraqDate = iraqToday.ToDateTime(TimeOnly.MinValue) },
            cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<(bool Ok, string? Error, FollowerDelegateDayRatingRow? Row)> SubmitAsync(
        int followerUserId,
        int listId,
        int score,
        string? note,
        string? cityValue,
        CancellationToken ct = default)
    {
        var (ok, error) = FollowerDelegateDayRatingRules.ValidateScore(score);
        if (!ok) return (false, error, null);
        if (followerUserId <= 0 || listId <= 0)
            return (false, "معرّف غير صالح.", null);

        await EnsureSchemaAsync(ct);
        var iraqToday = FollowerDelegateDayRatingRules.IraqToday(DateTime.UtcNow);
        await using var c = new SqlConnection(_cs);

        var allowed = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Delegates d ON d.DelegateID = ful.ListId
WHERE ful.UserId = @FollowerUserId
  AND ful.ListId = @ListId
  AND ISNULL(d.FollowerCollectionEnabled, 0) = 1;",
            new { FollowerUserId = followerUserId, ListId = listId },
            cancellationToken: ct));
        if (allowed == 0)
            return (false, "القائمة غير مفعّلة لجباية المتابع أو غير مسندة.", null);

        var noteTrim = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var city = string.IsNullOrWhiteSpace(cityValue) ? null : cityValue.Trim();

        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.FollowerDelegateDayRating AS t
USING (SELECT @FollowerId AS FollowerId, @ListId AS ListId, @IraqDate AS IraqDate) AS s
ON t.FollowerId = s.FollowerId AND t.ListId = s.ListId AND t.IraqDate = s.IraqDate
WHEN MATCHED THEN UPDATE SET
    Score = @Score,
    Note = @Note,
    CityValue = COALESCE(@CityValue, t.CityValue),
    CreatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (FollowerId, ListId, CityValue, IraqDate, Score, Note, CreatedAtUtc)
VALUES
    (@FollowerId, @ListId, @CityValue, @IraqDate, @Score, @Note, SYSUTCDATETIME());",
            new
            {
                FollowerId = followerUserId,
                ListId = listId,
                IraqDate = iraqToday.ToDateTime(TimeOnly.MinValue),
                Score = score,
                Note = noteTrim,
                CityValue = city,
            },
            cancellationToken: ct));

        var row = await c.QuerySingleOrDefaultAsync<FollowerDelegateDayRatingRow>(new CommandDefinition(@"
SELECT TOP 1
    r.Id, r.FollowerId, r.ListId, d.DelegateName AS ListName,
    r.CityValue, r.IraqDate, r.Score, r.Note, r.CreatedAtUtc
FROM dbo.FollowerDelegateDayRating r
LEFT JOIN dbo.Delegates d ON d.DelegateID = r.ListId
WHERE r.FollowerId = @FollowerId AND r.ListId = @ListId AND r.IraqDate = @IraqDate;",
            new
            {
                FollowerId = followerUserId,
                ListId = listId,
                IraqDate = iraqToday.ToDateTime(TimeOnly.MinValue),
            },
            cancellationToken: ct));

        return (true, null, row);
    }
}
