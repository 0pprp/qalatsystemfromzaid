-- Idempotent: Delegated Manager central inbox, sales exceptions, mobile updates.
-- Target: ConnectionStrings:SalesGateway (gateway SQL). Do NOT run against Production automatically.
-- Safe to re-run.

SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.CentralComplaints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CentralComplaints
    (
        Id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CentralComplaints PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        SourceApp           NVARCHAR(64)     NOT NULL,
        SourceType          NVARCHAR(64)     NULL,
        SenderUserId        NVARCHAR(64)     NULL,
        SenderUserName      NVARCHAR(128)    NULL,
        SenderDisplayName   NVARCHAR(256)    NOT NULL,
        SenderRole          NVARCHAR(128)    NOT NULL,
        CityValue           NVARCHAR(64)     NULL,
        CityName            NVARCHAR(128)    NULL,
        Subject             NVARCHAR(256)    NOT NULL,
        Body                NVARCHAR(MAX)    NOT NULL,
        Status              NVARCHAR(32)     NOT NULL CONSTRAINT DF_CentralComplaints_Status DEFAULT (N'Unread'),
        CreatedAtUtc        DATETIME2(3)     NOT NULL CONSTRAINT DF_CentralComplaints_Created DEFAULT (SYSUTCDATETIME()),
        ReadAtUtc           DATETIME2(3)     NULL,
        MetadataJson        NVARCHAR(MAX)    NULL,
        CONSTRAINT CK_CentralComplaints_Status CHECK (Status IN (N'Unread', N'Read'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CentralComplaints_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.CentralComplaints'))
    CREATE INDEX IX_CentralComplaints_CreatedAtUtc ON dbo.CentralComplaints(CreatedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CentralComplaints_Status_City' AND object_id = OBJECT_ID(N'dbo.CentralComplaints'))
    CREATE INDEX IX_CentralComplaints_Status_City ON dbo.CentralComplaints(Status, CityValue) INCLUDE (SourceApp, SenderRole, CreatedAtUtc);
GO

IF OBJECT_ID(N'dbo.SalesExceptionRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesExceptionRequests
    (
        Id                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SalesExceptionRequests PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        CityValue               NVARCHAR(64)     NOT NULL,
        CityName                NVARCHAR(128)    NULL,
        CustomerId              INT              NULL,
        CustomerName            NVARCHAR(256)    NULL,
        CustomerPhone           NVARCHAR(64)     NULL,
        SalesRequestId          INT              NULL,
        RequestingManagerUserName NVARCHAR(128)  NOT NULL,
        RequestingManagerDisplayName NVARCHAR(256) NOT NULL,
        Reason                  NVARCHAR(MAX)    NOT NULL,
        TargetApproverType      NVARCHAR(64)     NOT NULL CONSTRAINT DF_SalesException_Target DEFAULT (N'DelegatedManager'),
        Status                  NVARCHAR(32)     NOT NULL CONSTRAINT DF_SalesException_Status DEFAULT (N'Pending'),
        RequestedAtUtc          DATETIME2(3)     NOT NULL CONSTRAINT DF_SalesException_Requested DEFAULT (SYSUTCDATETIME()),
        DecidedAtUtc            DATETIME2(3)     NULL,
        DecisionMakerUserName   NVARCHAR(128)    NULL,
        DecisionMakerDisplayName NVARCHAR(256)   NULL,
        DecisionNote            NVARCHAR(MAX)    NULL,
        BranchCustomerNotePosted BIT             NOT NULL CONSTRAINT DF_SalesException_NotePosted DEFAULT (0),
        CONSTRAINT CK_SalesException_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected', N'Cancelled')),
        CONSTRAINT CK_SalesException_Target CHECK (TargetApproverType IN (N'DelegatedManager', N'BranchManager'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesExceptionRequests_Status_Requested' AND object_id = OBJECT_ID(N'dbo.SalesExceptionRequests'))
    CREATE INDEX IX_SalesExceptionRequests_Status_Requested ON dbo.SalesExceptionRequests(Status, RequestedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesExceptionRequests_City_Status' AND object_id = OBJECT_ID(N'dbo.SalesExceptionRequests'))
    CREATE INDEX IX_SalesExceptionRequests_City_Status ON dbo.SalesExceptionRequests(CityValue, Status);
GO

IF OBJECT_ID(N'dbo.SalesExceptionAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesExceptionAudit
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesExceptionAudit PRIMARY KEY,
        ExceptionRequestId  UNIQUEIDENTIFIER NOT NULL,
        ActorUserName       NVARCHAR(128)    NOT NULL,
        ActorDisplayName    NVARCHAR(256)    NOT NULL,
        ActorRole           NVARCHAR(128)    NOT NULL,
        PreviousStatus      NVARCHAR(32)     NULL,
        NewStatus           NVARCHAR(32)     NOT NULL,
        DecisionNote        NVARCHAR(MAX)    NULL,
        CityValue           NVARCHAR(64)     NULL,
        CreatedAtUtc        DATETIME2(3)     NOT NULL CONSTRAINT DF_SalesExceptionAudit_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_SalesExceptionAudit_Request FOREIGN KEY (ExceptionRequestId)
            REFERENCES dbo.SalesExceptionRequests(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesExceptionAudit_Request' AND object_id = OBJECT_ID(N'dbo.SalesExceptionAudit'))
    CREATE INDEX IX_SalesExceptionAudit_Request ON dbo.SalesExceptionAudit(ExceptionRequestId, CreatedAtUtc);
GO

IF OBJECT_ID(N'dbo.MobileAppReleases', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MobileAppReleases
    (
        AppKey                      NVARCHAR(64)  NOT NULL,
        LatestVersionName           NVARCHAR(32)  NOT NULL,
        LatestVersionCode           INT           NOT NULL,
        MinimumSupportedVersionCode INT           NOT NULL,
        ForceUpdate                 BIT           NOT NULL CONSTRAINT DF_MobileAppReleases_Force DEFAULT (0),
        ApkUrl                      NVARCHAR(1024) NOT NULL,
        Sha256                      NVARCHAR(128) NOT NULL,
        ReleaseNotes                NVARCHAR(MAX) NULL,
        UpdatedAtUtc                DATETIME2(3)  NOT NULL CONSTRAINT DF_MobileAppReleases_Updated DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_MobileAppReleases PRIMARY KEY (AppKey)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.MobileAppReleases WHERE AppKey = N'delegated-manager')
BEGIN
    INSERT INTO dbo.MobileAppReleases
        (AppKey, LatestVersionName, LatestVersionCode, MinimumSupportedVersionCode, ForceUpdate, ApkUrl, Sha256, ReleaseNotes)
    VALUES
        (N'delegated-manager', N'1.0.0', 1, 1, 0,
         N'https://example.invalid/delegated-manager.apk',
         N'0000000000000000000000000000000000000000000000000000000000000000',
         N'إصدار أولي');
END
GO
