using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class LegalFollowupService : ILegalFollowupService
{
    private static readonly object SchemaGate = new();
    private static bool _schemaReady;

    private readonly SalesDevelopmentGuard _guard;
    private readonly IFollowerDirectoryService _followers;
    private readonly ISalesCustomerDocumentService _documents;
    private readonly ISalesRequestRepository _requests;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public LegalFollowupService(
        SalesDevelopmentGuard guard,
        IFollowerDirectoryService followers,
        ISalesCustomerDocumentService documents,
        ISalesRequestRepository requests,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _guard = guard;
        _followers = followers;
        _documents = documents;
        _requests = requests;
        _config = config;
        _env = env;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady) return;
        lock (SchemaGate)
        {
            if (_schemaReady) return;
            EnsureSql();
            _schemaReady = true;
        }
        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<LegalFollowupCaseDTO>> ListAsync(string? status, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        return await QueryCasesAsync(statusFilter: status, followerUserId: null, id: null, ct);
    }

    public async Task<LegalFollowupCaseDTO?> GetAsync(int id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var rows = await QueryCasesAsync(statusFilter: null, followerUserId: null, id: id, ct);
        var item = rows.FirstOrDefault();
        if (item == null) return null;
        await AttachDocumentsAsync(item, ct);
        return item;
    }

    public async Task<LegalFollowupCaseDTO> CreateAsync(
        LegalFollowupCreateRequest request,
        int actorUserId,
        string? actorName,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await _requests.EnsureSchemaAsync(ct);

        if (request.SalesRequestId <= 0) throw new InvalidOperationException("رقم طلب البيع غير صالح.");
        if (request.FollowerUserId <= 0) throw new InvalidOperationException("اختر المتابع.");

        var follower = (await _followers.ListActiveFollowersAsync(ct))
            .FirstOrDefault(f => f.UserId == request.FollowerUserId);
        if (follower == null)
            throw new InvalidOperationException("المتابع غير موجود أو غير مفعّل.");

        var row = await _requests.GetByIdAsync(request.SalesRequestId, ct)
                  ?? throw new InvalidOperationException("طلب المبيع غير موجود.");

        if (!CanSendSalesRequestToLegal(row))
        {
            if (row.IsDeleted)
                throw new InvalidOperationException("الطلب محذوف ولا يمكن إرساله للقانونية.");
            if (SalesExceptionHoldStatuses.IsHeld(row.ExceptionHoldStatus))
                throw new InvalidOperationException("الطلب محجوز ضمن طلب استثناء ولا يمكن إرساله للقانونية.");
            throw new InvalidOperationException("يُسمح بإرسال الطلبات غير المسندة أو المرفوضة فقط للمتابعة القانونية.");
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
            row.CustomerName = request.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
            row.CustomerPhone = request.CustomerPhone.Trim();
        if (!string.IsNullOrWhiteSpace(request.CustomerAddress))
            row.CustomerAddress = request.CustomerAddress.Trim();
        if (request.ExistingCustomerId is > 0)
        {
            row.ExistingCustomerId = request.ExistingCustomerId;
            row.CustomerSourceType = SalesRequestSources.ExistingCustomer;
        }

        if (string.IsNullOrWhiteSpace(row.CustomerName))
            throw new InvalidOperationException("اسم الزبون مطلوب قبل الإرسال.");
        if (string.IsNullOrWhiteSpace(row.CustomerPhone))
            throw new InvalidOperationException("رقم الهاتف مطلوب قبل الإرسال.");
        if (string.IsNullOrWhiteSpace(row.CustomerAddress))
            throw new InvalidOperationException("العنوان مطلوب قبل الإرسال.");

        await using var conn = Open();
        await using (var check = new SqlCommand(@"
SELECT TOP 1 Id FROM dbo.LegalFollowupCases WITH (NOLOCK)
WHERE SalesRequestId=@SalesRequestId AND Status=@InProgress;", conn))
        {
            check.Parameters.AddWithValue("@SalesRequestId", request.SalesRequestId);
            check.Parameters.AddWithValue("@InProgress", LegalFollowupStatuses.InProgress);
            var existing = await check.ExecuteScalarAsync(ct);
            if (existing != null && existing != DBNull.Value)
                throw new InvalidOperationException("هذا الطلب مُرسل مسبقاً للمتابعة القانونية وما زال قيد العمل.");
        }

        var shop = await LoadCustomerShopSnapshotAsync(row.ExistingCustomerId, row.CustomerName, row.CustomerPhone, ct);

        int newId;
        await using (var cmd = new SqlCommand(@"
INSERT INTO dbo.LegalFollowupCases
(SaleId, SalesRequestId, CustomerId, FollowerUserId, FollowerName, Status,
 CustomerName, CustomerPhone, CustomerAddress, Latitude, Longitude, ShopImageKey,
 CreatedByUserId, CreatedByName, CreatedAtUtc, Note)
OUTPUT INSERTED.Id
VALUES
(@SaleId, @SalesRequestId, @CustomerId, @FollowerUserId, @FollowerName, @Status,
 @CustomerName, @CustomerPhone, @CustomerAddress, @Lat, @Lng, @ShopImageKey,
 @CreatedByUserId, @CreatedByName, SYSUTCDATETIME(), @Note);", conn))
        {
            cmd.Parameters.AddWithValue("@SaleId", DBNull.Value);
            cmd.Parameters.AddWithValue("@SalesRequestId", request.SalesRequestId);
            cmd.Parameters.AddWithValue("@CustomerId", (object?)row.ExistingCustomerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FollowerUserId", request.FollowerUserId);
            cmd.Parameters.AddWithValue("@FollowerName", (object?)follower.FollowerName ?? "");
            cmd.Parameters.AddWithValue("@Status", LegalFollowupStatuses.InProgress);
            cmd.Parameters.AddWithValue("@CustomerName", row.CustomerName.Trim());
            cmd.Parameters.AddWithValue("@CustomerPhone", row.CustomerPhone!.Trim());
            cmd.Parameters.AddWithValue("@CustomerAddress", row.CustomerAddress!.Trim());
            cmd.Parameters.AddWithValue("@Lat", (object?)shop.Latitude ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Lng", (object?)shop.Longitude ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ShopImageKey", (object?)shop.ShopImageKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedByUserId", actorUserId);
            cmd.Parameters.AddWithValue("@CreatedByName", (object?)actorName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Note", string.IsNullOrWhiteSpace(request.Note) ? DBNull.Value : request.Note.Trim());
            newId = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        var previous = row.Status;
        row.Status = SalesRequestStatuses.LegalFollowup;
        // Leave the sales-employee app: rejected→legal clears assignment.
        row.TargetEmployeeId = 0;
        row.TargetEmployeeName = null;
        await _requests.UpdateAsync(row, ct);
        await _requests.InsertHistoryAsync(new SalesRequestHistoryDTO
        {
            RequestId = row.Id,
            Event = SalesRequestEvents.LegalFollowupSent,
            PreviousStatus = previous,
            Status = row.Status,
            ActorUserId = actorUserId,
            ActorName = actorName,
            ActorType = "SalesManager",
            Note = string.IsNullOrWhiteSpace(request.Note)
                ? $"إرسال للمتابع: {follower.FollowerName}"
                : request.Note.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        }, ct);

        return (await GetAsync(newId, ct))!;
    }

    /// <summary>
    /// Unassigned (New, no target) or employee-rejected requests may go to legal followup.
    /// Soft-deleted / exception-held rows are blocked.
    /// </summary>
    public static bool CanSendSalesRequestToLegal(SalesRequestDTO? row)
    {
        if (row == null || row.IsDeleted)
            return false;
        if (SalesExceptionHoldStatuses.IsHeld(row.ExceptionHoldStatus))
            return false;
        if (string.Equals(row.Status, SalesRequestStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            return true;
        return SalesRequestStatuses.IsUnassigned(row.Status) && row.TargetEmployeeId <= 0;
    }

    public async Task<IReadOnlyList<LegalFollowupCaseDTO>> ListForFollowerAsync(
        int followerUserId,
        string? status,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        return await QueryCasesAsync(statusFilter: status, followerUserId: followerUserId, id: null, ct);
    }

    public async Task<LegalFollowupCaseDTO?> GetForFollowerAsync(
        int id,
        int followerUserId,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var rows = await QueryCasesAsync(statusFilter: null, followerUserId: followerUserId, id: id, ct);
        var item = rows.FirstOrDefault();
        if (item == null) return null;
        await AttachDocumentsAsync(item, ct);
        return item;
    }

    public async Task<LegalFollowupCaseDTO> CompleteForFollowerAsync(
        int id,
        int followerUserId,
        string? note,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = Open();
        await using var cmd = new SqlCommand(@"
UPDATE dbo.LegalFollowupCases
SET Status=@Completed,
    CompletedAtUtc=SYSUTCDATETIME(),
    CompletionNote=@Note
WHERE Id=@Id AND FollowerUserId=@FollowerUserId AND Status=@InProgress;", conn);
        cmd.Parameters.AddWithValue("@Completed", LegalFollowupStatuses.Completed);
        cmd.Parameters.AddWithValue("@Note", string.IsNullOrWhiteSpace(note) ? DBNull.Value : note.Trim());
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@FollowerUserId", followerUserId);
        cmd.Parameters.AddWithValue("@InProgress", LegalFollowupStatuses.InProgress);
        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected <= 0)
            throw new InvalidOperationException("لا يمكن إكمال هذه المتابعة أو أنها مكتملة مسبقاً.");

        return (await GetForFollowerAsync(id, followerUserId, ct))!;
    }

    private async Task AttachDocumentsAsync(LegalFollowupCaseDTO item, CancellationToken ct)
    {
        item.ShopImageUrl = BuildShopImageUrl(item.ShopImageKey);
        var merged = new List<LegalFollowupDocumentDTO>();
        var uploadedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var conn = Open();
            await using var cmd = new SqlCommand(@"
SELECT Id, DocumentType, FileName
FROM dbo.LegalFollowupCaseDocuments WITH (NOLOCK)
WHERE CaseId=@CaseId
ORDER BY Id;", conn);
            cmd.Parameters.AddWithValue("@CaseId", item.Id);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var type = reader.GetString(1);
                uploadedTypes.Add(type);
                merged.Add(new LegalFollowupDocumentDTO
                {
                    Id = reader.GetInt32(0),
                    DocumentType = type,
                    TypeLabel = TypeLabel(type),
                    FileName = reader.IsDBNull(2) ? null : reader.GetString(2),
                    DownloadUrl = $"/api/sales-manager/legal-followups/{item.Id}/case-documents/{reader.GetInt32(0)}/file"
                });
            }
        }
        catch
        {
            // table may not exist yet on older branches — EnsureSchema creates it
        }

        try
        {
            List<SalesCustomerDocumentDTO> docs;
            if (item.SaleId > 0)
                docs = await _documents.ListForSaleAsync(item.SaleId, managerUrls: true, ct);
            else
                docs = await _documents.ListForCustomerAsync(item.CustomerId, item.CustomerName, item.CustomerPhone, managerUrls: true, ct);

            foreach (var d in docs.Where(d => IsLegalDocType(d.DocumentType)))
            {
                if (uploadedTypes.Contains(d.DocumentType)) continue;
                merged.Add(new LegalFollowupDocumentDTO
                {
                    Id = d.Id,
                    DocumentType = d.DocumentType,
                    TypeLabel = TypeLabel(d.DocumentType),
                    FileName = d.FileName,
                    DownloadUrl = d.FileUrl ?? $"/api/sales-manager/customer-documents/{d.Id}/file"
                });
            }
        }
        catch
        {
            // keep follower uploads even if sales docs fail
        }

        item.Documents = merged;
    }

    public async Task<(string FileName, byte[] Bytes, string ContentType)?> ReadCaseDocumentAsync(
        int caseId, int documentId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        await using var conn = Open();
        await using var cmd = new SqlCommand(@"
SELECT FileKey, FileName, ContentType
FROM dbo.LegalFollowupCaseDocuments WITH (NOLOCK)
WHERE Id=@Id AND CaseId=@CaseId;", conn);
        cmd.Parameters.AddWithValue("@Id", documentId);
        cmd.Parameters.AddWithValue("@CaseId", caseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var key = reader.IsDBNull(0) ? null : reader.GetString(0);
        var fileName = reader.IsDBNull(1) ? "doc.jpg" : reader.GetString(1);
        var contentType = reader.IsDBNull(2) ? null : reader.GetString(2);
        if (string.IsNullOrWhiteSpace(key)) return null;
        var path = ResolveLegalDocPath(key);
        if (path == null || !File.Exists(path)) return null;
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return (fileName, bytes, string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType);
    }

    private string? ResolveLegalDocPath(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (Path.IsPathRooted(key) && File.Exists(key)) return key;
        var relative = key.Replace('\\', '/').TrimStart('/');
        var candidates = new[]
        {
            Path.Combine(_env.ContentRootPath, relative),
            Path.Combine(_env.WebRootPath ?? "", relative),
            Path.Combine(_env.ContentRootPath, "wwwroot", relative),
            Path.Combine("/opt/qalaa-runtime/shared/legal-followup-docs", Path.GetFileName(relative)),
            Path.Combine("/opt/qalaa-runtime/shared/legal-followup-docs", relative),
            key
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static bool IsLegalDocType(string? t) =>
        string.Equals(t, SalesCustomerDocumentTypes.NationalIdFront, StringComparison.OrdinalIgnoreCase)
        || string.Equals(t, SalesCustomerDocumentTypes.NationalIdBack, StringComparison.OrdinalIgnoreCase)
        || string.Equals(t, SalesCustomerDocumentTypes.ResidenceCard, StringComparison.OrdinalIgnoreCase)
        || string.Equals(t, SalesCustomerDocumentTypes.ResidenceCardFront, StringComparison.OrdinalIgnoreCase)
        || string.Equals(t, SalesCustomerDocumentTypes.ResidenceCardBack, StringComparison.OrdinalIgnoreCase)
        || string.Equals(t, SalesCustomerDocumentTypes.ResidenceCertificate, StringComparison.OrdinalIgnoreCase);

    private static string TypeLabel(string? t) => t switch
    {
        SalesCustomerDocumentTypes.NationalIdFront => "البطاقة الوطنية - أمامية",
        SalesCustomerDocumentTypes.NationalIdBack => "البطاقة الوطنية - خلفية",
        SalesCustomerDocumentTypes.ResidenceCard => "بطاقة السكن",
        SalesCustomerDocumentTypes.ResidenceCardFront => "بطاقة السكن - أمامية",
        SalesCustomerDocumentTypes.ResidenceCardBack => "بطاقة السكن - خلفية",
        SalesCustomerDocumentTypes.ResidenceCertificate => "مضبطة تأييد سكن",
        _ => t ?? ""
    };

    private string? BuildShopImageUrl(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var baseUrl = (_config["PublicBaseUrl"] ?? "").Trim().TrimEnd('/');
        var path = key.StartsWith("/", StringComparison.Ordinal) ? key : "/" + key.TrimStart('/');
        return string.IsNullOrWhiteSpace(baseUrl) ? path : baseUrl + path;
    }

    private async Task<(double? Latitude, double? Longitude, string? ShopImageKey)> LoadCustomerShopSnapshotAsync(
        int? customerId, string? customerName, string? phone, CancellationToken ct)
    {
        await using var conn = Open();
        await using (var existsCmd = new SqlCommand(@"
SELECT CASE
  WHEN OBJECT_ID(N'dbo.SalesShopProfiles', N'U') IS NULL OR OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL THEN 0
  ELSE 1 END;", conn))
        {
            var ok = Convert.ToInt32(await existsCmd.ExecuteScalarAsync(ct));
            if (ok == 0) return (null, null, null);
        }

        await using var cmd = new SqlCommand(@"
SELECT TOP 1 p.Latitude, p.Longitude, NULLIF(LTRIM(RTRIM(p.ShopImageKey)), N'')
FROM dbo.SalesShopProfiles p WITH (NOLOCK)
INNER JOIN dbo.SalesDrafts d WITH (NOLOCK) ON d.SaleId = p.SaleId
WHERE (@CustomerId IS NOT NULL AND d.CustomerId = @CustomerId)
   OR (@Phone IS NOT NULL AND NULLIF(LTRIM(RTRIM(d.Phone)), N'') = @Phone)
   OR (@Name IS NOT NULL AND NULLIF(LTRIM(RTRIM(d.FullName)), N'') = @Name)
ORDER BY d.SaleId DESC;", conn);
        cmd.Parameters.AddWithValue("@CustomerId", (object?)customerId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : phone.Trim());
        cmd.Parameters.AddWithValue("@Name", string.IsNullOrWhiteSpace(customerName) ? DBNull.Value : customerName.Trim());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return (null, null, null);
        return (
            r.IsDBNull(0) ? null : Convert.ToDouble(r.GetValue(0)),
            r.IsDBNull(1) ? null : Convert.ToDouble(r.GetValue(1)),
            r.IsDBNull(2) ? null : r.GetString(2));
    }

    private async Task<IReadOnlyList<LegalFollowupCaseDTO>> QueryCasesAsync(
        string? statusFilter,
        int? followerUserId,
        int? id,
        CancellationToken ct)
    {
        var sql = @"
SELECT Id, ISNULL(SaleId, 0), CustomerId, FollowerUserId, FollowerName, Status,
       CustomerName, CustomerPhone, CustomerAddress, Latitude, Longitude, ShopImageKey,
       CreatedByUserId, CreatedByName, CreatedAtUtc, CompletedAtUtc, Note, CompletionNote,
       SalesRequestId
FROM dbo.LegalFollowupCases WITH (NOLOCK)
WHERE 1=1";
        if (id.HasValue) sql += " AND Id=@Id";
        if (followerUserId.HasValue) sql += " AND FollowerUserId=@FollowerUserId";
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var normalized = NormalizeStatus(statusFilter);
            if (normalized != null) sql += " AND Status=@Status";
            else statusFilter = null;
        }
        sql += " ORDER BY CreatedAtUtc DESC;";

        var list = new List<LegalFollowupCaseDTO>();
        await using var conn = Open();
        await using var cmd = new SqlCommand(sql, conn);
        if (id.HasValue) cmd.Parameters.AddWithValue("@Id", id.Value);
        if (followerUserId.HasValue) cmd.Parameters.AddWithValue("@FollowerUserId", followerUserId.Value);
        if (!string.IsNullOrWhiteSpace(statusFilter))
            cmd.Parameters.AddWithValue("@Status", NormalizeStatus(statusFilter)!);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(MapRow(r));
        }

        foreach (var item in list)
            item.ShopImageUrl = BuildShopImageUrl(item.ShopImageKey);

        return list;
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var s = status.Trim();
        if (s.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || s.Equals("done", StringComparison.OrdinalIgnoreCase)
            || s == "تمت المتابعة")
            return LegalFollowupStatuses.Completed;
        if (s.Equals("inprogress", StringComparison.OrdinalIgnoreCase)
            || s.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
            || s.Equals("open", StringComparison.OrdinalIgnoreCase)
            || s == "قيد العمل")
            return LegalFollowupStatuses.InProgress;
        if (s.Equals(LegalFollowupStatuses.Completed, StringComparison.OrdinalIgnoreCase)
            || s.Equals(LegalFollowupStatuses.InProgress, StringComparison.OrdinalIgnoreCase))
            return s.Equals(LegalFollowupStatuses.Completed, StringComparison.OrdinalIgnoreCase)
                ? LegalFollowupStatuses.Completed
                : LegalFollowupStatuses.InProgress;
        return null;
    }

    private static LegalFollowupCaseDTO MapRow(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        SaleId = r.IsDBNull(1) ? 0 : Convert.ToInt32(r.GetValue(1)),
        CustomerId = r.IsDBNull(2) ? null : r.GetInt32(2),
        FollowerUserId = r.GetInt32(3),
        FollowerName = r.IsDBNull(4) ? "" : r.GetString(4),
        Status = r.IsDBNull(5) ? LegalFollowupStatuses.InProgress : r.GetString(5),
        CustomerName = r.IsDBNull(6) ? "" : r.GetString(6),
        CustomerPhone = r.IsDBNull(7) ? null : r.GetString(7),
        CustomerAddress = r.IsDBNull(8) ? null : r.GetString(8),
        Latitude = r.IsDBNull(9) ? null : Convert.ToDouble(r.GetValue(9)),
        Longitude = r.IsDBNull(10) ? null : Convert.ToDouble(r.GetValue(10)),
        ShopImageKey = r.IsDBNull(11) ? null : r.GetString(11),
        CreatedByUserId = r.GetInt32(12),
        CreatedByName = r.IsDBNull(13) ? null : r.GetString(13),
        CreatedAtUtc = r.GetDateTime(14),
        CompletedAtUtc = r.IsDBNull(15) ? null : r.GetDateTime(15),
        Note = r.IsDBNull(16) ? null : r.GetString(16),
        CompletionNote = r.IsDBNull(17) ? null : r.GetString(17),
        SalesRequestId = r.FieldCount > 18 && !r.IsDBNull(18) ? r.GetInt32(18) : null
    };

    private SqlConnection Open()
    {
        var cs = _guard.GetSalesConnectionString()
                 ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
        var conn = new SqlConnection(cs);
        conn.Open();
        return conn;
    }

    private void EnsureSql()
    {
        var cs = _guard.GetSalesConnectionString()
                 ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
        using var conn = new SqlConnection(cs);
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
  IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LegalFollowupCases_SalesRequestId' AND object_id = OBJECT_ID(N'dbo.LegalFollowupCases'))
    CREATE INDEX IX_LegalFollowupCases_SalesRequestId ON dbo.LegalFollowupCases(SalesRequestId);
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
    }
}
