using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services
{
    /// <summary>
    /// Shared customer notes (dbo.CustomerNotes) keyed by CustomerId for all parties.
    /// </summary>
    public sealed class SharedCustomerNoteDto
    {
        public int NoteId { get; set; }
        public int CustomerId { get; set; }
        public string NoteText { get; set; } = "";
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
    }

    public interface ISharedCustomerNotesService
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task<IReadOnlyList<SharedCustomerNoteDto>> ListAsync(int customerId, CancellationToken ct = default);
        Task<SharedCustomerNoteDto> AddAsync(
            int customerId,
            string noteText,
            int? createdByUserId,
            string? createdByName,
            CancellationToken ct = default);
    }

    public sealed class SharedCustomerNotesService : ISharedCustomerNotesService
    {
        public const int MaxNoteLength = 2000;
        private readonly string _cs;

        public SharedCustomerNotesService(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("DataBaseConnection missing.");
        }

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            await using var c = new SqlConnection(_cs);
            await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<SharedCustomerNoteDto>> ListAsync(int customerId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var rows = await c.QueryAsync<SharedCustomerNoteDto>(new CommandDefinition(@"
SELECT
    N.NoteID AS NoteId,
    N.CustomerID AS CustomerId,
    N.NoteText,
    N.UserID AS CreatedByUserId,
    COALESCE(NULLIF(LTRIM(RTRIM(N.CreatedByName)), N''), U.UserName, N'—') AS CreatedByName,
    CAST(COALESCE(N.CreatedAtUtc, N.CreatedDate) AS DATETIME2) AS CreatedAtUtc
FROM dbo.CustomerNotes N
LEFT JOIN dbo.Users U ON U.UserID = N.UserID
WHERE N.CustomerID = @CustomerId
ORDER BY COALESCE(N.CreatedAtUtc, N.CreatedDate) DESC;",
                new { CustomerId = customerId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<SharedCustomerNoteDto> AddAsync(
            int customerId,
            string noteText,
            int? createdByUserId,
            string? createdByName,
            CancellationToken ct = default)
        {
            var text = (noteText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("نص الملاحظة مطلوب");
            }

            if (text.Length > MaxNoteLength)
            {
                throw new ArgumentException($"الحد الأقصى للملاحظة {MaxNoteLength} حرف");
            }

            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var row = await c.QueryFirstAsync<SharedCustomerNoteDto>(new CommandDefinition(@"
DECLARE @NoteID INT;
INSERT INTO dbo.CustomerNotes (CustomerID, UserID, NoteText, CreatedByName, CreatedAtUtc, CreatedDate)
VALUES (@CustomerId, @UserId, @NoteText, @CreatedByName, SYSUTCDATETIME(), GETDATE());
SET @NoteID = SCOPE_IDENTITY();
SELECT
    N.NoteID AS NoteId,
    N.CustomerID AS CustomerId,
    N.NoteText,
    N.UserID AS CreatedByUserId,
    COALESCE(NULLIF(LTRIM(RTRIM(N.CreatedByName)), N''), U.UserName, N'—') AS CreatedByName,
    CAST(COALESCE(N.CreatedAtUtc, N.CreatedDate) AS DATETIME2) AS CreatedAtUtc
FROM dbo.CustomerNotes N
LEFT JOIN dbo.Users U ON U.UserID = N.UserID
WHERE N.NoteID = @NoteID;",
                new
                {
                    CustomerId = customerId,
                    UserId = createdByUserId is > 0 ? createdByUserId : null,
                    NoteText = text,
                    CreatedByName = string.IsNullOrWhiteSpace(createdByName) ? null : createdByName.Trim(),
                },
                cancellationToken: ct));
            return row;
        }

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerNotes (
        NoteID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerID INT NOT NULL,
        UserID INT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedByName NVARCHAR(150) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomerNotes_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_CustomerNotes_CreatedDate DEFAULT (GETDATE())
    );
END

IF COL_LENGTH(N'dbo.CustomerNotes', N'CreatedByName') IS NULL
    ALTER TABLE dbo.CustomerNotes ADD CreatedByName NVARCHAR(150) NULL;

IF COL_LENGTH(N'dbo.CustomerNotes', N'CreatedAtUtc') IS NULL
    ALTER TABLE dbo.CustomerNotes ADD CreatedAtUtc DATETIME2 NULL;

IF EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomerNotes_Users'
)
    ALTER TABLE dbo.CustomerNotes DROP CONSTRAINT FK_CustomerNotes_Users;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.CustomerNotes') AND name = N'UserID' AND is_nullable = 0
)
    ALTER TABLE dbo.CustomerNotes ALTER COLUMN UserID INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomerNotes_Users')
    AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.CustomerNotes WITH NOCHECK
    ADD CONSTRAINT FK_CustomerNotes_Users FOREIGN KEY (UserID) REFERENCES dbo.Users (UserID);
END

UPDATE dbo.CustomerNotes
SET CreatedAtUtc = CAST(CreatedDate AS DATETIME2)
WHERE CreatedAtUtc IS NULL AND CreatedDate IS NOT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomerNotes_Customer_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.CustomerNotes')
)
BEGIN
    CREATE INDEX IX_CustomerNotes_Customer_CreatedAt
    ON dbo.CustomerNotes (CustomerID, CreatedAtUtc DESC);
END
";
    }
}
