using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Repository
{
    public sealed class FollowerActionsRepository : IFollowerActionsRepository
    {
        private readonly string _connectionString;

        public FollowerActionsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            await connection.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        }

        public async Task<(int DelegateId, string? CustomerName, string? Phone, string? Address, string? CityName, int? UserId, string? SaleName)?> GetCustomerScopeAsync(int customerId, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var row = await connection.QueryFirstOrDefaultAsync<dynamic>(new CommandDefinition(@"
SELECT TOP 1
    C.DelegateID AS DelegateId,
    C.CustomerName,
    C.PhoneNumber AS Phone,
    C.Address,
    Ci.CityName,
    C.UserID AS UserId,
    C.SaleName
FROM dbo.Customers C
LEFT JOIN dbo.Cities Ci ON Ci.CityID = C.CityID
WHERE C.CustomerID = @CustomerId;",
                new { CustomerId = customerId }, cancellationToken: ct));
            if (row == null)
            {
                return null;
            }

            return (
                (int)(row.DelegateId ?? 0),
                (string?)row.CustomerName,
                (string?)row.Phone,
                (string?)row.Address,
                (string?)row.CityName,
                row.UserId == null ? null : (int?)row.UserId,
                (string?)row.SaleName);
        }

        public async Task<string?> GetDelegateNameAsync(int delegateId, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT TOP 1 DelegateName FROM dbo.Delegates WHERE DelegateID = @Id",
                new { Id = delegateId }, cancellationToken: ct));
        }

        public async Task<FollowerCustomerNoteDTO> AddCustomerNoteAsync(FollowerCustomerNoteDTO note, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var connection = new SqlConnection(_connectionString);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.FollowerCustomerNotes
(CustomerId, NoteText, CreatedByUserId, CreatedByName, CreatedByRole, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@CustomerId, @NoteText, @CreatedByUserId, @CreatedByName, @CreatedByRole, @CreatedAtUtc);",
                note, cancellationToken: ct));
            note.Id = id;
            return note;
        }

        public async Task<IReadOnlyList<FollowerCustomerNoteDTO>> ListCustomerNotesAsync(int customerId, int followerId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var connection = new SqlConnection(_connectionString);
            var rows = await connection.QueryAsync<FollowerCustomerNoteDTO>(new CommandDefinition(@"
SELECT Id, CustomerId, NoteText, CreatedByUserId, CreatedByName, CreatedByRole, CreatedAtUtc
FROM dbo.FollowerCustomerNotes
WHERE CustomerId = @CustomerId AND CreatedByUserId = @FollowerId
ORDER BY CreatedAtUtc DESC;",
                new { CustomerId = customerId, FollowerId = followerId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<FollowerDelegateNoteDTO> AddDelegateNoteAsync(FollowerDelegateNoteDTO note, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var connection = new SqlConnection(_connectionString);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.FollowerDelegateNotes
(DelegateId, ListId, NoteText, CreatedByUserId, CreatedByName, CreatedByRole, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@DelegateId, @ListId, @NoteText, @CreatedByUserId, @CreatedByName, @CreatedByRole, @CreatedAtUtc);",
                note, cancellationToken: ct));
            note.Id = id;
            return note;
        }

        public async Task<IReadOnlyList<FollowerDelegateNoteDTO>> ListDelegateNotesForFollowerAsync(int delegateId, int followerId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var connection = new SqlConnection(_connectionString);
            var rows = await connection.QueryAsync<FollowerDelegateNoteDTO>(new CommandDefinition(@"
SELECT Id, DelegateId, ListId, NoteText, CreatedByUserId, CreatedByName, CreatedByRole, CreatedAtUtc
FROM dbo.FollowerDelegateNotes
WHERE DelegateId = @DelegateId AND CreatedByUserId = @FollowerId
ORDER BY CreatedAtUtc DESC;",
                new { DelegateId = delegateId, FollowerId = followerId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<FollowerSalesRequestResultDTO> InsertSalesRequestAsync(
            FollowerSalesRequestResultDTO meta,
            string customerName,
            string? phone,
            string? province,
            string? address,
            string? notes,
            int? existingCustomerId,
            string? cityValue,
            string? cityName,
            CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var tx = connection.BeginTransaction();

            await connection.ExecuteAsync(new CommandDefinition(SalesRequestsEnsureSql, transaction: tx, cancellationToken: ct));

            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesRequests
(CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName, CityValue, CityName,
 CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue, CustomerName, CustomerPhone, CustomerProvince,
 CustomerAddress, Notes, Status, CreatedAtUtc, AssignedAtUtc, AssignedByUserId, AssignedByName, PendingNote, ReturnNote)
OUTPUT INSERTED.Id
VALUES
(@CreatedByUserId, @CreatedByName, @CreatedByUserType, 0, NULL, @CityValue, @CityName,
 @CustomerSourceType, @ExistingCustomerId, NULL, @CustomerName, @Phone, @Province,
 @Address, @Notes, N'New', @CreatedAtUtc, NULL, NULL, NULL, NULL, NULL);",
                new
                {
                    meta.CreatedByUserId,
                    meta.CreatedByName,
                    CreatedByUserType = meta.CreatedByUserType ?? "Follower",
                    CityValue = cityValue,
                    CityName = cityName,
                    CustomerSourceType = "Follower",
                    ExistingCustomerId = existingCustomerId is > 0 ? existingCustomerId : null,
                    CustomerName = customerName,
                    Phone = phone,
                    Province = province,
                    Address = address,
                    Notes = notes,
                    meta.CreatedAtUtc
                },
                transaction: tx,
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesRequestHistory
(RequestId, EventType, PreviousStatus, Status, ActorUserId, ActorName, ActorType, EmployeeId, Note, CreatedAtUtc)
VALUES
(@RequestId, N'Created', NULL, N'New', @ActorUserId, @ActorName, N'Follower', NULL, @Note, @CreatedAtUtc);",
                new
                {
                    RequestId = id,
                    ActorUserId = meta.CreatedByUserId,
                    ActorName = meta.CreatedByName,
                    Note = notes,
                    meta.CreatedAtUtc
                },
                transaction: tx,
                cancellationToken: ct));

            tx.Commit();
            meta.Id = id;
            meta.CustomerSourceType = "Follower";
            meta.Status = "New";
            meta.ExistingCustomerId = existingCustomerId is > 0 ? existingCustomerId : null;
            return meta;
        }

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.FollowerCustomerNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerCustomerNotes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerId INT NOT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NOT NULL,
        CreatedByRole NVARCHAR(50) NOT NULL CONSTRAINT DF_FollowerCustomerNotes_Role DEFAULT (N'Follower'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerCustomerNotes_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_FollowerCustomerNotes_Customer ON dbo.FollowerCustomerNotes (CustomerId, CreatedAtUtc DESC);
END

IF OBJECT_ID(N'dbo.FollowerDelegateNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerDelegateNotes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DelegateId INT NOT NULL,
        ListId INT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NOT NULL,
        CreatedByRole NVARCHAR(50) NOT NULL CONSTRAINT DF_FollowerDelegateNotes_Role DEFAULT (N'Follower'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerDelegateNotes_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_FollowerDelegateNotes_Delegate ON dbo.FollowerDelegateNotes (DelegateId, CreatedByUserId);
END

-- Idempotent migration: copy old Employee-notes rows into Delegate notes (EmployeeId had been misused as list id in some demos).
IF OBJECT_ID(N'dbo.FollowerEmployeeNotes', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.FollowerDelegateNotes (DelegateId, ListId, NoteText, CreatedByUserId, CreatedByName, CreatedByRole, CreatedAtUtc)
    SELECT e.EmployeeId, e.ListId, e.NoteText, e.CreatedByUserId, e.CreatedByName, e.CreatedByRole, e.CreatedAtUtc
    FROM dbo.FollowerEmployeeNotes e
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.FollowerDelegateNotes d
        WHERE d.DelegateId = e.EmployeeId
          AND d.CreatedByUserId = e.CreatedByUserId
          AND d.NoteText = e.NoteText
          AND d.CreatedAtUtc = e.CreatedAtUtc
    );
END
";

        private const string SalesRequestsEnsureSql = @"
IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequests (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NULL,
        CreatedByUserType NVARCHAR(50) NULL,
        TargetEmployeeId INT NOT NULL CONSTRAINT DF_SR_Target DEFAULT (0),
        TargetEmployeeName NVARCHAR(200) NULL,
        CityValue NVARCHAR(50) NULL,
        CityName NVARCHAR(100) NULL,
        CustomerSourceType NVARCHAR(30) NOT NULL,
        ExistingCustomerId INT NULL,
        CustomerSourceCityValue NVARCHAR(50) NULL,
        CustomerName NVARCHAR(200) NOT NULL,
        CustomerPhone NVARCHAR(50) NULL,
        CustomerProvince NVARCHAR(100) NULL,
        CustomerAddress NVARCHAR(500) NULL,
        Notes NVARCHAR(MAX) NULL,
        Status NVARCHAR(30) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL,
        ViewedAtUtc DATETIME2 NULL,
        ProcessingAtUtc DATETIME2 NULL,
        ConvertedToSaleId INT NULL,
        CompletedAtUtc DATETIME2 NULL,
        RejectedAtUtc DATETIME2 NULL,
        RejectionReason NVARCHAR(MAX) NULL,
        AssignedAtUtc DATETIME2 NULL,
        AssignedByUserId INT NULL,
        AssignedByName NVARCHAR(200) NULL,
        PendingNote NVARCHAR(MAX) NULL,
        ReturnNote NVARCHAR(MAX) NULL,
        ManagerReadAtUtc DATETIME2 NULL
    );
END
IF OBJECT_ID(N'dbo.SalesRequestHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequestHistory (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RequestId INT NOT NULL,
        EventType NVARCHAR(50) NOT NULL,
        PreviousStatus NVARCHAR(30) NULL,
        Status NVARCHAR(30) NULL,
        ActorUserId INT NULL,
        ActorName NVARCHAR(200) NULL,
        ActorType NVARCHAR(50) NULL,
        EmployeeId INT NULL,
        Note NVARCHAR(MAX) NULL,
        CreatedAtUtc DATETIME2 NOT NULL
    );
END";
    }
}
