-- Customer waiver requests (طلب تنازل) — gateway SQL (SalesGateway).
-- Idempotent. Safe to re-run.
--
-- Impact analysis:
-- - BE_SalesEmployee (gateway): CustomerWaiverRequests store + DM decide + internal BM create
-- - BE_Company (branch): BM create/list forwarder; waiver-review-context; apply payment + DecisionNotifications
-- - FE_Company: accountant NavBarNotifications shows DecisionType تنازل (unread until Read)
-- - Flutter BM: زر طلب تنازل + سجل (Pending/Rejected/Approved)
-- - Flutter DM: inbox تنازل + ملاحظة مميزة + موافقة بمبلغ إجباري
-- - DB: gateway tables only; branch uses existing CustomersPayments + CustomerWeekDecisions + DecisionNotifications
-- - No change to BE_SalesEmployee SM exception path beyond shared DecisionRequest DTO field
SET NOCOUNT ON;
GO

IF OBJECT_ID(N'dbo.CustomerWaiverRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerWaiverRequests
    (
        Id                              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CustomerWaiverRequests PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        CityValue                       NVARCHAR(64)     NOT NULL,
        CityName                        NVARCHAR(128)    NULL,
        CustomerId                      INT              NOT NULL,
        CustomerName                    NVARCHAR(256)    NULL,
        CustomerPhone                   NVARCHAR(64)     NULL,
        BranchManagerUserName           NVARCHAR(128)    NOT NULL,
        BranchManagerDisplayName        NVARCHAR(256)    NOT NULL,
        BranchManagerUserId             INT              NULL,
        Note                            NVARCHAR(MAX)    NOT NULL,
        Status                          NVARCHAR(32)     NOT NULL CONSTRAINT DF_CustomerWaiver_Status DEFAULT (N'Pending'),
        RequestedAtUtc                  DATETIME2(3)     NOT NULL CONSTRAINT DF_CustomerWaiver_Requested DEFAULT (SYSUTCDATETIME()),
        DecidedAtUtc                    DATETIME2(3)     NULL,
        DecisionMakerUserName           NVARCHAR(128)    NULL,
        DecisionMakerDisplayName        NVARCHAR(256)    NULL,
        DecisionNote                    NVARCHAR(MAX)    NULL,
        ApprovedAmount                  FLOAT            NULL,
        PaymentPosted                   BIT              NOT NULL CONSTRAINT DF_CustomerWaiver_PaymentPosted DEFAULT (0),
        AccountantNotified              BIT              NOT NULL CONSTRAINT DF_CustomerWaiver_AcctNotified DEFAULT (0),
        CONSTRAINT CK_CustomerWaiver_Status CHECK (Status IN (N'Pending', N'Approved', N'Rejected'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerWaiverRequests_Status_Requested' AND object_id = OBJECT_ID(N'dbo.CustomerWaiverRequests'))
    CREATE INDEX IX_CustomerWaiverRequests_Status_Requested ON dbo.CustomerWaiverRequests(Status, RequestedAtUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerWaiverRequests_City_Status' AND object_id = OBJECT_ID(N'dbo.CustomerWaiverRequests'))
    CREATE INDEX IX_CustomerWaiverRequests_City_Status ON dbo.CustomerWaiverRequests(CityValue, Status);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerWaiverRequests_BmUser' AND object_id = OBJECT_ID(N'dbo.CustomerWaiverRequests'))
    CREATE INDEX IX_CustomerWaiverRequests_BmUser ON dbo.CustomerWaiverRequests(BranchManagerUserName, Status, RequestedAtUtc DESC);
GO

IF OBJECT_ID(N'dbo.CustomerWaiverAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerWaiverAudit
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerWaiverAudit PRIMARY KEY,
        WaiverId            UNIQUEIDENTIFIER NOT NULL,
        Action              NVARCHAR(64)     NOT NULL,
        ActorUserName       NVARCHAR(128)    NULL,
        ActorDisplayName    NVARCHAR(256)    NULL,
        DetailJson          NVARCHAR(MAX)    NULL,
        CreatedAtUtc        DATETIME2(3)     NOT NULL CONSTRAINT DF_CustomerWaiverAudit_Created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_CustomerWaiverAudit_Request FOREIGN KEY (WaiverId) REFERENCES dbo.CustomerWaiverRequests(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerWaiverAudit_Waiver' AND object_id = OBJECT_ID(N'dbo.CustomerWaiverAudit'))
    CREATE INDEX IX_CustomerWaiverAudit_Waiver ON dbo.CustomerWaiverAudit(WaiverId, CreatedAtUtc DESC);
GO
