using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public static class LegalFollowupStatuses
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public static string Arabic(string? status) => status switch
    {
        Completed => "تمت المتابعة",
        _ => "قيد العمل"
    };
}

public sealed class LegalFollowupCaseDto
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int? CustomerId { get; set; }
    public int FollowerUserId { get; set; }
    public string FollowerName { get; set; } = "";
    public string Status { get; set; } = LegalFollowupStatuses.InProgress;
    public string StatusLabel => LegalFollowupStatuses.Arabic(Status);
    public string CustomerName { get; set; } = "";
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ShopImageKey { get; set; }
    public string? ShopImageUrl { get; set; }
    public bool LocationSelected => LegalFollowupCompletionRules.HasLocation(Latitude, Longitude);
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Note { get; set; }
    public string? CompletionNote { get; set; }
    public List<LegalFollowupDocumentDto> Documents { get; set; } = [];
    public List<string> MissingDocumentTypes { get; set; } = [];
    public bool CanSubmit { get; set; }
}

public sealed class LegalFollowupDocumentDto
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public string? FileName { get; set; }
    public string? DownloadUrl { get; set; }
    public bool FromFollowerUpload { get; set; }
}

public interface ILegalFollowupFollowerService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LegalFollowupCaseDto>> ListAsync(int followerUserId, string? status, CancellationToken ct = default);
    Task<LegalFollowupCaseDto?> GetAsync(int id, int followerUserId, CancellationToken ct = default);
    Task<LegalFollowupCaseDto> SetLocationAsync(int id, int followerUserId, double latitude, double longitude, string? address, CancellationToken ct = default);
    Task<LegalFollowupDocumentDto> UploadDocumentAsync(int id, int followerUserId, string documentType, IFormFile file, CancellationToken ct = default);
    Task<LegalFollowupCaseDto> CompleteAsync(int id, int followerUserId, string? note, CancellationToken ct = default);
    Task<(string FileName, byte[] Bytes, string ContentType)?> ReadDocumentAsync(int caseId, int documentId, int followerUserId, CancellationToken ct = default);
    Task<(string FileName, byte[] Bytes, string ContentType)?> ReadCaseDocumentAsync(int caseId, int documentId, int followerUserId, CancellationToken ct = default);
    Task<(string FileName, byte[] Bytes, string ContentType)?> ReadShopImageAsync(int caseId, int followerUserId, CancellationToken ct = default);
}

