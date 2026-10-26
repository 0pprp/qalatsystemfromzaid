-- Device audit + support indexes for same-day duplicate collection guard.
-- Impact: DB only schema. Intake logic lives in BE_DelegateWebApplication.
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.DelegateDevicePaymentAudit', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DelegateDevicePaymentAudit
    (
        AuditID BIGINT IDENTITY(1, 1) NOT NULL,
        ClientPaymentId NVARCHAR(36) NULL,
        CustomerID INT NULL,
        DelegateID INT NULL,
        Amount FLOAT NULL,
        GpsLocation NVARCHAR(200) NULL,
        DeviceId NVARCHAR(128) NULL,
        DeviceModel NVARCHAR(128) NULL,
        DevicePlatform NVARCHAR(64) NULL,
        AppVersion NVARCHAR(64) NULL,
        PaymentCreatedAtUtc DATETIME2(3) NULL,
        ReceivedAtUtc DATETIME2(3) NOT NULL
            CONSTRAINT DF_DelegateDevicePaymentAudit_ReceivedAtUtc DEFAULT (SYSUTCDATETIME()),
        Outcome NVARCHAR(40) NOT NULL,
        OutcomeMessage NVARCHAR(400) NULL,
        CustomersPaymentsRequestID INT NULL,
        CONSTRAINT PK_DelegateDevicePaymentAudit PRIMARY KEY CLUSTERED (AuditID)
    );
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DelegateDevicePaymentAudit_Customer_Received'
      AND object_id = OBJECT_ID(N'dbo.DelegateDevicePaymentAudit'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DelegateDevicePaymentAudit_Customer_Received
        ON dbo.DelegateDevicePaymentAudit (CustomerID, ReceivedAtUtc DESC)
        INCLUDE (DeviceId, Amount, Outcome, ClientPaymentId);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DelegateDevicePaymentAudit_DeviceId'
      AND object_id = OBJECT_ID(N'dbo.DelegateDevicePaymentAudit'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DelegateDevicePaymentAudit_DeviceId
        ON dbo.DelegateDevicePaymentAudit (DeviceId, ReceivedAtUtc DESC)
        WHERE DeviceId IS NOT NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_DelegateDevicePaymentAudit_ClientPaymentId'
      AND object_id = OBJECT_ID(N'dbo.DelegateDevicePaymentAudit'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DelegateDevicePaymentAudit_ClientPaymentId
        ON dbo.DelegateDevicePaymentAudit (ClientPaymentId)
        WHERE ClientPaymentId IS NOT NULL;
END;

PRINT 'DelegateDevicePaymentAudit ready';
GO
