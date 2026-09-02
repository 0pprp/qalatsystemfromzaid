-- RUN ONLY ON DatabaseCompanyNajaf_DEMO.
-- Do not execute against DatabaseCompany or DatabaseCompanyNajaf.
IF DB_NAME() <> N'DatabaseCompanyNajaf_DEMO'
BEGIN
    RAISERROR(N'SalesRequests script is allowed only on DatabaseCompanyNajaf_DEMO.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequests (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NULL,
        CreatedByUserType NVARCHAR(50) NULL,
        TargetEmployeeId INT NOT NULL,
        TargetEmployeeName NVARCHAR(200) NULL,
        CityValue NVARCHAR(100) NULL,
        CityName NVARCHAR(200) NULL,
        CustomerSourceType NVARCHAR(30) NOT NULL,
        ExistingCustomerId INT NULL,
        CustomerSourceCityValue NVARCHAR(100) NULL,
        CustomerName NVARCHAR(200) NOT NULL,
        CustomerPhone NVARCHAR(50) NULL,
        CustomerProvince NVARCHAR(100) NULL,
        CustomerAddress NVARCHAR(400) NULL,
        Notes NVARCHAR(1000) NULL,
        Status NVARCHAR(30) NOT NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesRequests_CreatedAt DEFAULT (GETUTCDATE()),
        ViewedAtUtc DATETIME NULL,
        ProcessingAtUtc DATETIME NULL,
        ConvertedToSaleId INT NULL,
        CompletedAtUtc DATETIME NULL,
        RejectedAtUtc DATETIME NULL,
        RejectionReason NVARCHAR(400) NULL
    );
    CREATE INDEX IX_SalesRequests_Target ON dbo.SalesRequests (TargetEmployeeId, Status, CreatedAtUtc);
    CREATE INDEX IX_SalesRequests_City ON dbo.SalesRequests (CityValue, Status, CreatedAtUtc);
END;

IF COL_LENGTH(N'dbo.SalesDrafts', N'SalesRequestId') IS NULL
BEGIN
    ALTER TABLE dbo.SalesDrafts ADD SalesRequestId INT NULL;
END;
