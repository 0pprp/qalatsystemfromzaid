-- Additive audit columns for cross-branch sales-filter gateway actors.
-- Gateway filter employees must not write foreign-branch UserIds into local FKs;
-- store display name instead and keep ChangedByUserId / FilteredByUserId NULL.
SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredByUserName') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredByUserName NVARCHAR(200) NULL;

IF OBJECT_ID(N'dbo.SalesFilterHistory', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalesFilterHistory', N'ChangedByUserName') IS NULL
    ALTER TABLE dbo.SalesFilterHistory ADD ChangedByUserName NVARCHAR(200) NULL;
GO
