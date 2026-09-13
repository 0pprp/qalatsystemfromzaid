-- Idempotent: sales request name-transfer audit trail (branch DB).
IF OBJECT_ID(N'dbo.SalesRequestNameTransfers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequestNameTransfers (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleRequestId INT NOT NULL,
        FromEmployeeId INT NOT NULL,
        FromEmployeeName NVARCHAR(200) NULL,
        ToEmployeeId INT NOT NULL,
        ToEmployeeName NVARCHAR(200) NULL,
        TransferReason NVARCHAR(1000) NOT NULL,
        TransferredAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesRequestNameTransfers_At DEFAULT (SYSUTCDATETIME()),
        TransferredByUserId INT NOT NULL,
        TransferredByName NVARCHAR(200) NULL
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequestNameTransfers_SaleRequestId'
      AND object_id = OBJECT_ID(N'dbo.SalesRequestNameTransfers')
)
    CREATE INDEX IX_SalesRequestNameTransfers_SaleRequestId
        ON dbo.SalesRequestNameTransfers (SaleRequestId, TransferredAtUtc, Id);
