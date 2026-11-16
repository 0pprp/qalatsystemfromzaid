-- v3: the app is for field advertisers (agents). Accounts are created from the dashboard with a username and
-- password; orders carry a mandatory shop photo + location; sales are followed until fully paid; agents record
-- working days with a live location trail. Times are UTC; work dates are Iraq dates (UTC+3).

ALTER TABLE agents.Users ADD
    Username          VARCHAR(50)    NULL,
    PasswordHash      VARCHAR(200)   NULL,
    PasswordChangedAt DATETIME2      NULL,
    CreatedByAdmin    NVARCHAR(100)  NULL;
GO
CREATE UNIQUE INDEX UX_Users_Username ON agents.Users(Username) WHERE Username IS NOT NULL;

ALTER TABLE agents.Orders ADD
    ShopPhotoUrl        NVARCHAR(500) NULL,
    SaleId              INT           NULL,
    SaleTotal           BIGINT        NULL,
    SalePaid            BIGINT        NULL,
    SalePaymentsCount   INT           NULL,
    SaleLastPaymentAt   DATETIME2     NULL,
    SaleProgressSyncAt  DATETIME2     NULL,
    FinishedAt          DATETIME2     NULL;
GO
CREATE INDEX IX_Orders_SaleProgress ON agents.Orders(Status, SaleProgressSyncAt) WHERE Status = 'Sold';

-- One row per started «دوام». An agent may start/finish several times a day; the day total is the sum.
CREATE TABLE agents.WorkSessions (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkSessions PRIMARY KEY,
    UserId      BIGINT        NOT NULL CONSTRAINT FK_WorkSessions_User REFERENCES agents.Users(Id),
    WorkDate    DATE          NOT NULL,
    StartedAt   DATETIME2     NOT NULL,
    EndedAt     DATETIME2     NULL,
    StartLat    DECIMAL(9,6)  NULL,
    StartLng    DECIMAL(9,6)  NULL,
    EndLat      DECIMAL(9,6)  NULL,
    EndLng      DECIMAL(9,6)  NULL,
    EndedBy     VARCHAR(10)   NULL, -- user | auto
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_WorkSessions_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_WorkSessions_OpenPerUser ON agents.WorkSessions(UserId) WHERE EndedAt IS NULL;
CREATE INDEX IX_WorkSessions_Date ON agents.WorkSessions(WorkDate, UserId);

CREATE TABLE agents.LocationPoints (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LocationPoints PRIMARY KEY,
    UserId      BIGINT        NOT NULL,
    SessionId   BIGINT        NOT NULL CONSTRAINT FK_LocationPoints_Session REFERENCES agents.WorkSessions(Id),
    Latitude    DECIMAL(9,6)  NOT NULL,
    Longitude   DECIMAL(9,6)  NOT NULL,
    Accuracy    REAL          NULL,
    Speed       REAL          NULL,
    Battery     TINYINT       NULL,
    RecordedAt  DATETIME2     NOT NULL,
    ReceivedAt  DATETIME2     NOT NULL CONSTRAINT DF_LocationPoints_ReceivedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_LocationPoints_Session ON agents.LocationPoints(SessionId, RecordedAt);
CREATE INDEX IX_LocationPoints_UserTime ON agents.LocationPoints(UserId, RecordedAt DESC);

-- Commissions are hidden in the app until the scheme is reworked.
IF NOT EXISTS (SELECT 1 FROM agents.AppSettings WHERE [Key] = 'CommissionsVisible')
    INSERT agents.AppSettings([Key], [Value]) VALUES ('CommissionsVisible', 'false');
