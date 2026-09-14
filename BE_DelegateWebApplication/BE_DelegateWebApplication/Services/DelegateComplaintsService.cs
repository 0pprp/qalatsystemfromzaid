using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public sealed class DelegateComplaintDto
{
    public long Id { get; set; }
    public int DelegateId { get; set; }
    public int? UserId { get; set; }
    public string SenderDisplayName { get; set; } = "";
    public int? CityId { get; set; }
    public string? BranchLink { get; set; }
    public string MessageText { get; set; } = "";
    public string Status { get; set; } = "New";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public sealed class DelegateComplaintCreateResult
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public interface IDelegateComplaintsService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<DelegateComplaintCreateResult> CreateAsync(
        int delegateId,
        int? userId,
        string? senderDisplayName,
        int? cityId,
        string? branchLink,
        string messageText,
        CancellationToken ct = default);
}

/// <summary>
/// Write-only complaints from authenticated delegates.
/// No public read API yet — reserved for future authorized-manager app.
/// AsyncId is used only for auth and is never persisted.
/// </summary>
public sealed class DelegateComplaintsService : IDelegateComplaintsService
{
    public const int MinMessageLength = 10;
    public const int MaxMessageLength = 2000;

    private readonly string _cs;

    public DelegateComplaintsService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var c = new SqlConnection(_cs);
        await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
    }

    public async Task<DelegateComplaintCreateResult> CreateAsync(
        int delegateId,
        int? userId,
        string? senderDisplayName,
        int? cityId,
        string? branchLink,
        string messageText,
        CancellationToken ct = default)
    {
        if (delegateId <= 0)
        {
            throw new ArgumentException("هوية المندوب غير صالحة");
        }

        var text = (messageText ?? string.Empty).Trim();
        if (text.Length < MinMessageLength)
        {
            throw new ArgumentException($"نص الشكوى قصير جدًا (الحد الأدنى {MinMessageLength} أحرف)");
        }

        if (text.Length > MaxMessageLength)
        {
            throw new ArgumentException($"الحد الأقصى للشكوى {MaxMessageLength} حرف");
        }

        // Strip angle brackets to reduce stored XSS payload usefulness for future UIs.
        text = text.Replace('<', ' ').Replace('>', ' ');

        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        var row = await c.QueryFirstAsync<DelegateComplaintCreateResult>(new CommandDefinition(@"
INSERT INTO dbo.DelegateComplaints
    (DelegateId, UserId, SenderDisplayName, CityId, BranchLink, MessageText, Status, CreatedAtUtc)
OUTPUT INSERTED.Id, INSERTED.CreatedAtUtc
VALUES
    (@DelegateId, @UserId, @SenderDisplayName, @CityId, @BranchLink, @MessageText, N'New', SYSUTCDATETIME());",
            new
            {
                DelegateId = delegateId,
                UserId = userId is > 0 ? userId : null,
                SenderDisplayName = string.IsNullOrWhiteSpace(senderDisplayName)
                    ? "مندوب"
                    : senderDisplayName.Trim(),
                CityId = cityId is > 0 ? cityId : null,
                BranchLink = string.IsNullOrWhiteSpace(branchLink) ? null : branchLink.Trim(),
                MessageText = text,
            },
            cancellationToken: ct));
        return row;
    }

    private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.DelegateComplaints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DelegateComplaints (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DelegateComplaints PRIMARY KEY,
        DelegateId INT NOT NULL,
        UserId INT NULL,
        SenderDisplayName NVARCHAR(200) NOT NULL,
        CityId INT NULL,
        BranchLink NVARCHAR(500) NULL,
        MessageText NVARCHAR(2000) NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_DelegateComplaints_Status DEFAULT (N'New'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_DelegateComplaints_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ReadAtUtc DATETIME2 NULL,
        ResolvedAtUtc DATETIME2 NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DelegateComplaints_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.DelegateComplaints'))
    CREATE INDEX IX_DelegateComplaints_CreatedAtUtc ON dbo.DelegateComplaints (CreatedAtUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DelegateComplaints_DelegateId' AND object_id = OBJECT_ID(N'dbo.DelegateComplaints'))
    CREATE INDEX IX_DelegateComplaints_DelegateId ON dbo.DelegateComplaints (DelegateId);
";
}
