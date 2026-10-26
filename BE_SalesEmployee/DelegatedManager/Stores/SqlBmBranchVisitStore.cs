using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using Dapper;

namespace BE_SalesEmployee.DelegatedManager.Stores;

public sealed class SqlBmBranchVisitStore : IBmBranchVisitStore
{
    private static readonly object SchemaGate = new();
    private static int _schemaReady;
    private readonly SqlGatewayConnectionFactory _factory;

    public SqlBmBranchVisitStore(SqlGatewayConnectionFactory factory) => _factory = factory;

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1) return;
        lock (SchemaGate)
        {
            if (_schemaReady == 1) return;
        }

        using var connection = await _factory.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
IF OBJECT_ID(N'dbo.BmBranchVisits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmBranchVisits (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BmBranchVisits PRIMARY KEY,
        CityValue NVARCHAR(64) NOT NULL,
        CityName NVARCHAR(128) NULL,
        OpenedByUserId UNIQUEIDENTIFIER NOT NULL,
        OpenedByUserName NVARCHAR(128) NOT NULL,
        OpenedByDisplayName NVARCHAR(256) NOT NULL,
        ArrivedAtUtc DATETIME2(3) NOT NULL,
        DepartedAtUtc DATETIME2(3) NULL,
        Status NVARCHAR(32) NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        CreatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_BmVisits_Created DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF_BmVisits_Updated DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_BmBranchVisits_OpenUser ON dbo.BmBranchVisits (OpenedByUserId, Status);
    CREATE INDEX IX_BmBranchVisits_CityArrived ON dbo.BmBranchVisits (CityValue, ArrivedAtUtc DESC);
END

IF OBJECT_ID(N'dbo.BmBranchVisitMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmBranchVisitMembers (
        VisitId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(256) NOT NULL,
        CONSTRAINT PK_BmBranchVisitMembers PRIMARY KEY (VisitId, UserId),
        CONSTRAINT FK_BmVisitMembers_Visit FOREIGN KEY (VisitId) REFERENCES dbo.BmBranchVisits(Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.BmBranchVisitDelegateRatings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmBranchVisitDelegateRatings (
        VisitId UNIQUEIDENTIFIER NOT NULL,
        DelegateId INT NOT NULL,
        DelegateName NVARCHAR(256) NOT NULL,
        Score INT NOT NULL,
        RatedByUserId UNIQUEIDENTIFIER NOT NULL,
        RatedByDisplayName NVARCHAR(256) NULL,
        RatedAtUtc DATETIME2(3) NOT NULL,
        CONSTRAINT PK_BmBranchVisitDelegateRatings PRIMARY KEY (VisitId, DelegateId),
        CONSTRAINT FK_BmVisitRatings_Visit FOREIGN KEY (VisitId) REFERENCES dbo.BmBranchVisits(Id) ON DELETE CASCADE
    );
END

IF OBJECT_ID(N'dbo.BmBranchVisitAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BmBranchVisitAttachments (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BmBranchVisitAttachments PRIMARY KEY,
        VisitId UNIQUEIDENTIFIER NOT NULL,
        FileName NVARCHAR(512) NOT NULL,
        StoredFileName NVARCHAR(512) NOT NULL,
        ContentType NVARCHAR(128) NULL,
        SizeBytes BIGINT NOT NULL,
        UploadedAtUtc DATETIME2(3) NOT NULL,
        UploadedBy NVARCHAR(256) NULL,
        CONSTRAINT FK_BmVisitAtt_Visit FOREIGN KEY (VisitId) REFERENCES dbo.BmBranchVisits(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_BmVisitAtt_Visit ON dbo.BmBranchVisitAttachments (VisitId);
END
""", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task<BmBranchVisit?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        var visit = await c.QueryFirstOrDefaultAsync<BmBranchVisit>(new CommandDefinition(
            "SELECT TOP 1 * FROM dbo.BmBranchVisits WHERE Id = @Id", new { Id = id }, cancellationToken: ct));
        if (visit is null) return null;
        await AttachChildrenAsync(c, visit, ct);
        return visit;
    }

    public async Task<BmBranchVisit?> GetOpenForUserAsync(Guid openedByUserId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        var visit = await c.QueryFirstOrDefaultAsync<BmBranchVisit>(new CommandDefinition("""
SELECT TOP 1 * FROM dbo.BmBranchVisits
WHERE OpenedByUserId = @UserId AND Status = N'Open'
ORDER BY ArrivedAtUtc DESC;
""", new { UserId = openedByUserId }, cancellationToken: ct));
        if (visit is null) return null;
        await AttachChildrenAsync(c, visit, ct);
        return visit;
    }

    public async Task<IReadOnlyList<BmBranchVisit>> ListAsync(
        string? cityValue,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        var limit = Math.Clamp(take <= 0 ? 100 : take, 1, 500);
        var rows = (await c.QueryAsync<BmBranchVisit>(new CommandDefinition("""
SELECT TOP (@Take) * FROM dbo.BmBranchVisits
WHERE (@City IS NULL OR CityValue = @City)
  AND (@FromUtc IS NULL OR ArrivedAtUtc >= @FromUtc)
  AND (@ToUtc IS NULL OR ArrivedAtUtc <= @ToUtc)
ORDER BY ArrivedAtUtc DESC;
""", new
        {
            Take = limit,
            City = string.IsNullOrWhiteSpace(cityValue) ? null : cityValue.Trim(),
            FromUtc = fromUtc,
            ToUtc = toUtc,
        }, cancellationToken: ct))).ToList();

        foreach (var v in rows)
            await AttachChildrenAsync(c, v, ct);
        return rows;
    }

    public async Task<BmBranchVisit> InsertAsync(BmBranchVisit visit, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        if (visit.Id == Guid.Empty) visit.Id = Guid.NewGuid();
        await c.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.BmBranchVisits
    (Id, CityValue, CityName, OpenedByUserId, OpenedByUserName, OpenedByDisplayName,
     ArrivedAtUtc, DepartedAtUtc, Status, Notes, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@Id, @CityValue, @CityName, @OpenedByUserId, @OpenedByUserName, @OpenedByDisplayName,
     @ArrivedAtUtc, @DepartedAtUtc, @Status, @Notes, @CreatedAtUtc, @UpdatedAtUtc);
""", visit, cancellationToken: ct));

        foreach (var m in visit.Members)
        {
            await c.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.BmBranchVisitMembers (VisitId, UserId, DisplayName)
VALUES (@VisitId, @UserId, @DisplayName);
""", new { VisitId = visit.Id, m.UserId, m.DisplayName }, cancellationToken: ct));
        }

        return (await GetAsync(visit.Id, ct))!;
    }

    public async Task<BmBranchVisit?> UpdateAsync(BmBranchVisit visit, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        var n = await c.ExecuteAsync(new CommandDefinition("""
UPDATE dbo.BmBranchVisits SET
    DepartedAtUtc = @DepartedAtUtc,
    Status = @Status,
    Notes = @Notes,
    UpdatedAtUtc = @UpdatedAtUtc
WHERE Id = @Id;
""", visit, cancellationToken: ct));
        if (n == 0) return null;
        return await GetAsync(visit.Id, ct);
    }

    public async Task UpsertDelegateRatingAsync(BmBranchVisitDelegateRating rating, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        await c.ExecuteAsync(new CommandDefinition("""
MERGE dbo.BmBranchVisitDelegateRatings AS t
USING (SELECT @VisitId AS VisitId, @DelegateId AS DelegateId) AS s
ON t.VisitId = s.VisitId AND t.DelegateId = s.DelegateId
WHEN MATCHED THEN UPDATE SET
    DelegateName = @DelegateName, Score = @Score,
    RatedByUserId = @RatedByUserId, RatedByDisplayName = @RatedByDisplayName, RatedAtUtc = @RatedAtUtc
WHEN NOT MATCHED THEN INSERT
    (VisitId, DelegateId, DelegateName, Score, RatedByUserId, RatedByDisplayName, RatedAtUtc)
VALUES
    (@VisitId, @DelegateId, @DelegateName, @Score, @RatedByUserId, @RatedByDisplayName, @RatedAtUtc);

UPDATE dbo.BmBranchVisits SET UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @VisitId;
""", rating, cancellationToken: ct));
    }

    public async Task AddAttachmentAsync(BmBranchVisitAttachment attachment, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        if (attachment.Id == Guid.Empty) attachment.Id = Guid.NewGuid();
        await c.ExecuteAsync(new CommandDefinition("""
INSERT INTO dbo.BmBranchVisitAttachments
    (Id, VisitId, FileName, StoredFileName, ContentType, SizeBytes, UploadedAtUtc, UploadedBy)
VALUES
    (@Id, @VisitId, @FileName, @StoredFileName, @ContentType, @SizeBytes, @UploadedAtUtc, @UploadedBy);
UPDATE dbo.BmBranchVisits SET UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @VisitId;
""", attachment, cancellationToken: ct));
    }

    public async Task<BmBranchVisitAttachment?> GetAttachmentAsync(Guid visitId, Guid attachmentId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        using var c = await _factory.OpenAsync(ct);
        return await c.QueryFirstOrDefaultAsync<BmBranchVisitAttachment>(new CommandDefinition("""
SELECT TOP 1 * FROM dbo.BmBranchVisitAttachments WHERE VisitId = @VisitId AND Id = @Id;
""", new { VisitId = visitId, Id = attachmentId }, cancellationToken: ct));
    }

    private static async Task AttachChildrenAsync(System.Data.IDbConnection c, BmBranchVisit visit, CancellationToken ct)
    {
        var members = (await c.QueryAsync<BmBranchVisitMember>(new CommandDefinition(
            "SELECT * FROM dbo.BmBranchVisitMembers WHERE VisitId = @Id", new { visit.Id }, cancellationToken: ct))).ToList();
        var ratings = (await c.QueryAsync<BmBranchVisitDelegateRating>(new CommandDefinition(
            "SELECT * FROM dbo.BmBranchVisitDelegateRatings WHERE VisitId = @Id", new { visit.Id }, cancellationToken: ct))).ToList();
        var atts = (await c.QueryAsync<BmBranchVisitAttachment>(new CommandDefinition(
            "SELECT * FROM dbo.BmBranchVisitAttachments WHERE VisitId = @Id ORDER BY UploadedAtUtc", new { visit.Id }, cancellationToken: ct))).ToList();
        visit.Members = members;
        visit.DelegateRatings = ratings;
        visit.Attachments = atts;
    }
}
