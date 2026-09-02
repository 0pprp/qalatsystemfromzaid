using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Sales.Services
{
    public sealed class SalesCompleteRepository : ISalesCompleteRepository
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly ISalesDraftRepository _drafts;

        public SalesCompleteRepository(SalesDevelopmentGuard guard, ISalesDraftRepository drafts)
        {
            _guard = guard;
            _drafts = drafts;
        }

        public async Task<SalesCompleteTxResult> CompleteInTransactionAsync(
            int saleId,
            int employeeId,
            string cityValue,
            CancellationToken ct)
        {
            await _drafts.EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            try
            {
                var header = await connection.QueryFirstOrDefaultAsync<SalesDraftDTO>(new CommandDefinition(
                    HeaderLockSql, new { SaleId = saleId }, tx, cancellationToken: ct));
                if (header == null)
                {
                    throw new SalesCompleteException(StatusCodes.Status404NotFound, "العملية غير موجودة.");
                }

                if (header.EmployeeId != employeeId
                    || !string.Equals(header.CityValue, cityValue, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك إتمام عملية تخص موظفاً أو فرعاً آخر.");
                }

                header.Items = (await connection.QueryAsync<SalesDraftItemDTO>(new CommandDefinition(
                    @"SELECT SaleItemId, ProductId, ProductName, Quantity, UnitSalePrice, LineSalePrice
                      FROM dbo.SalesDraftItems WHERE SaleId = @SaleId",
                    new { SaleId = saleId }, tx, cancellationToken: ct))).ToList();

                if (header.Status == SalesStatuses.Rejected || header.EvaluationLevel == SalesEvaluationLevels.Rejected)
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن إتمام عملية بيع مرفوضة.");
                }

                if (SalesCompleteRules.AlreadyCompleted(header.Status))
                {
                    header.Documents = (await LoadDocumentsAsync(connection, tx, saleId, ct)).ToList();
                    await tx.CommitAsync(ct);
                    return new SalesCompleteTxResult
                    {
                        Sale = header,
                        AlreadyCompleted = true,
                        InventoryDeducted = false,
                        DeductionCount = 0
                    };
                }

                var validation = SalesCompleteRules.ValidateForComplete(header);
                if (validation != null)
                {
                    throw new SalesCompleteException(StatusCodes.Status400BadRequest, validation);
                }

                var lockedItems = new List<(SalesDraftItemDTO Line, int StoreId)>();
                foreach (var line in header.Items)
                {
                    var stock = await connection.QueryFirstOrDefaultAsync<ItemLockRow>(new CommandDefinition(
                        @"SELECT ItemID, ItemName, Quantity, StoreID, ItemState
                          FROM dbo.Items WITH (UPDLOCK, ROWLOCK)
                          WHERE ItemID = @ProductId",
                        new { line.ProductId }, tx, cancellationToken: ct));
                    if (stock == null || stock.ItemState == false)
                    {
                        throw new SalesCompleteException(StatusCodes.Status409Conflict, "أحد المنتجات لم يعد موجوداً في المخزن.");
                    }

                    if ((stock.Quantity ?? 0) < line.Quantity)
                    {
                        throw new SalesCompleteException(StatusCodes.Status409Conflict, "الكمية المطلوبة غير متوفرة حالياً.");
                    }

                    lockedItems.Add((line, stock.StoreID ?? 0));
                }

                var deducted = await RecordFinalSaleAndDeductAsync(
                    connection, tx, header, employeeId, lockedItems, ct);

                var completedAt = DateTime.Now;
                await connection.ExecuteAsync(new CommandDefinition(
                    @"UPDATE dbo.SalesDrafts
                      SET Status = @Status,
                          CompletedAt = @CompletedAt,
                          CompletedBy = @CompletedBy,
                          DocumentsStatus = @DocumentsStatus
                      WHERE SaleId = @SaleId AND Status = @Pending",
                    new
                    {
                        SaleId = saleId,
                        Status = SalesStatuses.Completed,
                        CompletedAt = completedAt,
                        CompletedBy = employeeId,
                        DocumentsStatus = SalesStatuses.DocumentsPending,
                        Pending = SalesStatuses.Pending
                    }, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);

                header.Status = SalesStatuses.Completed;
                header.CompletedAt = completedAt;
                header.CompletedBy = employeeId;
                header.DocumentsStatus = SalesStatuses.DocumentsPending;
                return new SalesCompleteTxResult
                {
                    Sale = header,
                    AlreadyCompleted = false,
                    InventoryDeducted = deducted,
                    DeductionCount = deducted ? 1 : 0
                };
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<SalesDocumentRecord>> GetDocumentsAsync(int saleId, int employeeId, CancellationToken ct)
        {
            var owned = await GetOwnedSaleAsync(saleId, employeeId, ct);
            if (owned == null)
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك الوصول إلى مستندات عملية لا تخصك.");
            }

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesDocumentRecord>(new CommandDefinition(
                DocumentSelectSql, new { SaleId = saleId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<SalesDocumentRecord?> GetDocumentAsync(int saleId, int documentId, int employeeId, CancellationToken ct)
        {
            var owned = await GetOwnedSaleAsync(saleId, employeeId, ct);
            if (owned == null)
            {
                var header = await GetSaleHeaderAsync(saleId, ct);
                if (header != null)
                {
                    throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك تنزيل مستندات عملية لا تخصك.");
                }

                return null;
            }

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesDocumentRecord>(new CommandDefinition(
                DocumentSelectSql + " AND Id = @Id",
                new { SaleId = saleId, Id = documentId }, cancellationToken: ct));
        }

        public async Task<SalesDocumentRecord> UpsertDocumentAsync(SalesDocumentRecord record, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
IF EXISTS (SELECT 1 FROM dbo.SalesDocuments WHERE SaleId = @SaleId AND DocumentType = @DocumentType)
BEGIN
    UPDATE dbo.SalesDocuments
    SET FileName = @FileName, StoragePath = @StoragePath, CreatedAt = @CreatedAt
    WHERE SaleId = @SaleId AND DocumentType = @DocumentType;
    SELECT Id FROM dbo.SalesDocuments WHERE SaleId = @SaleId AND DocumentType = @DocumentType;
END
ELSE
BEGIN
    INSERT INTO dbo.SalesDocuments (SaleId, DocumentType, FileName, StoragePath, CreatedAt)
    OUTPUT INSERTED.Id
    VALUES (@SaleId, @DocumentType, @FileName, @StoragePath, @CreatedAt);
END;",
                new
                {
                    record.SaleId,
                    record.DocumentType,
                    record.FileName,
                    record.StoragePath,
                    CreatedAt = record.CreatedAt == default ? DateTime.Now : record.CreatedAt
                }, cancellationToken: ct));
            record.DocumentId = id;
            return record;
        }

        public async Task SetDocumentsStatusAsync(int saleId, string documentsStatus, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.SalesDrafts SET DocumentsStatus = @DocumentsStatus WHERE SaleId = @SaleId",
                new { SaleId = saleId, DocumentsStatus = documentsStatus }, cancellationToken: ct));
        }

        public Task<SalesDraftDTO?> GetOwnedSaleAsync(int saleId, int employeeId, CancellationToken ct) =>
            _drafts.GetByIdAsync(saleId, employeeId, ct);

        public async Task<SalesDraftDTO?> GetSaleHeaderAsync(int saleId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesDraftDTO>(new CommandDefinition(
                @"SELECT SaleId, EmployeeId, CityValue, Status, EvaluationLevel
                  FROM dbo.SalesDrafts WHERE SaleId = @SaleId",
                new { SaleId = saleId }, cancellationToken: ct));
        }

        private async Task<bool> RecordFinalSaleAndDeductAsync(
            SqlConnection connection,
            SqlTransaction tx,
            SalesDraftDTO sale,
            int employeeId,
            List<(SalesDraftItemDTO Line, int StoreId)> lockedItems,
            CancellationToken ct)
        {
            var storeId = lockedItems.Select(x => x.StoreId).FirstOrDefault(id => id > 0);
            var canUseOfficial = await ProcedureExistsAsync(connection, tx, "CustomersSales_Create", ct)
                                 && await ProcedureExistsAsync(connection, tx, "SelectItemSalesTemporaryPost_Create", ct)
                                 && await ProcedureExistsAsync(connection, tx, "InsertSelectItemsSaleFromSelectItemSalesTemporary", ct);

            if (canUseOfficial)
            {
                if (await ProcedureExistsAsync(connection, tx, "ClearSelectItemSalesTemporary", ct))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "ClearSelectItemSalesTemporary",
                        new { UserID = employeeId },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }

                foreach (var (line, _) in lockedItems)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "SelectItemSalesTemporaryPost_Create",
                        new { ItemID = line.ProductId, Quantity = line.Quantity, UserCreateID = employeeId },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }

                var now = DateTime.Now;
                if (sale.CustomerId is > 0 && await ProcedureExistsAsync(connection, tx, "InsertCustomerSale", ct))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "InsertCustomerSale",
                        new
                        {
                            UserID = employeeId,
                            CustomerID = sale.CustomerId,
                            DateCreate = now,
                            StoreID = storeId == 0 ? (int?)null : storeId,
                            DelegateID = (int?)null,
                            DiscountAmountTotal = 0d,
                            DiscountAmountTotalDay = 0d
                        },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }
                else
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "CustomersSales_Create",
                        new
                        {
                            CustomerName = Trunc(sale.FullName, 100),
                            PhoneNumber = Trunc(sale.Phone, 100),
                            Address = Trunc(sale.Address, 100),
                            ShopName = Trunc(sale.MukhtarName, 100),
                            NearestFunctionPoint = Trunc(sale.NearestLandmark, 100),
                            SaleName = Trunc(sale.UserName, 100),
                            ReceiptName = Trunc(sale.UserName, 100),
                            Notes = sale.EvaluationNote,
                            UserID = employeeId,
                            DateCreate = now,
                            StoreID = storeId == 0 ? (int?)null : storeId,
                            DelegateID = (int?)null,
                            DiscountAmountTotal = 0d,
                            DiscountAmountTotalDay = 0d
                        },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }

                return true;
            }

            foreach (var (line, _) in lockedItems)
            {
                var rows = await connection.ExecuteAsync(new CommandDefinition(
                    @"UPDATE dbo.Items
                      SET Quantity = Quantity - @Quantity
                      WHERE ItemID = @ProductId AND Quantity >= @Quantity AND ISNULL(ItemState, 1) = 1",
                    new { line.ProductId, line.Quantity }, tx, cancellationToken: ct));
                if (rows == 0)
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "الكمية المطلوبة غير متوفرة حالياً.");
                }
            }

            if (await TableExistsAsync(connection, tx, "WithdrawalStores", ct))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO dbo.WithdrawalStores (UserID, State, WithdrawalStoresDate, StoreID, AsyncState, AsyncID)
                      VALUES (@UserID, 1, GETDATE(), @StoreID, 0, NEWID())",
                    new { UserID = employeeId, StoreID = storeId == 0 ? (int?)null : storeId },
                    tx,
                    cancellationToken: ct));
            }

            return true;
        }

        private static async Task<IReadOnlyList<SalesDocumentDTO>> LoadDocumentsAsync(
            SqlConnection connection,
            SqlTransaction tx,
            int saleId,
            CancellationToken ct)
        {
            var rows = await connection.QueryAsync<SalesDocumentRecord>(new CommandDefinition(
                DocumentSelectSql, new { SaleId = saleId }, tx, cancellationToken: ct));
            return rows.Select(SalesDocumentMapper.ToDto).ToList();
        }

        private static async Task<bool> ProcedureExistsAsync(
            SqlConnection connection,
            SqlTransaction tx,
            string name,
            CancellationToken ct)
        {
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT OBJECT_ID(@Name, 'P')", new { Name = "dbo." + name }, tx, cancellationToken: ct));
            return id.HasValue && id.Value != 0;
        }

        private static async Task<bool> TableExistsAsync(
            SqlConnection connection,
            SqlTransaction tx,
            string name,
            CancellationToken ct)
        {
            var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT OBJECT_ID(@Name, 'U')", new { Name = "dbo." + name }, tx, cancellationToken: ct));
            return id.HasValue && id.Value != 0;
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module refused a non-demo connection.");

        private static string? Trunc(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? value : (value.Length <= max ? value : value[..max]);

        private const string HeaderLockSql = @"
SELECT SaleId, EmployeeId, UserName, UserType, CityValue, CityName, Status, CustomerId, SourceCityValue,
       FullName, Phone, Province, NationalCardNumber, Address, NearestLandmark, MukhtarName, RationCenterNumber,
       EvaluationLevel, EvaluationNote, BaseSalePrice, FinalSalePrice, DailyInstallment, CreatedAt,
       CompletedAt, CompletedBy, DocumentsStatus
FROM dbo.SalesDrafts WITH (UPDLOCK, ROWLOCK)
WHERE SaleId = @SaleId";

        private const string DocumentSelectSql = @"
SELECT Id AS DocumentId, SaleId, DocumentType, FileName, StoragePath, CreatedAt
FROM dbo.SalesDocuments
WHERE SaleId = @SaleId";

        private sealed class ItemLockRow
        {
            public int ItemID { get; set; }
            public string? ItemName { get; set; }
            public int? Quantity { get; set; }
            public int? StoreID { get; set; }
            public bool? ItemState { get; set; }
        }
    }

    public static class SalesDocumentMapper
    {
        public static SalesDocumentDTO ToDto(SalesDocumentRecord record) => new()
        {
            DocumentId = record.DocumentId,
            Type = record.DocumentType,
            FileName = record.FileName,
            DownloadUrl = $"/api/sales/{record.SaleId}/documents/{record.DocumentId}/download",
            CreatedAt = record.CreatedAt
        };
    }
}
