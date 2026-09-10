using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Repository
{
    public sealed class FollowerActionsRepository : IFollowerActionsRepository
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _env;
        private readonly string? _documentsRoot;

        public FollowerActionsRepository(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
            _env = env;
            _documentsRoot = configuration["SalesDocumentsRoot"];
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

        public async Task<string?> GetFollowerCityNameAsync(int followerDelegateId, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            // followerDelegateId == Users.UserID for UserType=متابع.
            var fromUser = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 Ci.CityName
FROM dbo.UsersSelectedCities usc
LEFT JOIN dbo.Cities Ci ON Ci.CityID = usc.CityID
WHERE usc.UserID = @UserId;",
                new { UserId = followerDelegateId }, cancellationToken: ct));
            if (!string.IsNullOrWhiteSpace(fromUser))
            {
                return fromUser;
            }

            return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
SELECT TOP 1 Ci.CityName
FROM dbo.Delegates D
LEFT JOIN dbo.Cities Ci ON Ci.CityID = D.CityID
WHERE D.DelegateID = @Id;",
                new { Id = followerDelegateId }, cancellationToken: ct));
        }

        public async Task<string?> GetCityNameByIdAsync(int cityId, CancellationToken ct = default)
        {
            if (cityId <= 0) return null;
            await using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT TOP 1 CityName FROM dbo.Cities WHERE CityID = @Id",
                new { Id = cityId }, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<FollowerSalesDocumentRow>> ListCustomerSalesDocumentsAsync(
            int customerId, string? customerName, string? phone, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesCustomerDocuments', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (exists == 0)
            {
                return [];
            }

            HashSet<int> saleIds = [];
            var draftsExist = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (draftsExist == 1)
            {
                var drafts = await connection.QueryAsync<(int SaleId, int? CustomerId, string? FullName, string? Phone)>(
                    new CommandDefinition(
                        "SELECT SaleId, CustomerId, FullName, Phone FROM dbo.SalesDrafts",
                        cancellationToken: ct));
                saleIds = drafts
                    .Where(d =>
                        (customerId > 0 && d.CustomerId == customerId)
                        || NamesMatch(d.FullName, customerName)
                        || PhonesMatch(d.Phone, phone))
                    .Select(d => d.SaleId)
                    .ToHashSet();
            }

            var rows = (await connection.QueryAsync<FollowerSalesDocumentRow>(new CommandDefinition(@"
SELECT Id, SaleId, CustomerId, CustomerName, CustomerPhone, DocumentType, FileKey, FileName, ContentType
FROM dbo.SalesCustomerDocuments
ORDER BY CreatedAtUtc, Id;", cancellationToken: ct))).ToList();

            return rows.Where(r => DocumentMatches(r, customerId, customerName, phone, saleIds)).ToList();
        }

        public async Task<FollowerSalesDocumentRow?> GetSalesDocumentAsync(int documentId, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesCustomerDocuments', N'U') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));
            if (exists == 0)
            {
                return null;
            }

            return await connection.QueryFirstOrDefaultAsync<FollowerSalesDocumentRow>(new CommandDefinition(@"
SELECT Id, SaleId, CustomerId, CustomerName, CustomerPhone, DocumentType, FileKey, FileName, ContentType
FROM dbo.SalesCustomerDocuments WHERE Id = @Id;",
                new { Id = documentId }, cancellationToken: ct));
        }

        public async Task<(string? ShopImageKey, int? SaleId)?> GetCustomerShopImageAsync(
            int customerId, string? customerName, string? phone, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            var ok = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE
  WHEN OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NULL OR OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL THEN 0
  ELSE 1 END", cancellationToken: ct));
            if (ok == 0)
            {
                return null;
            }

            var rows = await connection.QueryAsync<(int SaleId, int? CustomerId, string? FullName, string? Phone, string? ShopImageKey)>(
                new CommandDefinition(@"
SELECT d.SaleId, d.CustomerId, d.FullName, d.Phone, p.ShopImageKey
FROM dbo.SalesDrafts d
INNER JOIN dbo.SalesShopProfiles p ON p.SaleId = d.SaleId
WHERE p.ShopImageKey IS NOT NULL AND LTRIM(RTRIM(p.ShopImageKey)) <> N''
ORDER BY d.SaleId DESC;", cancellationToken: ct));

            foreach (var row in rows)
            {
                if ((customerId > 0 && row.CustomerId == customerId)
                    || NamesMatch(row.FullName, customerName)
                    || PhonesMatch(row.Phone, phone))
                {
                    return (row.ShopImageKey, row.SaleId);
                }
            }

            return null;
        }

        public Task<(string FileName, byte[] Bytes, string ContentType)?> ReadSalesDocumentFileAsync(int documentId, CancellationToken ct = default) =>
            ReadKeyedFileAsync(async () =>
            {
                var row = await GetSalesDocumentAsync(documentId, ct);
                return row == null ? null : (row.FileKey, row.FileName, row.ContentType);
            }, ct);

        public Task<(string FileName, byte[] Bytes, string ContentType)?> ReadShopImageFileAsync(string shopImageKey, CancellationToken ct = default) =>
            ReadKeyedFileAsync(() => Task.FromResult<(string FileKey, string FileName, string? ContentType)?>(
                (shopImageKey, Path.GetFileName(shopImageKey.Replace('\\', '/')), null)), ct);

        private async Task<(string FileName, byte[] Bytes, string ContentType)?> ReadKeyedFileAsync(
            Func<Task<(string FileKey, string FileName, string? ContentType)?>> load,
            CancellationToken ct)
        {
            var meta = await load();
            if (meta == null || string.IsNullOrWhiteSpace(meta.Value.FileKey))
            {
                return null;
            }

            var path = ResolveDocumentPath(meta.Value.FileKey);
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, ct);
            var contentType = FollowerCustomerMedia.ResolveImageContentType(meta.Value.ContentType, path);
            var fileName = string.IsNullOrWhiteSpace(meta.Value.FileName)
                ? Path.GetFileName(path)
                : meta.Value.FileName;
            return (fileName, bytes, contentType);
        }

        /// <summary>Used for failure diagnostics only — never log secrets.</summary>
        public string DescribeDocumentPathResolution(string fileKey)
        {
            var resolved = ResolveDocumentPath(fileKey);
            var exists = File.Exists(resolved);
            return $"resolved='{resolved}'; exists={exists}; documentsRootConfigured={!string.IsNullOrWhiteSpace(_documentsRoot)}; contentRoot='{_env.ContentRootPath}'";
        }

        private string ResolveDocumentPath(string key)
        {
            if (Path.IsPathRooted(key) || key.Contains(":\\", StringComparison.Ordinal))
            {
                return key;
            }

            var relative = key.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            var roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(_documentsRoot))
            {
                roots.Add(_documentsRoot.TrimEnd('/', '\\'));
            }

            roots.Add(Path.Combine(_env.ContentRootPath, "App_Data"));
            if (!string.IsNullOrWhiteSpace(_env.WebRootPath))
            {
                roots.Add(Path.GetFullPath(Path.Combine(_env.WebRootPath, "..", "App_Data")));
            }

            foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidate = relative.StartsWith("App_Data", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(root.EndsWith("App_Data", StringComparison.OrdinalIgnoreCase)
                        ? Path.GetDirectoryName(root) ?? root
                        : root, relative)
                    : Path.Combine(root, relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(_env.ContentRootPath, "App_Data", relative);
        }

        private static bool DocumentMatches(
            FollowerSalesDocumentRow row,
            int customerId,
            string? customerName,
            string? phone,
            IReadOnlyCollection<int> saleIds)
        {
            if (customerId > 0 && row.CustomerId == customerId)
            {
                return true;
            }

            if (row.SaleId is int saleId && saleIds.Contains(saleId))
            {
                return true;
            }

            return NamesMatch(row.CustomerName, customerName) || PhonesMatch(row.CustomerPhone, phone);
        }

        private static bool NamesMatch(string? left, string? right)
        {
            var a = string.Join(" ", (left ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToLowerInvariant();
            var b = string.Join(" ", (right ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToLowerInvariant();
            return a.Length > 0 && a == b;
        }

        private static bool PhonesMatch(string? left, string? right)
        {
            static string? Digits(string? v)
            {
                if (string.IsNullOrWhiteSpace(v)) return null;
                var d = new string(v.Where(char.IsDigit).ToArray());
                return d.Length == 0 ? null : d;
            }

            var a = Digits(left);
            var b = Digits(right);
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            var tailA = a.Length > 10 ? a[^10..] : a;
            var tailB = b.Length > 10 ? b[^10..] : b;
            return tailA.Length >= 7 && tailA == tailB;
        }

        /// <summary>
        /// Prefer image MIME from extension when DB stores application/octet-stream or empty.
        /// </summary>
        internal static string ResolveContentType(string? stored, string path) =>
            FollowerCustomerMedia.ResolveImageContentType(stored, path);

        private static string GuessContentType(string ext) =>
            FollowerCustomerMedia.ResolveImageContentType(null, "x" + ext);

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
            string customerSourceType,
            string saleRequestType,
            int? sourceListId = null,
            CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var tx = connection.BeginTransaction();

            await connection.ExecuteAsync(new CommandDefinition(SalesRequestsEnsureSql, transaction: tx, cancellationToken: ct));

            var sourceType = string.IsNullOrWhiteSpace(customerSourceType) ? "Follower" : customerSourceType.Trim();
            var kind = SaleRequestTypes.Normalize(saleRequestType, existingCustomerId is > 0);
            var actorType = meta.CreatedByUserType ?? sourceType;

            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesRequests
(CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName, CityValue, CityName,
 CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue, CustomerName, CustomerPhone, CustomerProvince,
 CustomerAddress, Notes, Status, CreatedAtUtc, AssignedAtUtc, AssignedByUserId, AssignedByName, PendingNote, ReturnNote,
 SaleRequestType, SourceListId)
OUTPUT INSERTED.Id
VALUES
(@CreatedByUserId, @CreatedByName, @CreatedByUserType, 0, NULL, @CityValue, @CityName,
 @CustomerSourceType, @ExistingCustomerId, NULL, @CustomerName, @Phone, @Province,
 @Address, @Notes, N'New', @CreatedAtUtc, NULL, NULL, NULL, NULL, NULL,
 @SaleRequestType, @SourceListId);",
                new
                {
                    meta.CreatedByUserId,
                    meta.CreatedByName,
                    CreatedByUserType = actorType,
                    CityValue = cityValue,
                    CityName = cityName,
                    CustomerSourceType = sourceType,
                    ExistingCustomerId = existingCustomerId is > 0 ? existingCustomerId : null,
                    CustomerName = customerName,
                    Phone = phone,
                    Province = province,
                    Address = address,
                    Notes = notes,
                    meta.CreatedAtUtc,
                    SaleRequestType = kind,
                    SourceListId = sourceListId is > 0 ? sourceListId : null
                },
                transaction: tx,
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesRequestHistory
(RequestId, EventType, PreviousStatus, Status, ActorUserId, ActorName, ActorType, EmployeeId, Note, CreatedAtUtc)
VALUES
(@RequestId, N'Created', NULL, N'New', @ActorUserId, @ActorName, @ActorType, NULL, @Note, @CreatedAtUtc);",
                new
                {
                    RequestId = id,
                    ActorUserId = meta.CreatedByUserId,
                    ActorName = meta.CreatedByName,
                    ActorType = actorType,
                    Note = notes,
                    meta.CreatedAtUtc
                },
                transaction: tx,
                cancellationToken: ct));

            tx.Commit();
            meta.Id = id;
            meta.CustomerSourceType = sourceType;
            meta.SaleRequestType = kind;
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
END
IF COL_LENGTH(N'dbo.SalesRequests', N'SaleRequestType') IS NULL
    ALTER TABLE dbo.SalesRequests ADD SaleRequestType NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'SourceListId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD SourceListId INT NULL;";
    }
}
