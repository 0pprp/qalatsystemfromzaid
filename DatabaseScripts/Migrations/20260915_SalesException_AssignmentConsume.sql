-- Idempotent: assignment-consume markers on SalesExceptionRequests (gateway SQL).
-- Target: ConnectionStrings:SalesGateway. Safe to re-run.

SET NOCOUNT ON;
GO

IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'AssignmentConsumed') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD AssignmentConsumed BIT NOT NULL
        CONSTRAINT DF_SalesException_AssignmentConsumed DEFAULT (0);
GO

IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'AssignedEmployeeId') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD AssignedEmployeeId INT NULL;
GO

IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'AssignedEmployeeName') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD AssignedEmployeeName NVARCHAR(256) NULL;
GO

IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'AssignedAtUtc') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD AssignedAtUtc DATETIME2(3) NULL;
GO

IF COL_LENGTH(N'dbo.SalesExceptionRequests', N'AssignedByManagerUserName') IS NULL
    ALTER TABLE dbo.SalesExceptionRequests ADD AssignedByManagerUserName NVARCHAR(128) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesExceptionRequests_SalesRequest_Active'
      AND object_id = OBJECT_ID(N'dbo.SalesExceptionRequests')
)
BEGIN
    CREATE INDEX IX_SalesExceptionRequests_SalesRequest_Active
        ON dbo.SalesExceptionRequests (SalesRequestId, Status, AssignmentConsumed)
        WHERE SalesRequestId IS NOT NULL;
END
GO
