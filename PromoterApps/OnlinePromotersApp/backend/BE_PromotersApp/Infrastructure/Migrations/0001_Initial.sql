-- Online promoters app («المروّجون أونلاين»). Everything lives in the `promoters` schema, so the app can share the
-- company database without touching its dbo tables. Money is whole Iraqi dinars (BIGINT).

CREATE TABLE promoters.Users (
    Id                BIGINT IDENTITY(1,1) PRIMARY KEY,
    Username          NVARCHAR(60)  NOT NULL,
    PasswordHash      VARCHAR(200)  NOT NULL,
    Phone             VARCHAR(20)   NOT NULL,
    -- Set when another promoter created this account («حساب فرعي»); NULL for accounts made in the dashboard.
    ParentUserId      BIGINT        NULL REFERENCES promoters.Users(Id),
    -- Share of this account's commission that goes to the parent (0–100). Ignored when IsFull = 1.
    ParentCutPercent  DECIMAL(5,2)  NOT NULL DEFAULT 0,
    -- Full account: keeps 100% of its commission and may create sub-accounts.
    IsFull            BIT           NOT NULL DEFAULT 0,
    IsSuspended       BIT           NOT NULL DEFAULT 0,
    SuspendReason     NVARCHAR(300) NULL,
    CreatedByAdmin    NVARCHAR(100) NULL,
    PasswordChangedAt DATETIME2     NULL,
    LastLoginAt       DATETIME2     NULL,
    CreatedAt         DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_Users_Username ON promoters.Users(Username);
CREATE INDEX IX_Users_Parent ON promoters.Users(ParentUserId);

CREATE TABLE promoters.RefreshTokens (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId    BIGINT        NOT NULL REFERENCES promoters.Users(Id),
    TokenHash VARBINARY(32) NOT NULL,
    ExpiresAt DATETIME2     NOT NULL,
    RevokedAt DATETIME2     NULL,
    CreatedAt DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_RefreshTokens_Hash ON promoters.RefreshTokens(TokenHash);
CREATE INDEX IX_RefreshTokens_User ON promoters.RefreshTokens(UserId);

-- What promoters can sell: only a name, a price and the promoter's commission per unit.
CREATE TABLE promoters.Products (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    Name       NVARCHAR(100) NOT NULL,
    Price      BIGINT        NOT NULL,
    Commission BIGINT        NOT NULL,
    IsActive   BIT           NOT NULL DEFAULT 1,
    IsDeleted  BIT           NOT NULL DEFAULT 0,
    SortOrder  INT           NOT NULL DEFAULT 0,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);

-- A sale order. Product, price and both commission shares are copied when the order is made, so later changes to the
-- product or to the parent's cut never change an existing order.
CREATE TABLE promoters.Orders (
    Id                 BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderUuid          UNIQUEIDENTIFIER NOT NULL,
    OrderNumber        VARCHAR(30)    NULL,
    UserId             BIGINT         NOT NULL REFERENCES promoters.Users(Id),
    IdempotencyKey     VARCHAR(64)    NULL,
    ProductId          INT            NOT NULL REFERENCES promoters.Products(Id),
    ProductName        NVARCHAR(100)  NOT NULL,
    UnitPrice          BIGINT         NOT NULL,
    Quantity           INT            NOT NULL,
    CommissionTotal    BIGINT         NOT NULL,
    SellerCommission   BIGINT         NOT NULL,
    ParentUserId       BIGINT         NULL,
    ParentCutPercent   DECIMAL(5,2)   NOT NULL DEFAULT 0,
    ParentCommission   BIGINT         NOT NULL DEFAULT 0,
    CityValue          INT            NOT NULL,
    CityName           NVARCHAR(100)  NOT NULL,
    CustomerName       NVARCHAR(200)  NOT NULL,
    CustomerPhone      VARCHAR(20)    NOT NULL,
    Address            NVARCHAR(400)  NOT NULL,
    Notes              NVARCHAR(500)  NULL,
    Status             VARCHAR(30)    NOT NULL,
    PublicReason       NVARCHAR(400)  NULL,
    SalesRequestId     INT            NULL,
    SaleId             INT            NULL,
    BranchStatus       VARCHAR(40)    NULL,
    BranchRejectReason NVARCHAR(1000) NULL,
    LastSyncAt         DATETIME2      NULL,
    SoldAt             DATETIME2      NULL,
    CreatedAt          DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt          DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_Orders_Uuid ON promoters.Orders(OrderUuid);
CREATE UNIQUE INDEX UX_Orders_Idempotency ON promoters.Orders(UserId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
CREATE INDEX IX_Orders_User ON promoters.Orders(UserId, Id DESC);
CREATE INDEX IX_Orders_Status ON promoters.Orders(Status);
CREATE INDEX IX_Orders_Phone ON promoters.Orders(CustomerPhone);

CREATE TABLE promoters.OrderEvents (
    Id        BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderId   BIGINT        NOT NULL REFERENCES promoters.Orders(Id),
    Status    VARCHAR(30)   NOT NULL,
    Note      NVARCHAR(400) NULL,
    Actor     NVARCHAR(100) NULL,
    CreatedAt DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_OrderEvents_Order ON promoters.OrderEvents(OrderId);

-- Withdrawal requests. The amount leaves the balance when requested and comes back if the request is rejected.
CREATE TABLE promoters.Payouts (
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId        BIGINT        NOT NULL REFERENCES promoters.Users(Id),
    Amount        BIGINT        NOT NULL,
    Method        VARCHAR(20)   NOT NULL,      -- QiCard | ZainCash
    AccountNumber NVARCHAR(50)  NOT NULL,
    Status        VARCHAR(20)   NOT NULL,      -- Pending | Paid | Rejected
    AdminNote     NVARCHAR(400) NULL,
    DecidedBy     NVARCHAR(100) NULL,
    DecidedAt     DATETIME2     NULL,
    LastMessageAt DATETIME2     NULL,
    CreatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Payouts_User ON promoters.Payouts(UserId, Id DESC);
CREATE INDEX IX_Payouts_Status ON promoters.Payouts(Status);
-- One open request at a time per promoter.
CREATE UNIQUE INDEX UX_Payouts_OnePending ON promoters.Payouts(UserId) WHERE Status = 'Pending';

-- Follow-up chat of a withdrawal request between the promoter and the dashboard.
CREATE TABLE promoters.PayoutMessages (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    PayoutId   BIGINT         NOT NULL REFERENCES promoters.Payouts(Id),
    FromAdmin  BIT            NOT NULL,
    SenderName NVARCHAR(100)  NOT NULL,
    Body       NVARCHAR(1000) NOT NULL,
    ReadAt     DATETIME2      NULL,            -- read by the other side
    CreatedAt  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_PayoutMessages_Payout ON promoters.PayoutMessages(PayoutId, Id);

-- The wallet: every change to a promoter's balance is one row. Balance = SUM(Amount).
CREATE TABLE promoters.WalletEntries (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId     BIGINT        NOT NULL REFERENCES promoters.Users(Id),
    Amount     BIGINT        NOT NULL,
    Type       VARCHAR(30)   NOT NULL,         -- Sale | Team | SaleReversal | TeamReversal | Payout | PayoutReturned | Adjustment
    OrderId    BIGINT        NULL REFERENCES promoters.Orders(Id),
    FromUserId BIGINT        NULL REFERENCES promoters.Users(Id),
    PayoutId   BIGINT        NULL REFERENCES promoters.Payouts(Id),
    Note       NVARCHAR(300) NULL,
    CreatedBy  NVARCHAR(100) NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_WalletEntries_User ON promoters.WalletEntries(UserId, Id DESC);
CREATE INDEX IX_WalletEntries_From ON promoters.WalletEntries(UserId, FromUserId) WHERE FromUserId IS NOT NULL;
-- A sale credits (and a cancellation reverses) each account once.
CREATE UNIQUE INDEX UX_WalletEntries_Order ON promoters.WalletEntries(UserId, OrderId, Type) WHERE OrderId IS NOT NULL;
CREATE UNIQUE INDEX UX_WalletEntries_Payout ON promoters.WalletEntries(PayoutId, Type) WHERE PayoutId IS NOT NULL;

-- Marketing material from the dashboard: photos, videos and PDFs.
CREATE TABLE promoters.Materials (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    Title       NVARCHAR(150) NOT NULL,
    Kind        VARCHAR(10)   NOT NULL,        -- image | video | pdf
    FilePath    VARCHAR(400)  NOT NULL,        -- media/materials/…
    FileName    NVARCHAR(200) NOT NULL,
    ContentType VARCHAR(100)  NOT NULL,
    SizeBytes   BIGINT        NOT NULL,
    IsDeleted   BIT           NOT NULL DEFAULT 0,
    CreatedBy   NVARCHAR(100) NULL,
    CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE promoters.Broadcasts (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    Title      NVARCHAR(150)  NOT NULL,
    Body       NVARCHAR(1000) NOT NULL,
    Recipients INT            NOT NULL DEFAULT 0,
    CreatedBy  NVARCHAR(100)  NOT NULL,
    CreatedAt  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE promoters.Notifications (
    Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId      BIGINT         NOT NULL REFERENCES promoters.Users(Id),
    Type        VARCHAR(30)    NOT NULL,
    Title       NVARCHAR(150)  NOT NULL,
    Body        NVARCHAR(1000) NOT NULL,
    DataJson    NVARCHAR(1000) NULL,
    BroadcastId BIGINT         NULL REFERENCES promoters.Broadcasts(Id),
    ReadAt      DATETIME2      NULL,
    CreatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Notifications_User ON promoters.Notifications(UserId, Id DESC);

CREATE TABLE promoters.AppSettings (
    [Key]     VARCHAR(100)   NOT NULL PRIMARY KEY,
    [Value]   NVARCHAR(1000) NOT NULL,
    UpdatedAt DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE promoters.AuditLog (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    ActorName  NVARCHAR(100)  NOT NULL,
    ActorRole  NVARCHAR(50)   NOT NULL,
    Action     VARCHAR(60)    NOT NULL,
    Entity     VARCHAR(40)    NOT NULL,
    EntityId   VARCHAR(40)    NULL,
    BeforeJson NVARCHAR(MAX)  NULL,
    AfterJson  NVARCHAR(MAX)  NULL,
    Ip         VARCHAR(64)    NULL,
    CreatedAt  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_AuditLog_Created ON promoters.AuditLog(CreatedAt DESC);
