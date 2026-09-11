-- Idempotent shared CustomerNotes migration (CustomerId-keyed notes for all parties).
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.CustomerNotes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerNotes (
        NoteID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CustomerID INT NOT NULL,
        UserID INT NULL,
        NoteText NVARCHAR(MAX) NOT NULL,
        CreatedByName NVARCHAR(150) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CustomerNotes_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedDate DATETIME NOT NULL CONSTRAINT DF_CustomerNotes_CreatedDate DEFAULT (GETDATE())
    );
END

IF COL_LENGTH(N'dbo.CustomerNotes', N'CreatedByName') IS NULL
    ALTER TABLE dbo.CustomerNotes ADD CreatedByName NVARCHAR(150) NULL;

IF COL_LENGTH(N'dbo.CustomerNotes', N'CreatedAtUtc') IS NULL
    ALTER TABLE dbo.CustomerNotes ADD CreatedAtUtc DATETIME2 NULL;

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomerNotes_Users')
    ALTER TABLE dbo.CustomerNotes DROP CONSTRAINT FK_CustomerNotes_Users;

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.CustomerNotes') AND name = N'UserID' AND is_nullable = 0
)
    ALTER TABLE dbo.CustomerNotes ALTER COLUMN UserID INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CustomerNotes_Users')
    AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.CustomerNotes WITH NOCHECK
    ADD CONSTRAINT FK_CustomerNotes_Users FOREIGN KEY (UserID) REFERENCES dbo.Users (UserID);
END
GO

UPDATE dbo.CustomerNotes
SET CreatedAtUtc = CAST(CreatedDate AS DATETIME2)
WHERE CreatedAtUtc IS NULL AND CreatedDate IS NOT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_CustomerNotes_Customer_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.CustomerNotes')
)
BEGIN
    CREATE INDEX IX_CustomerNotes_Customer_CreatedAt
    ON dbo.CustomerNotes (CustomerID, CreatedAtUtc DESC);
END
GO
