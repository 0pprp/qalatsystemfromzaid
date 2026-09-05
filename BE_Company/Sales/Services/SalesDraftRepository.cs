using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class SalesDraftRepository : ISalesDraftRepository
    {
        private readonly SalesDevelopmentGuard _guard;

        public SalesDraftRepository(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = SchemaSql;
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<SalesDraftDTO> CreateAsync(SalesDraftDTO draft, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            try
            {
                var saleId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesDrafts
(EmployeeId, UserName, UserType, CityValue, CityName, Status, CustomerId, SourceCityValue,
 FullName, Phone, Province, NationalCardNumber, Address, NearestLandmark, MukhtarName, RationCenterNumber,
 EvaluationLevel, EvaluationNote, BaseSalePrice, FinalSalePrice, DailyInstallment,
 DefaultTotalSalePrice, DefaultDailyInstallment, DefaultDownPayment,
 OverrideTotalSalePrice, OverrideDailyInstallment, OverrideDownPayment, DownPayment,
 SalesRequestId, CustomerListId)
OUTPUT INSERTED.SaleId
VALUES
(@EmployeeId, @UserName, @UserType, @CityValue, @CityName, @Status, @CustomerId, @SourceCityValue,
 @FullName, @Phone, @Province, @NationalCardNumber, @Address, @NearestLandmark, @MukhtarName, @RationCenterNumber,
 @EvaluationLevel, @EvaluationNote, @BaseSalePrice, @FinalSalePrice, @DailyInstallment,
 @DefaultTotalSalePrice, @DefaultDailyInstallment, @DefaultDownPayment,
 @OverrideTotalSalePrice, @OverrideDailyInstallment, @OverrideDownPayment, @DownPayment,
 @SalesRequestId, @CustomerListId);",
                    draft, tx, cancellationToken: ct));

                foreach (var item in draft.Items)
                {
                    item.SaleItemId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesDraftItems (SaleId, ProductId, ProductName, Quantity, UnitSalePrice, LineSalePrice)
OUTPUT INSERTED.SaleItemId
VALUES (@SaleId, @ProductId, @ProductName, @Quantity, @UnitSalePrice, @LineSalePrice);",
                        new
                        {
                            SaleId = saleId,
                            item.ProductId,
                            item.ProductName,
                            item.Quantity,
                            item.UnitSalePrice,
                            item.LineSalePrice
                        }, tx, cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
                draft.SaleId = saleId;
                var saved = await GetByIdAsync(saleId, draft.EmployeeId, ct);
                return saved ?? draft;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<SalesDraftDTO>> GetByEmployeeAsync(int employeeId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var headers = (await connection.QueryAsync<SalesDraftDTO>(new CommandDefinition(
                @"SELECT SaleId, EmployeeId, UserName, UserType, CityValue, CityName, Status, CustomerId, SourceCityValue,
                         FullName, Phone, Province, NationalCardNumber, Address, NearestLandmark, MukhtarName, RationCenterNumber,
                         EvaluationLevel, EvaluationNote, BaseSalePrice, FinalSalePrice, DailyInstallment,
                         DefaultTotalSalePrice, DefaultDailyInstallment, DefaultDownPayment,
                         OverrideTotalSalePrice, OverrideDailyInstallment, OverrideDownPayment, DownPayment,
                         CreatedAt,
                         CompletedAt, CompletedBy, DocumentsStatus, SalesRequestId, CustomerListId
                  FROM dbo.SalesDrafts
                  WHERE EmployeeId = @EmployeeId
                  ORDER BY CreatedAt DESC",
                new { EmployeeId = employeeId }, cancellationToken: ct))).ToList();

            if (headers.Count == 0)
            {
                return headers;
            }

            var items = await connection.QueryAsync<SaleItemRow>(new CommandDefinition(
                @"SELECT SaleItemId, SaleId, ProductId, ProductName, Quantity, UnitSalePrice, LineSalePrice
                  FROM dbo.SalesDraftItems
                  WHERE SaleId IN @SaleIds",
                new { SaleIds = headers.Select(h => h.SaleId).ToArray() }, cancellationToken: ct));

            var lookup = items.ToLookup(i => i.SaleId);
            foreach (var header in headers)
            {
                header.Items = lookup[header.SaleId].Select(MapItem).ToList();
            }

            return headers;
        }

        public async Task UpdateCheckoutAsync(SalesDraftDTO draft, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesDrafts SET
 BaseSalePrice = @BaseSalePrice,
 FinalSalePrice = @FinalSalePrice,
 DailyInstallment = @DailyInstallment,
 DefaultTotalSalePrice = @DefaultTotalSalePrice,
 DefaultDailyInstallment = @DefaultDailyInstallment,
 DefaultDownPayment = @DefaultDownPayment,
 OverrideTotalSalePrice = @OverrideTotalSalePrice,
 OverrideDailyInstallment = @OverrideDailyInstallment,
 OverrideDownPayment = @OverrideDownPayment,
 DownPayment = @DownPayment
WHERE SaleId = @SaleId AND EmployeeId = @EmployeeId",
                draft, cancellationToken: ct));
        }

        public async Task<SalesDraftDTO?> GetByIdAsync(int saleId, int employeeId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var header = await connection.QueryFirstOrDefaultAsync<SalesDraftDTO>(new CommandDefinition(
                @"SELECT SaleId, EmployeeId, UserName, UserType, CityValue, CityName, Status, CustomerId, SourceCityValue,
                         FullName, Phone, Province, NationalCardNumber, Address, NearestLandmark, MukhtarName, RationCenterNumber,
                         EvaluationLevel, EvaluationNote, BaseSalePrice, FinalSalePrice, DailyInstallment,
                         DefaultTotalSalePrice, DefaultDailyInstallment, DefaultDownPayment,
                         OverrideTotalSalePrice, OverrideDailyInstallment, OverrideDownPayment, DownPayment,
                         CreatedAt,
                         CompletedAt, CompletedBy, DocumentsStatus, SalesRequestId, CustomerListId
                  FROM dbo.SalesDrafts
                  WHERE SaleId = @SaleId AND EmployeeId = @EmployeeId",
                new { SaleId = saleId, EmployeeId = employeeId }, cancellationToken: ct));
            if (header == null)
            {
                return null;
            }

            var items = await connection.QueryAsync<SaleItemRow>(new CommandDefinition(
                @"SELECT SaleItemId, SaleId, ProductId, ProductName, Quantity, UnitSalePrice, LineSalePrice
                  FROM dbo.SalesDraftItems WHERE SaleId = @SaleId",
                new { SaleId = saleId }, cancellationToken: ct));
            header.Items = items.Select(MapItem).ToList();
            return header;
        }

        private string RequireConnection()
        {
            return _guard.GetSalesConnectionString()
                   ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
        }

        private static SalesDraftItemDTO MapItem(SaleItemRow row) => new()
        {
            SaleItemId = row.SaleItemId,
            ProductId = row.ProductId,
            ProductName = row.ProductName,
            Quantity = row.Quantity,
            UnitSalePrice = row.UnitSalePrice,
            LineSalePrice = row.LineSalePrice
        };

        private sealed class SaleItemRow
        {
            public int SaleItemId { get; set; }
            public int SaleId { get; set; }
            public int ProductId { get; set; }
            public string? ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal UnitSalePrice { get; set; }
            public decimal LineSalePrice { get; set; }
        }

        private const string SchemaSql = @"
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
    }
}