public sealed class LegalFollowupFollowerService : ILegalFollowupFollowerService
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;
    private readonly string _cs;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public LegalFollowupFollowerService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
        _env = env;
        _config = configuration;
    }

    public Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady) return Task.CompletedTask;
        lock (SchemaGate)
        {
            if (_schemaReady) return Task.CompletedTask;
            using var conn = new SqlConnection(_cs);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
IF OBJECT_ID(N'dbo.LegalFollowupCases', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.LegalFollowupCases
  (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LegalFollowupCases PRIMARY KEY,
    SaleId INT NULL,
    SalesRequestId INT NULL,
    CustomerId INT NULL,
    FollowerUserId INT NOT NULL,
    FollowerName NVARCHAR(200) NULL,
    Status NVARCHAR(32) NOT NULL CONSTRAINT DF_LegalFollowupCases_Status DEFAULT(N'InProgress'),
    CustomerName NVARCHAR(200) NOT NULL,
    CustomerPhone NVARCHAR(50) NULL,
    CustomerAddress NVARCHAR(500) NULL,
    Latitude FLOAT NULL,
    Longitude FLOAT NULL,
    ShopImageKey NVARCHAR(500) NULL,
    CreatedByUserId INT NOT NULL,
    CreatedByName NVARCHAR(200) NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_LegalFollowupCases_Created DEFAULT(SYSUTCDATETIME()),
    CompletedAtUtc DATETIME2(0) NULL,
    Note NVARCHAR(1000) NULL,
    CompletionNote NVARCHAR(1000) NULL
  );
  CREATE INDEX IX_LegalFollowupCases_Status_Created ON dbo.LegalFollowupCases(Status, CreatedAtUtc DESC);
  CREATE INDEX IX_LegalFollowupCases_Follower_Status ON dbo.LegalFollowupCases(FollowerUserId, Status, CreatedAtUtc DESC);
  CREATE INDEX IX_LegalFollowupCases_SaleId ON dbo.LegalFollowupCases(SaleId);
  CREATE INDEX IX_LegalFollowupCases_SalesRequestId ON dbo.LegalFollowupCases(SalesRequestId);
END
ELSE
BEGIN
  IF COL_LENGTH(N'dbo.LegalFollowupCases', N'SalesRequestId') IS NULL
    ALTER TABLE dbo.LegalFollowupCases ADD SalesRequestId INT NULL;
  IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.LegalFollowupCases') AND name = N'SaleId' AND is_nullable = 0)
    ALTER TABLE dbo.LegalFollowupCases ALTER COLUMN SaleId INT NULL;
END

IF OBJECT_ID(N'dbo.LegalFollowupCaseDocuments', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.LegalFollowupCaseDocuments
  (
    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LegalFollowupCaseDocuments PRIMARY KEY,
    CaseId INT NOT NULL,
    DocumentType NVARCHAR(64) NOT NULL,
    FileKey NVARCHAR(500) NOT NULL,
    FileName NVARCHAR(260) NOT NULL,
    ContentType NVARCHAR(100) NULL,
    UploadedByUserId INT NULL,
    CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_LegalFollowupCaseDocuments_Created DEFAULT(SYSUTCDATETIME()),
    UpdatedAtUtc DATETIME2(0) NULL
  );
  CREATE UNIQUE INDEX UX_LegalFollowupCaseDocuments_Case_Type ON dbo.LegalFollowupCaseDocuments(CaseId, DocumentType);
  CREATE INDEX IX_LegalFollowupCaseDocuments_CaseId ON dbo.LegalFollowupCaseDocuments(CaseId);
END";
            cmd.ExecuteNonQuery();
            _schemaReady = true;
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<LegalFollowupCaseDto>> ListAsync(int followerUserId, string? status, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var normalized = NormalizeStatus(status);
        await using var conn = new SqlConnection(_cs);
        var rows = (await conn.QueryAsync<LegalFollowupCaseDto>(new CommandDefinition(@"
SELECT Id, SaleId, CustomerId, FollowerUserId, FollowerName, Status,
       CustomerName, CustomerPhone, CustomerAddress, Latitude, Longitude, ShopImageKey,
       CreatedAtUtc, CompletedAtUtc, Note, CompletionNote
FROM dbo.LegalFollowupCases WITH (NOLOCK)
WHERE FollowerUserId=@FollowerUserId
  AND (@Status IS NULL OR Status=@Status)
ORDER BY CreatedAtUtc DESC;",
            new { FollowerUserId = followerUserId, Status = normalized },
            cancellationToken: ct))).ToList();

        foreach (var row in rows)
            row.ShopImageUrl = BuildShopImageUrl(row.Id, row.ShopImageKey);

        return rows;
    }

    public async Task<LegalFollowupCaseDto?> GetAsync(int id, int followerUserId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = new SqlConnection(_cs);
        var item = await conn.QueryFirstOrDefaultAsync<LegalFollowupCaseDto>(new CommandDefinition(@"
SELECT Id, SaleId, CustomerId, FollowerUserId, FollowerName, Status,
       CustomerName, CustomerPhone, CustomerAddress, Latitude, Longitude, ShopImageKey,
       CreatedAtUtc, CompletedAtUtc, Note, CompletionNote
FROM dbo.LegalFollowupCases WITH (NOLOCK)
WHERE Id=@Id AND FollowerUserId=@FollowerUserId;",
            new { Id = id, FollowerUserId = followerUserId },
            cancellationToken: ct));
        if (item == null) return null;

        item.ShopImageUrl = BuildShopImageUrl(item.Id, item.ShopImageKey);
        item.Documents = await LoadDocumentsAsync(conn, item, ct);
        ApplyCompletionFlags(item);
        return item;
    }

    public async Task<LegalFollowupCaseDto> SetLocationAsync(
        int id, int followerUserId, double latitude, double longitude, string? address, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        if (!LegalFollowupCompletionRules.HasLocation(latitude, longitude))
            throw new ArgumentException("إحداثيات موقع المحل غير صحيحة");

        await using var conn = new SqlConnection(_cs);
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.LegalFollowupCases
SET Latitude=@Latitude,
    Longitude=@Longitude,
    CustomerAddress=COALESCE(NULLIF(LTRIM(RTRIM(@Address)), N''), CustomerAddress)
WHERE Id=@Id AND FollowerUserId=@FollowerUserId AND Status=@InProgress;",
            new
            {
                Latitude = latitude,
                Longitude = longitude,
                Address = address,
                Id = id,
                FollowerUserId = followerUserId,
                InProgress = LegalFollowupStatuses.InProgress
            },
            cancellationToken: ct));
        if (affected <= 0)
            throw new InvalidOperationException("لا يمكن تحديث موقع متابعة مكتملة أو غير موجودة.");

        return (await GetAsync(id, followerUserId, ct))!;
    }

    public async Task<LegalFollowupDocumentDto> UploadDocumentAsync(
        int id, int followerUserId, string documentType, IFormFile file, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        if (file == null || file.Length <= 0)
            throw new ArgumentException("ملف الصورة مطلوب");
        if (!LegalFollowupCompletionRules.IsKnownUploadType(documentType))
            throw new ArgumentException("نوع المستمسك غير معروف");

        var type = documentType.Trim();
        var item = await GetAsync(id, followerUserId, ct)
                   ?? throw new InvalidOperationException("المتابعة غير موجودة");
        if (!string.Equals(item.Status, LegalFollowupStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("لا يمكن رفع مستمسكات لمتابعة مكتملة");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
        ext = ext.ToLowerInvariant();
        if (ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            ext = ".jpg";

        var folder = ResolveDocsFolder();
        Directory.CreateDirectory(folder);

        await using var conn = new SqlConnection(_cs);
        var existingId = await conn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(@"
SELECT TOP 1 Id FROM dbo.LegalFollowupCaseDocuments
WHERE CaseId=@CaseId AND DocumentType=@DocumentType
ORDER BY Id DESC;",
            new { CaseId = id, DocumentType = type },
            cancellationToken: ct));

        int docId;
        if (existingId is > 0)
        {
            docId = existingId.Value;
            var previous = await conn.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
                "SELECT FileKey FROM dbo.LegalFollowupCaseDocuments WHERE Id=@Id",
                new { Id = docId }, cancellationToken: ct));
            TryDeleteFile(previous);
        }
        else
        {
            docId = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.LegalFollowupCaseDocuments
(CaseId, DocumentType, FileKey, FileName, ContentType, UploadedByUserId, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES (@CaseId, @DocumentType, N'', @FileName, @ContentType, @UploadedByUserId, SYSUTCDATETIME());",
                new
                {
                    CaseId = id,
                    DocumentType = type,
                    FileName = string.IsNullOrWhiteSpace(file.FileName) ? "doc" + ext : Path.GetFileName(file.FileName),
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? GuessContentType(ext) : file.ContentType,
                    UploadedByUserId = followerUserId
                },
                cancellationToken: ct));
        }

        var storedName = $"case-{id}-{docId}{ext}";
        var path = Path.Combine(folder, storedName);
        await using (var stream = File.Create(path))
            await file.CopyToAsync(stream, ct);

        var key = BuildRelativeKey(storedName);
        await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.LegalFollowupCaseDocuments SET
 FileKey=@FileKey,
 FileName=@FileName,
 ContentType=@ContentType,
 UploadedByUserId=@UploadedByUserId,
 UpdatedAtUtc=SYSUTCDATETIME()
WHERE Id=@Id;",
            new
            {
                Id = docId,
                FileKey = key,
                FileName = storedName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? GuessContentType(ext) : file.ContentType,
                UploadedByUserId = followerUserId
            },
            cancellationToken: ct));

        return new LegalFollowupDocumentDto
        {
            Id = docId,
            DocumentType = type,
            TypeLabel = LegalFollowupCompletionRules.TypeLabel(type),
            FileName = storedName,
                DownloadUrl = $"/api/Followers/LegalFollowups/{id}/case-documents/{docId}/file",
            FromFollowerUpload = true
        };
    }

    public async Task<LegalFollowupCaseDto> CompleteAsync(int id, int followerUserId, string? note, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var item = await GetAsync(id, followerUserId, ct)
                   ?? throw new InvalidOperationException("المتابعة غير موجودة");
        var gate = LegalFollowupCompletionRules.ValidateReady(
            item.Latitude,
            item.Longitude,
            item.Documents.Select(d => d.DocumentType));
        if (gate != null)
            throw new InvalidOperationException(gate);

        await using var conn = new SqlConnection(_cs);
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.LegalFollowupCases
SET Status=@Completed, CompletedAtUtc=SYSUTCDATETIME(), CompletionNote=@Note
WHERE Id=@Id AND FollowerUserId=@FollowerUserId AND Status=@InProgress;",
            new
            {
                Completed = LegalFollowupStatuses.Completed,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
                Id = id,
                FollowerUserId = followerUserId,
                InProgress = LegalFollowupStatuses.InProgress
            },
            cancellationToken: ct));
        if (affected <= 0)
            throw new InvalidOperationException("لا يمكن إكمال هذه المتابعة أو أنها مكتملة مسبقاً.");

        return (await GetAsync(id, followerUserId, ct))!;
    }

    public async Task<(string FileName, byte[] Bytes, string ContentType)?> ReadCaseDocumentAsync(
        int caseId, int documentId, int followerUserId, CancellationToken ct = default)
    {
        var item = await GetAsync(caseId, followerUserId, ct);
        if (item == null) return null;

        await using var conn = new SqlConnection(_cs);
        var row = await conn.QueryFirstOrDefaultAsync<(string? FileKey, string? FileName, string? ContentType)>(
            new CommandDefinition(@"
SELECT FileKey, FileName, ContentType
FROM dbo.LegalFollowupCaseDocuments WITH (NOLOCK)
WHERE Id=@Id AND CaseId=@CaseId;",
                new { Id = documentId, CaseId = caseId },
                cancellationToken: ct));
        if (string.IsNullOrWhiteSpace(row.FileKey)) return null;
        var path = ResolvePath(row.FileKey);
        if (path == null || !File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var ctType = string.IsNullOrWhiteSpace(row.ContentType) ? GuessContentType(path) : row.ContentType!;
        return (row.FileName ?? Path.GetFileName(path), bytes, ctType);
    }

    public async Task<(string FileName, byte[] Bytes, string ContentType)?> ReadDocumentAsync(
        int caseId, int documentId, int followerUserId, CancellationToken ct = default)
    {
        var item = await GetAsync(caseId, followerUserId, ct);
        if (item == null) return null;

        await using var conn = new SqlConnection(_cs);
        var row = await conn.QueryFirstOrDefaultAsync<(string FileKey, string FileName, string? ContentType)>(
            new CommandDefinition(@"
SELECT FileKey, FileName, ContentType
FROM dbo.SalesCustomerDocuments WITH (NOLOCK)
WHERE Id=@Id
  AND (
    (@SaleId > 0 AND SaleId=@SaleId)
    OR (@SaleId <= 0 AND (
         (@CustomerId IS NOT NULL AND CustomerId=@CustomerId)
         OR (@Phone IS NOT NULL AND NULLIF(LTRIM(RTRIM(CustomerPhone)), N'') = @Phone)
         OR (@Name IS NOT NULL AND NULLIF(LTRIM(RTRIM(CustomerName)), N'') = @Name)
    ))
  );",
                new
                {
                    Id = documentId,
                    SaleId = item.SaleId,
                    CustomerId = item.CustomerId,
                    Phone = item.CustomerPhone,
                    Name = item.CustomerName
                },
                cancellationToken: ct));
        if (string.IsNullOrWhiteSpace(row.FileKey)) return null;

        var salesPath = ResolvePath(row.FileKey);
        if (salesPath == null || !File.Exists(salesPath)) return null;
        var salesBytes = await File.ReadAllBytesAsync(salesPath, ct);
        var salesCt = string.IsNullOrWhiteSpace(row.ContentType) ? GuessContentType(salesPath) : row.ContentType!;
        return (row.FileName, salesBytes, salesCt);
    }

    public async Task<(string FileName, byte[] Bytes, string ContentType)?> ReadShopImageAsync(
        int caseId, int followerUserId, CancellationToken ct = default)
    {
        var item = await GetAsync(caseId, followerUserId, ct);
        if (item == null || string.IsNullOrWhiteSpace(item.ShopImageKey)) return null;
        var path = ResolvePath(item.ShopImageKey);
        if (path == null || !File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return (Path.GetFileName(path), bytes, GuessContentType(path));
    }

    private static void ApplyCompletionFlags(LegalFollowupCaseDto item)
    {
        var present = item.Documents.Select(d => d.DocumentType).ToList();
        item.MissingDocumentTypes = LegalFollowupCompletionRules.RequiredDocumentTypes
            .Where(req => !present.Any(p => string.Equals(p, req, StringComparison.OrdinalIgnoreCase)
                || (string.Equals(req, "ResidenceCardFront", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p, "ResidenceCard", StringComparison.OrdinalIgnoreCase))
                || (string.Equals(req, "ResidenceCardBack", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p, "ResidenceCard", StringComparison.OrdinalIgnoreCase))))
            .ToList();
        item.CanSubmit = LegalFollowupCompletionRules.ValidateReady(item.Latitude, item.Longitude, present) == null
                         && string.Equals(item.Status, LegalFollowupStatuses.InProgress, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<LegalFollowupDocumentDto>> LoadDocumentsAsync(
        SqlConnection conn, LegalFollowupCaseDto item, CancellationToken ct)
    {
        var result = new List<LegalFollowupDocumentDto>();
        var caseRows = await conn.QueryAsync<(int Id, string DocumentType, string FileName)>(new CommandDefinition(@"
SELECT Id, DocumentType, FileName
FROM dbo.LegalFollowupCaseDocuments WITH (NOLOCK)
WHERE CaseId=@CaseId
ORDER BY Id;",
            new { CaseId = item.Id },
            cancellationToken: ct));

        foreach (var r in caseRows)
        {
            result.Add(new LegalFollowupDocumentDto
            {
                Id = r.Id,
                DocumentType = r.DocumentType,
                TypeLabel = LegalFollowupCompletionRules.TypeLabel(r.DocumentType),
                FileName = r.FileName,
                DownloadUrl = $"/api/Followers/LegalFollowups/{item.Id}/case-documents/{r.Id}/file",
                FromFollowerUpload = true
            });
        }

        var uploadedTypes = new HashSet<string>(
            result.Select(d => d.DocumentType),
            StringComparer.OrdinalIgnoreCase);

        var exists = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesCustomerDocuments', N'U') IS NULL THEN 0 ELSE 1 END",
            cancellationToken: ct));
        if (exists == 0) return result;

        var salesRows = await conn.QueryAsync<(int Id, string DocumentType, string FileName)>(new CommandDefinition(@"
SELECT Id, DocumentType, FileName
FROM dbo.SalesCustomerDocuments WITH (NOLOCK)
WHERE (@SaleId > 0 AND SaleId=@SaleId)
   OR (@SaleId <= 0 AND (
        (@CustomerId IS NOT NULL AND CustomerId=@CustomerId)
        OR (@Phone IS NOT NULL AND NULLIF(LTRIM(RTRIM(CustomerPhone)), N'') = @Phone)
        OR (@Name IS NOT NULL AND NULLIF(LTRIM(RTRIM(CustomerName)), N'') = @Name)
   ))
ORDER BY Id;",
            new
            {
                SaleId = item.SaleId,
                CustomerId = item.CustomerId,
                Phone = item.CustomerPhone,
                Name = item.CustomerName
            },
            cancellationToken: ct));

        foreach (var r in salesRows)
        {
            if (!IsLegalDocType(r.DocumentType)) continue;
            if (uploadedTypes.Contains(r.DocumentType)) continue;
            result.Add(new LegalFollowupDocumentDto
            {
                Id = r.Id,
                DocumentType = r.DocumentType,
                TypeLabel = LegalFollowupCompletionRules.TypeLabel(r.DocumentType),
                FileName = r.FileName,
                DownloadUrl = $"/api/Followers/LegalFollowups/{item.Id}/documents/{r.Id}/file",
                FromFollowerUpload = false
            });
        }

        return result;
    }

    private string ResolveDocsFolder()
    {
        var configured = (_config["LegalFollowup:DocsRoot"] ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Directory.CreateDirectory(configured);
            return configured;
        }

        var shared = Path.Combine("/opt/qalaa-runtime/shared/legal-followup-docs");
        try
        {
            Directory.CreateDirectory(shared);
            return shared;
        }
        catch
        {
            var local = Path.Combine(_env.ContentRootPath, "App_Data", "legal-followup-docs");
            Directory.CreateDirectory(local);
            return local;
        }
    }

    private string BuildRelativeKey(string storedName)
    {
        var folder = ResolveDocsFolder();
        // Prefer absolute path so BE_Company and BE_Delegate resolve the same file on this host.
        return Path.Combine(folder, storedName);
    }

    private void TryDeleteFile(string? key)
    {
        try
        {
            var path = ResolvePath(key ?? "");
            if (path != null && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private string? BuildShopImageUrl(int caseId, string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : $"/api/Followers/LegalFollowups/{caseId}/shop-image";

    private string? ResolvePath(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (Path.IsPathRooted(key) && File.Exists(key)) return key;
        var relative = key.Replace('\\', '/').TrimStart('/');
        var candidates = new[]
        {
            Path.Combine(_env.ContentRootPath, relative),
            Path.Combine(_env.WebRootPath ?? "", relative),
            Path.Combine(_env.ContentRootPath, "wwwroot", relative),
            Path.Combine(AppContext.BaseDirectory, relative),
            Path.Combine("/opt/qalaa-runtime/shared/legal-followup-docs", Path.GetFileName(relative)),
            Path.Combine("/opt/qalaa-runtime/shared/legal-followup-docs", relative)
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string GuessContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            _ => "image/jpeg"
        };
    }

    private static bool IsLegalDocType(string? t) =>
        LegalFollowupCompletionRules.IsKnownUploadType(t);

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var s = status.Trim();
        if (s.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || s.Equals("done", StringComparison.OrdinalIgnoreCase)
            || s == "تمت المتابعة"
            || s.Equals(LegalFollowupStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return LegalFollowupStatuses.Completed;
        if (s.Equals("inprogress", StringComparison.OrdinalIgnoreCase)
            || s.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
            || s.Equals("open", StringComparison.OrdinalIgnoreCase)
            || s == "قيد العمل"
            || s.Equals(LegalFollowupStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            return LegalFollowupStatuses.InProgress;
        return null;
    }
}
