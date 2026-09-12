-- Sales filter gate for SalesRequests + city ACL for filter employees.
-- Decision: legacy rows already assigned to a sales employee (TargetEmployeeId > 0
-- and Status in the employee workflow) backfill FilterStatus = ReadyForSale so
-- existing work does not disappear from the sales-employee app after deploy.
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.SalesFilterUserCities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesFilterUserCities (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        CityValue NVARCHAR(100) NOT NULL,
        CityName NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesFilterUserCities_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_SalesFilterUserCities_User_City UNIQUE (UserId, CityValue)
    );
    CREATE INDEX IX_SalesFilterUserCities_User ON dbo.SalesFilterUserCities (UserId);
END

IF COL_LENGTH(N'dbo.SalesRequests', N'FilterStatus') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterStatus NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredByUserId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredByUserId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilterNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilterRejectReason') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterRejectReason NVARCHAR(400) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredAtUtc DATETIME2 NULL;

UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE FilterStatus IS NULL
  AND TargetEmployeeId > 0
  AND [Status] IN (N'Assigned', N'Viewed', N'PreparedForSale', N'Pending', N'InProgress',
                   N'ConvertedToSale', N'Inspected', N'Completed', N'Returned', N'Rejected');

UPDATE dbo.SalesRequests SET FilterStatus = N'PendingFilter' WHERE FilterStatus IS NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_FilterStatus' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_FilterStatus ON dbo.SalesRequests (FilterStatus);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_City_FilterStatus' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_City_FilterStatus ON dbo.SalesRequests (CityValue, FilterStatus);

IF OBJECT_ID(N'dbo.SalesFilterHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesFilterHistory (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleRequestId INT NOT NULL,
        PreviousStatus NVARCHAR(30) NULL,
        NewStatus NVARCHAR(30) NOT NULL,
        ChangedByUserId INT NULL,
        Note NVARCHAR(1000) NULL,
        Reason NVARCHAR(400) NULL,
        ChangedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesFilterHistory_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_SalesFilterHistory_Request ON dbo.SalesFilterHistory (SaleRequestId, ChangedAtUtc, Id);
END
GO
