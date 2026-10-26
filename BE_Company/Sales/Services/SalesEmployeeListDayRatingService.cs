using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class SalesEmployeeListDayRatingStatusDto
{
    public int ListId { get; set; }
    public string ListName { get; set; } = "";
    public bool AlreadyRated { get; set; }
    public int? Score { get; set; }
    public string? Note { get; set; }
}

public sealed class SalesEmployeeListDayRatingRow
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public DateTime IraqDate { get; set; }
    public int Score { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public interface ISalesEmployeeListDayRatingService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);

    Task<SalesEmployeeListDayRatingStatusDto> GetTodayStatusAsync(
        int employeeId,
        int listId,
        CancellationToken ct = default);

    Task<(bool Ok, string? Error, SalesEmployeeListDayRatingRow? Row)> SubmitAsync(
        int employeeId,
        int listId,
        int score,
        string? note,
        CancellationToken ct = default);
}

public sealed class SalesEmployeeListDayRatingService : ISalesEmployeeListDayRatingService
{
    private readonly string _cs;
    private static int _schemaReady;
    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    public SalesEmployeeListDayRatingService(IConfiguration configuration)
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
IF OBJECT_ID(N'dbo.SalesEmployeeListDayRating', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesEmployeeListDayRating (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesEmployeeListDayRating PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ListId INT NOT NULL,
        IraqDate DATE NOT NULL,
        Score INT NOT NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SELDR_At DEFAULT (SYSUTCDATETIME())
    );
END

IF OBJECT_ID(N'dbo.SalesEmployeeListDayRating', N'U') IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_SELDR_Employee_List_Day'
      AND object_id = OBJECT_ID(N'dbo.SalesEmployeeListDayRating'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UX_SELDR_Employee_List_Day
        ON dbo.SalesEmployeeListDayRating (EmployeeId, ListId, IraqDate)');
END
", cancellationToken: ct));
            Volatile.Write(ref _schemaReady, 1);
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    public async Task<SalesEmployeeListDayRatingStatusDto> GetTodayStatusAsync(
        int employeeId,
        int listId,
        CancellationToken ct = default)
    {
        if (employeeId <= 0 || listId <= 0)
            return new SalesEmployeeListDayRatingStatusDto { ListId = listId, AlreadyRated = false };

        await EnsureSchemaAsync(ct);
        var iraqToday = SalesEmployeeListDayRatingRules.IraqToday(DateTime.UtcNow);
        await using var c = new SqlConnection(_cs);

        var listName = await c.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 DelegateName FROM dbo.Delegates WHERE DelegateID = @ListId;",
            new { ListId = listId }, cancellationToken: ct)) ?? "";

        var row = await c.QueryFirstOrDefaultAsync<SalesEmployeeListDayRatingRow>(new CommandDefinition(@"
SELECT TOP 1 Id, EmployeeId, ListId, IraqDate, Score, Note, CreatedAtUtc
FROM dbo.SalesEmployeeListDayRating
WHERE EmployeeId = @EmployeeId AND ListId = @ListId AND IraqDate = @IraqDate;",
            new
            {
                EmployeeId = employeeId,
                ListId = listId,
                IraqDate = iraqToday.ToDateTime(TimeOnly.MinValue),
            },
            cancellationToken: ct));

        if (row is null)
        {
            return new SalesEmployeeListDayRatingStatusDto
            {
                ListId = listId,
                ListName = listName,
                AlreadyRated = false,
            };
        }

        return new SalesEmployeeListDayRatingStatusDto
        {
            ListId = listId,
            ListName = listName,
            AlreadyRated = true,
            Score = row.Score,
            Note = row.Note,
        };
    }

    public async Task<(bool Ok, string? Error, SalesEmployeeListDayRatingRow? Row)> SubmitAsync(
        int employeeId,
        int listId,
        int score,
        string? note,
        CancellationToken ct = default)
    {
        var (ok, error) = SalesEmployeeListDayRatingRules.ValidateScore(score);
        if (!ok) return (false, error, null);
        if (employeeId <= 0 || listId <= 0)
            return (false, "معرّف غير صالح.", null);

        await EnsureSchemaAsync(ct);
        var iraqToday = SalesEmployeeListDayRatingRules.IraqToday(DateTime.UtcNow);
        await using var c = new SqlConnection(_cs);

        var listExists = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.Delegates WHERE DelegateID = @ListId;",
            new { ListId = listId }, cancellationToken: ct));
        if (listExists == 0)
            return (false, "القائمة غير موجودة.", null);

        var noteTrim = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        await c.ExecuteAsync(new CommandDefinition(@"
MERGE dbo.SalesEmployeeListDayRating AS t
USING (SELECT @EmployeeId AS EmployeeId, @ListId AS ListId, @IraqDate AS IraqDate) AS s
ON t.EmployeeId = s.EmployeeId AND t.ListId = s.ListId AND t.IraqDate = s.IraqDate
WHEN MATCHED THEN UPDATE SET
    Score = @Score,
    Note = @Note,
    CreatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT
    (EmployeeId, ListId, IraqDate, Score, Note, CreatedAtUtc)
VALUES
    (@EmployeeId, @ListId, @IraqDate, @Score, @Note, SYSUTCDATETIME());",
            new
            {
                EmployeeId = employeeId,
                ListId = listId,
                IraqDate = iraqToday.ToDateTime(TimeOnly.MinValue),
                Score = score,
                Note = noteTrim,
            },
            cancellationToken: ct));

        var saved = await c.QuerySingleOrDefaultAsync<SalesEmployeeListDayRatingRow>(new CommandDefinition(@"
SELECT TOP 1
    r.Id, r.EmployeeId, r.ListId, d.DelegateName AS ListName,
    r.IraqDate, r.Score, r.Note, r.CreatedAtUtc
FROM dbo.SalesEmployeeListDayRating r
LEFT JOIN dbo.Delegates d ON d.DelegateID = r.ListId
WHERE r.EmployeeId = @EmployeeId AND r.ListId = @ListId AND r.IraqDate = @IraqDate;",
            new
            {
                EmployeeId = employeeId,
                ListId = listId,
                IraqDate = iraqToday.ToDateTime(TimeOnly.MinValue),
            },
            cancellationToken: ct));

        return (true, null, saved);
    }
}
