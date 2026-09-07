namespace BE_Company.Sales.Services
{
    /// <summary>
    /// SQL Server compiles a whole batch before running it. Adding PostingStatus and then
    /// UPDATEing / indexing it in the same batch fails with "Invalid column name".
    /// Each entry in <see cref="Commands"/> is a separate SqlCommand / batch.
    /// </summary>
    public static class SalesDraftSchema
    {
        public static IReadOnlyList<string> Commands { get; } =
        [
            CoreSql,
            AddPostingColumnsSql,
            BackfillPostingSql,
            PostingIndexesSql,
            AddWizardStepSql
        ];

        public const string CoreSql = @"
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
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesDrafts_CreatedAt DEFAULT (GETDATE()),
        CompletedAt DATETIME NULL,
        CompletedBy INT NULL,
        DocumentsStatus NVARCHAR(30) NULL
    );
END;
IF COL_LENGTH(N'dbo.SalesDrafts', N'CompletedAt') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD CompletedAt DATETIME NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'CompletedBy') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD CompletedBy INT NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'DocumentsStatus') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD DocumentsStatus NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'SalesRequestId') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD SalesRequestId INT NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'CustomerListId') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD CustomerListId INT NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'DefaultTotalSalePrice') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD DefaultTotalSalePrice DECIMAL(18, 0) NOT NULL CONSTRAINT DF_SalesDrafts_DefaultTotal DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesDrafts', N'DefaultDailyInstallment') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD DefaultDailyInstallment DECIMAL(18, 0) NOT NULL CONSTRAINT DF_SalesDrafts_DefaultDaily DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesDrafts', N'DefaultDownPayment') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD DefaultDownPayment DECIMAL(18, 0) NOT NULL CONSTRAINT DF_SalesDrafts_DefaultDown DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesDrafts', N'OverrideTotalSalePrice') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD OverrideTotalSalePrice DECIMAL(18, 0) NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'OverrideDailyInstallment') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD OverrideDailyInstallment DECIMAL(18, 0) NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'OverrideDownPayment') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD OverrideDownPayment DECIMAL(18, 0) NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'DownPayment') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD DownPayment DECIMAL(18, 0) NOT NULL CONSTRAINT DF_SalesDrafts_DownPayment DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesDrafts', N'DownPaymentCustomerPaymentId') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD DownPaymentCustomerPaymentId INT NULL;
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
END;";

        public const string AddPostingColumnsSql = @"
IF COL_LENGTH(N'dbo.SalesDrafts', N'PostingStatus') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD PostingStatus NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'PostedAtUtc') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD PostedAtUtc DATETIME NULL;
IF COL_LENGTH(N'dbo.SalesDrafts', N'PostingAttempts') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD PostingAttempts INT NOT NULL CONSTRAINT DF_SalesDrafts_PostingAttempts DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesDrafts', N'LastPostingError') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD LastPostingError NVARCHAR(MAX) NULL;";

        public const string BackfillPostingSql = @"
UPDATE dbo.SalesDrafts
SET PostingStatus = N'Posted'
WHERE Status = N'Completed'
  AND PostingStatus IS NULL;
UPDATE dbo.SalesDrafts
SET PostingStatus = N'Pending'
WHERE PostingStatus IS NULL;";

        public const string PostingIndexesSql = @"
IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalesDrafts', N'PostingStatus') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE name = N'IX_SalesDrafts_PostingDue'
          AND object_id = OBJECT_ID(N'dbo.SalesDrafts'))
BEGIN
    EXEC(N'CREATE NONCLUSTERED INDEX IX_SalesDrafts_PostingDue
        ON dbo.SalesDrafts (PostingStatus, Status)
        WHERE PostingStatus IN (N''Pending'', N''Failed'', N''Processing'');');
END;";

        public const string AddWizardStepSql = @"
IF COL_LENGTH(N'dbo.SalesDrafts', N'WizardCurrentStep') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD WizardCurrentStep INT NULL;";
    }
}
