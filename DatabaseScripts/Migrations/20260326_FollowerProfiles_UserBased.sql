-- Demo-safe / idempotent: Follower capability on company Users (not Delegates).
-- Does NOT delete historical GPS rows keyed by old DelegateId.
-- Going forward FollowerWorkShifts.FollowerId stores Users.UserID.

IF OBJECT_ID(N'dbo.FollowerProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerProfiles (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_FollowerProfiles_IsActive DEFAULT(1),
        CityId INT NULL,
        CityName NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerProfiles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT NULL,
        UpdatedAtUtc DATETIME2 NULL,
        CONSTRAINT UQ_FollowerProfiles_UserId UNIQUE (UserId),
        CONSTRAINT FK_FollowerProfiles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserID)
    );
    CREATE INDEX IX_FollowerProfiles_Active ON dbo.FollowerProfiles (IsActive) WHERE IsActive = 1;
END
GO

-- Optional documentation column (non-breaking).
IF COL_LENGTH('dbo.FollowerWorkShifts', 'IdentityNote') IS NULL
    ALTER TABLE dbo.FollowerWorkShifts ADD IdentityNote NVARCHAR(100) NULL;
GO
