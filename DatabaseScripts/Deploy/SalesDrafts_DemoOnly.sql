-- RUN ONLY ON DatabaseCompanyNajaf_DEMO.
-- Do not execute against DatabaseCompany or DatabaseCompanyNajaf.
IF DB_NAME() <> N'DatabaseCompanyNajaf_DEMO'
BEGIN
    RAISERROR(N'SalesDrafts script is allowed only on DatabaseCompanyNajaf_DEMO.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesDrafts (
        SaleId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        UserName NVARCHAR(200) NULL,
        UserType NVARCHAR(100) NULL,
        CityValue NVARCHAR(100) NULL,
        CityName NVARCHAR(200) NULL,
        Status NVARCHAR(20) NOT NULL,
        CustomerId INT NULL,
        SourceCityValue NVARCHAR(100) NULL,
        FullName NVARCHAR(255) NOT NULL,
        Phone NVARCHAR(50) NULL,
        Province NVARCHAR(200) NULL,
        NationalCardNumber NVARCHAR(50) NULL,
        Address NVARCHAR(500) NULL,
        NearestLandmark NVARCHAR(255) NULL,
        MukhtarName NVARCHAR(255) NULL,
        RationCenterNumber NVARCHAR(50) NULL,
        EvaluationLevel INT NOT NULL,
        EvaluationNote NVARCHAR(MAX) NOT NULL,
        BaseSalePrice DECIMAL(18, 0) NOT NULL,
        FinalSalePrice DECIMAL(18, 0) NOT NULL,
        DailyInstallment DECIMAL(18, 0) NOT NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesDrafts_CreatedAt DEFAULT (GETDATE())
    );
    CREATE INDEX IX_SalesDrafts_EmployeeId ON dbo.SalesDrafts (EmployeeId, CreatedAt DESC);
END;

IF OBJECT_ID(N'dbo.SalesDraftItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesDraftItems (
        SaleItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleId INT NOT NULL,
        ProductId INT NOT NULL,
        ProductName NVARCHAR(255) NULL,
        Quantity INT NOT NULL,
        UnitSalePrice DECIMAL(18, 0) NOT NULL,
        LineSalePrice DECIMAL(18, 0) NOT NULL,
        CONSTRAINT FK_SalesDraftItems_SalesDrafts FOREIGN KEY (SaleId) REFERENCES dbo.SalesDrafts (SaleId)
    );
END;
