-- Follower notes: customer + list Delegate (not Sales Employee).
IF OBJECT_ID(N'dbo.FollowerCustomerNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerCustomerNotes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerId INT NOT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NOT NULL,
        CreatedByRole NVARCHAR(50) NOT NULL CONSTRAINT DF_FollowerCustomerNotes_Role DEFAULT (N'Follower'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerCustomerNotes_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_FollowerCustomerNotes_Customer ON dbo.FollowerCustomerNotes (CustomerId, CreatedAtUtc DESC);
END
GO

IF OBJECT_ID(N'dbo.FollowerDelegateNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerDelegateNotes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        DelegateId INT NOT NULL,
        ListId INT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NOT NULL,
        CreatedByRole NVARCHAR(50) NOT NULL CONSTRAINT DF_FollowerDelegateNotes_Role DEFAULT (N'Follower'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerDelegateNotes_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_FollowerDelegateNotes_Delegate ON dbo.FollowerDelegateNotes (DelegateId, CreatedByUserId);
END
GO

-- Idempotent copy from legacy misnamed table if present.
IF OBJECT_ID(N'dbo.FollowerEmployeeNotes', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.FollowerDelegateNotes (DelegateId, ListId, NoteText, CreatedByUserId, CreatedByName, CreatedByRole, CreatedAtUtc)
    SELECT e.EmployeeId, e.ListId, e.NoteText, e.CreatedByUserId, e.CreatedByName, e.CreatedByRole, e.CreatedAtUtc
    FROM dbo.FollowerEmployeeNotes e
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.FollowerDelegateNotes d
        WHERE d.DelegateId = e.EmployeeId
          AND d.CreatedByUserId = e.CreatedByUserId
          AND d.NoteText = e.NoteText
          AND d.CreatedAtUtc = e.CreatedAtUtc
    );
END
GO
