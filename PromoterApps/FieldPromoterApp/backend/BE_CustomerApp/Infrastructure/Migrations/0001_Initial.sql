-- BE_CustomerApp initial schema. Applied once by DbMigrator (tracked in agents.__CustomerAppMigrations).
-- Money columns are BIGINT Iraqi dinars. Times are UTC (DATETIME2).

CREATE TABLE agents.Users (
    Id                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Phone             VARCHAR(11)    NOT NULL,
    FirstName         NVARCHAR(50)   NOT NULL,
    FatherName        NVARCHAR(50)   NOT NULL,
    GrandfatherName   NVARCHAR(50)   NOT NULL,
    Surname           NVARCHAR(50)   NULL,
    CityValue         INT            NULL,
    ReferralCode      VARCHAR(12)    NOT NULL,
    ParentUserId      BIGINT         NULL CONSTRAINT FK_Users_Parent REFERENCES agents.Users(Id),
    CreatedByUserId   BIGINT         NULL CONSTRAINT FK_Users_CreatedBy REFERENCES agents.Users(Id),
    Depth             INT            NOT NULL CONSTRAINT DF_Users_Depth DEFAULT 0,
    DirectCount       INT            NOT NULL CONSTRAINT DF_Users_DirectCount DEFAULT 0,
    NetworkSize       INT            NOT NULL CONSTRAINT DF_Users_NetworkSize DEFAULT 0,
    MarketingOptIn    BIT            NOT NULL CONSTRAINT DF_Users_Marketing DEFAULT 0,
    IsSuspended       BIT            NOT NULL CONSTRAINT DF_Users_Suspended DEFAULT 0,
    SuspendReason     NVARCHAR(400)  NULL,
    EarningsFrozen    BIT            NOT NULL CONSTRAINT DF_Users_Frozen DEFAULT 0,
    FreezeReason      NVARCHAR(400)  NULL,
    IsDeleted         BIT            NOT NULL CONSTRAINT DF_Users_Deleted DEFAULT 0,
    DeletedAt         DATETIME2      NULL,
    TermsAcceptedAt   DATETIME2      NULL,
    LastLoginAt       DATETIME2      NULL,
    CreatedAt         DATETIME2      NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2      NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT SYSUTCDATETIME()
);
-- Deleted accounts release their phone (it is replaced with a tombstone value) so the person can re-register.
CREATE UNIQUE INDEX UX_Users_Phone ON agents.Users(Phone);
CREATE UNIQUE INDEX UX_Users_ReferralCode ON agents.Users(ReferralCode);
CREATE INDEX IX_Users_Parent ON agents.Users(ParentUserId) INCLUDE (FirstName, CreatedAt, NetworkSize, IsDeleted);

