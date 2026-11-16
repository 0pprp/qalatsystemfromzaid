-- Paper archive bundles for sales employees.
-- Idempotent. The API also creates these tables on first use.
IF OBJECT_ID(N'dbo.SalesArchiveBundles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesArchiveBundles (
        BundleId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchiveBundles PRIMARY KEY,
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(200) NULL,
        BundleNumber INT NOT NULL,
        PeriodStart DATE NOT NULL,
        PeriodEnd DATE NOT NULL,
        IntermediateName NVARCHAR(200) NULL,
        IntermediatePhone NVARCHAR(20) NULL,
        Note NVARCHAR(1000) NULL,
        PhotoPath NVARCHAR(500) NULL,
        SentAt DATETIME NULL,
        ReviewedAt DATETIME NULL
    );
END
GO
IF OBJECT_ID(N'dbo.SalesArchiveItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesArchiveItems (
        ArchiveId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchiveItems PRIMARY KEY,
        SaleId INT NOT NULL,
        BundleId INT NOT NULL,
        EmployeeId INT NOT NULL,
        CustomerName NVARCHAR(255) NOT NULL,
        SaleDate DATE NOT NULL,
        SalePrice DECIMAL(18, 0) NOT NULL,
        ItemName NVARCHAR(500) NOT NULL,
        ListId INT NULL,
        ListName NVARCHAR(255) NULL,
        Status NVARCHAR(20) NOT NULL,
        RefusalNote NVARCHAR(1000) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesArchiveItems_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT FK_SalesArchiveItems_Bundle FOREIGN KEY (BundleId) REFERENCES dbo.SalesArchiveBundles (BundleId)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalesArchiveBundles_EmpPeriod' AND object_id = OBJECT_ID(N'dbo.SalesArchiveBundles'))
    CREATE UNIQUE INDEX UX_SalesArchiveBundles_EmpPeriod ON dbo.SalesArchiveBundles (EmployeeId, PeriodStart);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalesArchiveBundles_EmpNumber' AND object_id = OBJECT_ID(N'dbo.SalesArchiveBundles'))
    CREATE UNIQUE INDEX UX_SalesArchiveBundles_EmpNumber ON dbo.SalesArchiveBundles (EmployeeId, BundleNumber);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SalesArchiveItems_Sale' AND object_id = OBJECT_ID(N'dbo.SalesArchiveItems'))
    CREATE UNIQUE INDEX UX_SalesArchiveItems_Sale ON dbo.SalesArchiveItems (SaleId);
GO
IF OBJECT_ID(N'dbo.SalesArchivePhotos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesArchivePhotos (
        PhotoId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesArchivePhotos PRIMARY KEY,
        BundleId INT NOT NULL,
        RelativePath NVARCHAR(500) NOT NULL,
        SortOrder INT NOT NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesArchivePhotos_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_SalesArchivePhotos_Bundle FOREIGN KEY (BundleId) REFERENCES dbo.SalesArchiveBundles (BundleId)
    );
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesArchivePhotos_Bundle' AND object_id = OBJECT_ID(N'dbo.SalesArchivePhotos'))
    CREATE INDEX IX_SalesArchivePhotos_Bundle ON dbo.SalesArchivePhotos (BundleId, SortOrder);
GO
INSERT INTO dbo.SalesArchivePhotos (BundleId, RelativePath, SortOrder)
SELECT b.BundleId, b.PhotoPath, 1
FROM dbo.SalesArchiveBundles b
WHERE b.PhotoPath IS NOT NULL
  AND LTRIM(RTRIM(b.PhotoPath)) <> N''
  AND NOT EXISTS (SELECT 1 FROM dbo.SalesArchivePhotos p WHERE p.BundleId = b.BundleId);
GO
