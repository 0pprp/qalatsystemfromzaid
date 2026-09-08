-- Follower notes (customer + employee). Salesman APIs must never SELECT these tables.
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

IF OBJECT_ID(N'dbo.FollowerEmployeeNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerEmployeeNotes (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ListId INT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NOT NULL,
        CreatedByRole NVARCHAR(50) NOT NULL CONSTRAINT DF_FollowerEmployeeNotes_Role DEFAULT (N'Follower'),
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerEmployeeNotes_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_FollowerEmployeeNotes_Employee ON dbo.FollowerEmployeeNotes (EmployeeId, CreatedByUserId);
END
GO
