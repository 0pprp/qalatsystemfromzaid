-- RUN ONLY ON DatabaseCompanyNajaf_DEMO.
-- Do not execute against DatabaseCompany or DatabaseCompanyNajaf.
IF DB_NAME() <> N'DatabaseCompanyNajaf_DEMO'
BEGIN
    RAISERROR(N'SalesComplete script is allowed only on DatabaseCompanyNajaf_DEMO.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.SalesDrafts', N'CompletedAt') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD CompletedAt DATETIME NULL;

IF COL_LENGTH(N'dbo.SalesDrafts', N'CompletedBy') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD CompletedBy INT NULL;

IF COL_LENGTH(N'dbo.SalesDrafts', N'DocumentsStatus') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD DocumentsStatus NVARCHAR(30) NULL;

IF OBJECT_ID(N'dbo.SalesDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesDocuments (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleId INT NOT NULL,
        DocumentType NVARCHAR(50) NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        StoragePath NVARCHAR(500) NOT NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesDocuments_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT FK_SalesDocuments_SalesDrafts FOREIGN KEY (SaleId) REFERENCES dbo.SalesDrafts (SaleId),
        CONSTRAINT UQ_SalesDocuments_SaleType UNIQUE (SaleId, DocumentType)
    );
END;
