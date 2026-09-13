-- Idempotent soft-delete columns for SalesRequests (17 branch DBs).
-- Safe: additive only; no destructive drops.
IF COL_LENGTH(N'dbo.SalesRequests', N'IsDeleted') IS NULL
    ALTER TABLE dbo.SalesRequests ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SalesRequests_IsDeleted DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesRequests', N'DeletedAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD DeletedAtUtc DATETIME2 NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'DeletedByUserId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD DeletedByUserId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'DeletedByName') IS NULL
    ALTER TABLE dbo.SalesRequests ADD DeletedByName NVARCHAR(200) NULL;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_IsDeleted' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_IsDeleted ON dbo.SalesRequests (IsDeleted);
