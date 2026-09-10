SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Follower ↔ list ACL. ListId = dbo.Delegates.DelegateID (same key as Followers/Lists).
-- Login gate remains Users.UserType = متابع + UserState; this table only scopes lists.
-- Idempotent / Demo-safe. Does NOT seed assignments.

IF OBJECT_ID(N'dbo.FollowerUserLists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerUserLists (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerUserLists PRIMARY KEY,
        UserId INT NOT NULL,
        ListId INT NOT NULL, -- Delegates.DelegateID
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerUserLists_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_FollowerUserLists_User_List UNIQUE (UserId, ListId),
        CONSTRAINT FK_FollowerUserLists_Users
            FOREIGN KEY (UserId) REFERENCES dbo.Users (UserID),
        CONSTRAINT FK_FollowerUserLists_Delegates
            FOREIGN KEY (ListId) REFERENCES dbo.Delegates (DelegateID)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FollowerUserLists_UserId'
      AND object_id = OBJECT_ID(N'dbo.FollowerUserLists')
)
BEGIN
    CREATE INDEX IX_FollowerUserLists_UserId
        ON dbo.FollowerUserLists (UserId)
        INCLUDE (ListId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FollowerUserLists_ListId'
      AND object_id = OBJECT_ID(N'dbo.FollowerUserLists')
)
BEGIN
    CREATE INDEX IX_FollowerUserLists_ListId
        ON dbo.FollowerUserLists (ListId);
END
GO