CREATE TABLE agents.OtpRequests (
    Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OtpRequests PRIMARY KEY,
    Phone             VARCHAR(11)    NOT NULL,
    Purpose           VARCHAR(20)    NOT NULL,
    CodeHash          VARBINARY(32)  NOT NULL,
    ExpiresAt         DATETIME2      NOT NULL,
    Attempts          INT            NOT NULL CONSTRAINT DF_Otp_Attempts DEFAULT 0,
    ConsumedAt        DATETIME2      NULL,
    RequestedByUserId BIGINT         NULL,
    PayloadJson       NVARCHAR(2000) NULL,
    Ip                VARCHAR(45)    NULL,
    CreatedAt         DATETIME2      NOT NULL CONSTRAINT DF_Otp_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_OtpRequests_Phone ON agents.OtpRequests(Phone, CreatedAt);
CREATE INDEX IX_OtpRequests_Ip ON agents.OtpRequests(Ip, CreatedAt);

CREATE TABLE agents.RefreshTokens (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshTokens PRIMARY KEY,
    UserId      BIGINT        NOT NULL CONSTRAINT FK_RefreshTokens_User REFERENCES agents.Users(Id),
    TokenHash   VARBINARY(32) NOT NULL,
    ExpiresAt   DATETIME2     NOT NULL,
    RevokedAt   DATETIME2     NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_RefreshTokens_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_RefreshTokens_Hash ON agents.RefreshTokens(TokenHash);
CREATE INDEX IX_RefreshTokens_User ON agents.RefreshTokens(UserId);

CREATE TABLE agents.Devices (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Devices PRIMARY KEY,
    UserId      BIGINT         NOT NULL CONSTRAINT FK_Devices_User REFERENCES agents.Users(Id),
    Token       NVARCHAR(512)  NOT NULL,
    Platform    VARCHAR(10)    NOT NULL,
    AppVersion  VARCHAR(20)    NULL,
    UpdatedAt   DATETIME2      NOT NULL CONSTRAINT DF_Devices_UpdatedAt DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_Devices_Token ON agents.Devices(Token);
CREATE INDEX IX_Devices_User ON agents.Devices(UserId);

CREATE TABLE agents.Categories (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL,
    IconUrl     NVARCHAR(500) NULL,
    SortOrder   INT           NOT NULL CONSTRAINT DF_Categories_Sort DEFAULT 0,
    IsVisible   BIT           NOT NULL CONSTRAINT DF_Categories_Visible DEFAULT 1,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE agents.Products (
    Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    CategoryId         INT            NULL CONSTRAINT FK_Products_Category REFERENCES agents.Categories(Id),
    Name               NVARCHAR(200)  NOT NULL,
    Description        NVARCHAR(MAX)  NULL,
    ExtraInfo          NVARCHAR(2000) NULL,
    CashPrice          BIGINT         NULL,
    InstallmentPrice   BIGINT         NULL,
    DailyInstallment   BIGINT         NULL,
    InstallmentDays    INT            NULL,
    AllowCash          BIT            NOT NULL CONSTRAINT DF_Products_AllowCash DEFAULT 1,
    AllowInstallments  BIT            NOT NULL CONSTRAINT DF_Products_AllowInst DEFAULT 1,
    BaseCommission     BIGINT         NOT NULL CONSTRAINT DF_Products_Commission DEFAULT 0,
    IsVisible          BIT            NOT NULL CONSTRAINT DF_Products_Visible DEFAULT 0,
    IsFeatured         BIT            NOT NULL CONSTRAINT DF_Products_Featured DEFAULT 0,
    Badge              NVARCHAR(30)   NULL,
    SortOrder          INT            NOT NULL CONSTRAINT DF_Products_Sort DEFAULT 0,
    SoldCount          INT            NOT NULL CONSTRAINT DF_Products_Sold DEFAULT 0,
    CreatedAt          DATETIME2      NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt          DATETIME2      NOT NULL CONSTRAINT DF_Products_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Products_Money CHECK (BaseCommission >= 0 AND ISNULL(CashPrice,0) >= 0 AND ISNULL(InstallmentPrice,0) >= 0)
);
CREATE INDEX IX_Products_Category ON agents.Products(CategoryId, IsVisible);

CREATE TABLE agents.ProductMedia (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductMedia PRIMARY KEY,
    ProductId   INT           NOT NULL CONSTRAINT FK_ProductMedia_Product REFERENCES agents.Products(Id) ON DELETE CASCADE,
    Type        VARCHAR(10)   NOT NULL,
    Url         NVARCHAR(500) NOT NULL,
    SortOrder   INT           NOT NULL CONSTRAINT DF_ProductMedia_Sort DEFAULT 0
);
CREATE INDEX IX_ProductMedia_Product ON agents.ProductMedia(ProductId, SortOrder);

CREATE TABLE agents.ProductSpecs (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductSpecs PRIMARY KEY,
    ProductId   INT           NOT NULL CONSTRAINT FK_ProductSpecs_Product REFERENCES agents.Products(Id) ON DELETE CASCADE,
    Name        NVARCHAR(100) NOT NULL,
    Value       NVARCHAR(500) NOT NULL,
    SortOrder   INT           NOT NULL CONSTRAINT DF_ProductSpecs_Sort DEFAULT 0
);
CREATE INDEX IX_ProductSpecs_Product ON agents.ProductSpecs(ProductId, SortOrder);

CREATE TABLE agents.ProductBranches (
    ProductId   INT NOT NULL CONSTRAINT FK_ProductBranches_Product REFERENCES agents.Products(Id) ON DELETE CASCADE,
    CityValue   INT NOT NULL,
    CONSTRAINT PK_ProductBranches PRIMARY KEY (ProductId, CityValue)
);

CREATE TABLE agents.Banners (
    Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Banners PRIMARY KEY,
    Title       NVARCHAR(150) NOT NULL,
    Subtitle    NVARCHAR(300) NULL,
    ImageUrl    NVARCHAR(500) NOT NULL,
    ProductId   INT           NULL,
    CategoryId  INT           NULL,
    ExternalUrl NVARCHAR(500) NULL,
    SortOrder   INT           NOT NULL CONSTRAINT DF_Banners_Sort DEFAULT 0,
    IsVisible   BIT           NOT NULL CONSTRAINT DF_Banners_Visible DEFAULT 1,
    StartsAt    DATETIME2     NULL,
    EndsAt      DATETIME2     NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_Banners_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE agents.Orders (
    Id                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    OrderUuid          UNIQUEIDENTIFIER NOT NULL,
    OrderNumber        VARCHAR(20)    NULL,
    SubmittedByUserId  BIGINT         NOT NULL CONSTRAINT FK_Orders_User REFERENCES agents.Users(Id),
    IdempotencyKey     VARCHAR(64)    NULL,
    CityValue          INT            NOT NULL,
    CityName           NVARCHAR(100)  NOT NULL,
    PurchaseType       VARCHAR(20)    NOT NULL,
    ForSelf            BIT            NOT NULL,
    FirstName          NVARCHAR(50)   NOT NULL,
    FatherName         NVARCHAR(50)   NOT NULL,
    GrandfatherName    NVARCHAR(50)   NOT NULL,
    Surname            NVARCHAR(50)   NULL,
    CustomerPhone      VARCHAR(11)    NOT NULL,
    SecondaryPhone     VARCHAR(11)    NULL,
    Area               NVARCHAR(150)  NOT NULL,
    AddressDetails     NVARCHAR(400)  NOT NULL,
    NearestLandmark    NVARCHAR(200)  NULL,
    Latitude           DECIMAL(9,6)   NULL,
    Longitude          DECIMAL(9,6)   NULL,
    ShopName           NVARCHAR(150)  NULL,
    ShopBusinessType   NVARCHAR(100)  NULL,
    ShopAddress        NVARCHAR(400)  NULL,
    OwnsShop           BIT            NULL,
    Notes              NVARCHAR(1000) NULL,
    EstimatedTotal     BIGINT         NOT NULL,
    Status             VARCHAR(20)    NOT NULL,
    PublicReason       NVARCHAR(400)  NULL,
    InternalNote       NVARCHAR(1000) NULL,
    SalesRequestId     INT            NULL,
    BranchStatus       VARCHAR(30)    NULL,
    BranchRejectReason NVARCHAR(400)  NULL,
    LastSyncAt         DATETIME2      NULL,
    ReviewedBy         NVARCHAR(100)  NULL,
    DecidedBy          NVARCHAR(100)  NULL,
    DecidedAt          DATETIME2      NULL,
    SoldAt             DATETIME2      NULL,
    CreatedAt          DATETIME2      NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt          DATETIME2      NOT NULL CONSTRAINT DF_Orders_UpdatedAt DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_Orders_Uuid ON agents.Orders(OrderUuid);
CREATE UNIQUE INDEX UX_Orders_Number ON agents.Orders(OrderNumber) WHERE OrderNumber IS NOT NULL;
CREATE UNIQUE INDEX UX_Orders_Idempotency ON agents.Orders(SubmittedByUserId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
CREATE INDEX IX_Orders_User ON agents.Orders(SubmittedByUserId, CreatedAt DESC);
CREATE INDEX IX_Orders_Status ON agents.Orders(Status, CreatedAt DESC);
CREATE INDEX IX_Orders_Phone ON agents.Orders(CustomerPhone, Status);

CREATE TABLE agents.OrderItems (
    Id                 BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId            BIGINT        NOT NULL CONSTRAINT FK_OrderItems_Order REFERENCES agents.Orders(Id),
    ProductId          INT           NOT NULL CONSTRAINT FK_OrderItems_Product REFERENCES agents.Products(Id),
    ProductName        NVARCHAR(200) NOT NULL,
    Quantity           INT           NOT NULL,
    UnitPrice          BIGINT        NOT NULL,
    DailyInstallment   BIGINT        NULL,
    InstallmentDays    INT           NULL
);
CREATE INDEX IX_OrderItems_Order ON agents.OrderItems(OrderId);

CREATE TABLE agents.OrderEvents (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderEvents PRIMARY KEY,
    OrderId     BIGINT         NOT NULL CONSTRAINT FK_OrderEvents_Order REFERENCES agents.Orders(Id),
    Status      VARCHAR(20)    NOT NULL,
    Note        NVARCHAR(1000) NULL,
    IsInternal  BIT            NOT NULL CONSTRAINT DF_OrderEvents_Internal DEFAULT 0,
    Actor       NVARCHAR(100)  NULL,
    CreatedAt   DATETIME2      NOT NULL CONSTRAINT DF_OrderEvents_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_OrderEvents_Order ON agents.OrderEvents(OrderId, CreatedAt);

-- One row per sold order: the guard that commissions are computed exactly once, plus the snapshot used.
CREATE TABLE agents.CommissionRuns (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommissionRuns PRIMARY KEY,
    OrderId         BIGINT        NOT NULL CONSTRAINT FK_CommissionRuns_Order REFERENCES agents.Orders(Id),
    BaseCommission  BIGINT        NOT NULL,
    RatioPercent    DECIMAL(5,2)  NOT NULL,
    RoundingUnit    INT           NOT NULL,
    TotalAmount     BIGINT        NOT NULL,
    ReversedAt      DATETIME2     NULL,
    ReverseReason   NVARCHAR(400) NULL,
    ReversedBy      NVARCHAR(100) NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_CommissionRuns_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_CommissionRuns_Order ON agents.CommissionRuns(OrderId);

-- Append-mostly ledger. Kind: Direct | Network | Clawback | Adjustment. Status: Pending | Available | Paid | Reversed | Frozen.
CREATE TABLE agents.CommissionEntries (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CommissionEntries PRIMARY KEY,
    RunId       BIGINT        NULL CONSTRAINT FK_CommissionEntries_Run REFERENCES agents.CommissionRuns(Id),
    OrderId     BIGINT        NULL,
    UserId      BIGINT        NOT NULL CONSTRAINT FK_CommissionEntries_User REFERENCES agents.Users(Id),
    Level       INT           NULL,
    Kind        VARCHAR(12)   NOT NULL,
    Amount      BIGINT        NOT NULL,
    Status      VARCHAR(12)   NOT NULL,
    AvailableAt DATETIME2     NOT NULL,
    Note        NVARCHAR(400) NULL,
    CreatedBy   NVARCHAR(100) NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_CommissionEntries_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt   DATETIME2     NOT NULL CONSTRAINT DF_CommissionEntries_UpdatedAt DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_CommissionEntries_OrderLevel ON agents.CommissionEntries(OrderId, Level) WHERE Kind IN ('Direct','Network');
CREATE INDEX IX_CommissionEntries_User ON agents.CommissionEntries(UserId, Status) INCLUDE (Amount, Kind);
CREATE INDEX IX_CommissionEntries_Maturing ON agents.CommissionEntries(Status, AvailableAt);

CREATE TABLE agents.Payouts (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payouts PRIMARY KEY,
    UserId          BIGINT        NOT NULL CONSTRAINT FK_Payouts_User REFERENCES agents.Users(Id),
    Amount          BIGINT        NOT NULL CONSTRAINT CK_Payouts_Amount CHECK (Amount > 0),
    Method          VARCHAR(20)   NOT NULL,
    AccountDetails  NVARCHAR(200) NULL,
    Status          VARCHAR(12)   NOT NULL,
    Note            NVARCHAR(400) NULL,
    Reference       NVARCHAR(100) NULL,
    ProcessedBy     NVARCHAR(100) NULL,
    ProcessedAt     DATETIME2     NULL,
    CreatedAt       DATETIME2     NOT NULL CONSTRAINT DF_Payouts_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Payouts_User ON agents.Payouts(UserId, Status);
-- At most one open (Requested/Approved) payout per user.
CREATE UNIQUE INDEX UX_Payouts_OpenPerUser ON agents.Payouts(UserId) WHERE Status IN ('Requested','Approved');

CREATE TABLE agents.Notifications (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId      BIGINT         NOT NULL CONSTRAINT FK_Notifications_User REFERENCES agents.Users(Id),
    Type        VARCHAR(20)    NOT NULL,
    Title       NVARCHAR(150)  NOT NULL,
    Body        NVARCHAR(1000) NOT NULL,
    DataJson    NVARCHAR(500)  NULL,
    BroadcastId BIGINT         NULL,
    ReadAt      DATETIME2      NULL,
    CreatedAt   DATETIME2      NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Notifications_User ON agents.Notifications(UserId, CreatedAt DESC) INCLUDE (ReadAt);

CREATE TABLE agents.Broadcasts (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Broadcasts PRIMARY KEY,
    Title       NVARCHAR(150)  NOT NULL,
    Body        NVARCHAR(1000) NOT NULL,
    Audience    VARCHAR(20)    NOT NULL,
    CityValue   INT            NULL,
    ProductId   INT            NULL,
    Recipients  INT            NOT NULL CONSTRAINT DF_Broadcasts_Recipients DEFAULT 0,
    CreatedBy   NVARCHAR(100)  NOT NULL,
    CreatedAt   DATETIME2      NOT NULL CONSTRAINT DF_Broadcasts_CreatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE agents.AppSettings (
    [Key]       VARCHAR(50)    NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
    [Value]     NVARCHAR(1000) NOT NULL,
    UpdatedAt   DATETIME2      NOT NULL CONSTRAINT DF_AppSettings_UpdatedAt DEFAULT SYSUTCDATETIME()
);

CREATE TABLE agents.AuditLog (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    ActorName   NVARCHAR(100) NOT NULL,
    ActorRole   NVARCHAR(50)  NOT NULL,
    Action      VARCHAR(60)   NOT NULL,
    Entity      VARCHAR(40)   NOT NULL,
    EntityId    VARCHAR(40)   NULL,
    BeforeJson  NVARCHAR(MAX) NULL,
    AfterJson   NVARCHAR(MAX) NULL,
    Ip          VARCHAR(45)   NULL,
    CreatedAt   DATETIME2     NOT NULL CONSTRAINT DF_AuditLog_CreatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_AuditLog_Entity ON agents.AuditLog(Entity, EntityId);
CREATE INDEX IX_AuditLog_CreatedAt ON agents.AuditLog(CreatedAt DESC);
