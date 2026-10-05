using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services;

public sealed class FollowerComplaintCreateResult
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public interface IFollowerComplaintsService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<FollowerComplaintCreateResult> CreateAsync(
        int userId,
        string? senderDisplayName,
        int? cityId,
        string? branchLink,
        string messageText,
        CancellationToken ct = default);
}

/// <summary>
/// Write-only complaints from authenticated followers (Users.UserType = متابع).
/// AsyncId is auth-only and never persisted.
/// </summary>
public sealed class FollowerComplaintsService : IFollowerComplaintsService
{
    public const int MinMessageLength = 10;
    public const int MaxMessageLength = 2000;

    private readonly string _cs;

    public FollowerComplaintsService(IConfiguration configuration)
    {
        _cs = configuration.GetConnectionString("DataBaseConnection")
              ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var c = new SqlConnection(_cs);
        await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
    }

    public async Task<FollowerComplaintCreateResult> CreateAsync(
        int userId,
        string? senderDisplayName,
        int? cityId,
        string? branchLink,
        string messageText,
        CancellationToken ct = default)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("هوية المتابع غير صالحة");
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

        text = text.Replace('<', ' ').Replace('>', ' ');

        await EnsureSchemaAsync(ct);
        await using var c = new SqlConnection(_cs);
        var row = await c.QueryFirstAsync<FollowerComplaintCreateResult>(new CommandDefinition(@"
INSERT INTO dbo.FollowerComplaints
    (UserId, SenderDisplayName, CityId, BranchLink, MessageText, Status, CreatedAtUtc)
OUTPUT INSERTED.Id, INSERTED.CreatedAtUtc
VALUES
    (@UserId, @SenderDisplayName, @CityId, @BranchLink, @MessageText, N'New', SYSUTCDATETIME());",
            new
            {
                UserId = userId,
                SenderDisplayName = string.IsNullOrWhiteSpace(senderDisplayName)
                    ? "متابع"
                    : senderDisplayName.Trim(),
                CityId = cityId is > 0 ? cityId : null,
                BranchLink = string.IsNullOrWhiteSpace(branchLink) ? null : branchLink.Trim(),
                MessageText = text,
            },
            cancellationToken: ct));
        return row;
    }

    private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.FollowerComplaints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerComplaints (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerComplaints PRIMARY KEY,
        UserId INT NOT NULL,
        SenderDisplayName NVARCHAR(200) NOT NULL,
        CityId INT NULL,
        BranchLink NVARCHAR(500) NULL,
        MessageText NVARCHAR(2000) NOT NULL,
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_FollowerComplaints_Status DEFAULT (N'New'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerComplaints_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        ReadAtUtc DATETIME2 NULL,
        ResolvedAtUtc DATETIME2 NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FollowerComplaints_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.FollowerComplaints'))
    CREATE INDEX IX_FollowerComplaints_CreatedAtUtc ON dbo.FollowerComplaints (CreatedAtUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FollowerComplaints_UserId' AND object_id = OBJECT_ID(N'dbo.FollowerComplaints'))
    CREATE INDEX IX_FollowerComplaints_UserId ON dbo.FollowerComplaints (UserId);
";
}
