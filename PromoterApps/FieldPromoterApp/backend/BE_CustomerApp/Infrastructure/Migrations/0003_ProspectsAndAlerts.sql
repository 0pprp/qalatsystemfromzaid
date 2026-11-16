-- v3.1: agents pin potential customers' shops on the map (shared with every agent and the dashboard), and the
-- dashboard is alerted when a working agent stays in one place for an hour.

CREATE TABLE agents.Prospects (
    Id              BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Prospects PRIMARY KEY,
    CreatedByUserId BIGINT         NOT NULL CONSTRAINT FK_Prospects_User REFERENCES agents.Users(Id),
    CityValue       INT            NULL,
    Latitude        DECIMAL(9,6)   NOT NULL,
    Longitude       DECIMAL(9,6)   NOT NULL,
    ShopName        NVARCHAR(150)  NOT NULL,
    Description     NVARCHAR(500)  NULL,
    Interest        TINYINT        NOT NULL CONSTRAINT CK_Prospects_Interest CHECK (Interest BETWEEN 1 AND 5),
    Phone           VARCHAR(20)    NULL,
    GoodsValue      BIGINT         NULL,
    LengthM         DECIMAL(6,2)   NULL,
    WidthM          DECIMAL(6,2)   NULL,
    IsDeleted       BIT            NOT NULL CONSTRAINT DF_Prospects_IsDeleted DEFAULT 0,
    CreatedAt       DATETIME2      NOT NULL CONSTRAINT DF_Prospects_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2      NOT NULL CONSTRAINT DF_Prospects_UpdatedAt DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_Prospects_Location ON agents.Prospects(Latitude, Longitude) INCLUDE (Interest, ShopName) WHERE IsDeleted = 0;
CREATE INDEX IX_Prospects_CreatedBy ON agents.Prospects(CreatedByUserId, CreatedAt);

-- Raised by the worker while an agent is working; ResolvedAt is set once the agent moves on (or the day ends).
CREATE TABLE agents.WorkAlerts (
    Id          BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkAlerts PRIMARY KEY,
    UserId      BIGINT         NOT NULL CONSTRAINT FK_WorkAlerts_User REFERENCES agents.Users(Id),
    SessionId   BIGINT         NOT NULL CONSTRAINT FK_WorkAlerts_Session REFERENCES agents.WorkSessions(Id),
    Type        VARCHAR(20)    NOT NULL, -- Stationary
    Latitude    DECIMAL(9,6)   NOT NULL,
    Longitude   DECIMAL(9,6)   NOT NULL,
    SinceAt     DATETIME2      NOT NULL, -- first point of the stay
    LastSeenAt  DATETIME2      NOT NULL, -- latest point still at that place
    CreatedAt   DATETIME2      NOT NULL CONSTRAINT DF_WorkAlerts_CreatedAt DEFAULT SYSUTCDATETIME(),
    ResolvedAt  DATETIME2      NULL,
    SeenBy      NVARCHAR(100)  NULL,
    SeenAt      DATETIME2      NULL
);
CREATE INDEX IX_WorkAlerts_Open ON agents.WorkAlerts(SessionId) WHERE ResolvedAt IS NULL;
CREATE INDEX IX_WorkAlerts_Created ON agents.WorkAlerts(CreatedAt DESC);
