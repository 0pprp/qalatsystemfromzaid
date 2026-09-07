using BE_Company.DTO;
using BE_Company.IRepository;
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
        private readonly ICustomersPaymentsRepository _payments;

        public SalesCompleteRepository(
            SalesDevelopmentGuard guard,
            ISalesDraftRepository drafts,
            ICustomersPaymentsRepository payments)
        {
            _guard = guard;
            _drafts = drafts;
            _payments = payments;
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

                if (header.Status == SalesStatuses.Rejected)
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

                var completedAt = DateTime.Now;
                var posting = await RecordFinalSaleAndDeductAsync(
                    connection, tx, header, employeeId, lockedItems, completedAt, ct);
                var deducted = posting.Deducted;
                if (posting.CustomerId is > 0 && header.CustomerId is not > 0)
                {
                    header.CustomerId = posting.CustomerId;
                }

                await SyncCanonicalCustomerIdentityAsync(connection, tx, header, posting.CustomerId, ct);

                await RecordDownPaymentIfNeededAsync(
                    connection, tx, header, employeeId, posting, completedAt, ct);

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

        private async Task<FinalSalePosting> RecordFinalSaleAndDeductAsync(
            SqlConnection connection,
            SqlTransaction tx,
            SalesDraftDTO sale,
            int employeeId,
            List<(SalesDraftItemDTO Line, int StoreId)> lockedItems,
            DateTime now,
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

                int? postedCustomerId = sale.CustomerId;
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
                            DelegateID = sale.CustomerListId,
                            DiscountAmountTotal = 0d,
                            DiscountAmountTotalDay = 0d
                        },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                }
                else
                {
                    var created = await connection.QueryFirstOrDefaultAsync<PostedCustomerSaleRow>(new CommandDefinition(
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
                            DelegateID = sale.CustomerListId,
                            DiscountAmountTotal = 0d,
                            DiscountAmountTotalDay = 0d
                        },
                        tx,
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: ct));
                    if (created?.CustomerID > 0)
                    {
                        postedCustomerId = created.CustomerID;
                    }
                    else
                    {
                        postedCustomerId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                            "SELECT CAST(IDENT_CURRENT(N'dbo.Customers') AS INT)",
                            transaction: tx,
                            cancellationToken: ct)) ?? sale.CustomerId;
                    }
                }

                return new FinalSalePosting
                {
                    Deducted = true,
                    OfficialSalePosted = true,
                    CustomerId = postedCustomerId
                };
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

            return new FinalSalePosting
            {
                Deducted = true,
                OfficialSalePosted = false,
                CustomerId = sale.CustomerId
            };
        }

        private async Task SyncCanonicalCustomerIdentityAsync(
            SqlConnection connection,
            SqlTransaction tx,
            SalesDraftDTO sale,
            int? postedCustomerId,
            CancellationToken ct)
        {
            string? requestName = null;
            string? requestPhone = null;
            string? requestAddress = null;
            string? requestProvince = null;
            if (sale.SalesRequestId is > 0 && await TableExistsAsync(connection, tx, "SalesRequests", ct))
            {
                var linked = await connection.QueryFirstOrDefaultAsync<LinkedRequestIdentity>(new CommandDefinition(
                    @"SELECT CustomerName, CustomerPhone, CustomerAddress, CustomerProvince
                      FROM dbo.SalesRequests
                      WHERE Id = @Id",
                    new { Id = sale.SalesRequestId }, tx, cancellationToken: ct));
                requestName = linked?.CustomerName;
                requestPhone = linked?.CustomerPhone;
                requestAddress = linked?.CustomerAddress;
                requestProvince = linked?.CustomerProvince;
            }

            var name = SalesCustomerIdentity.PreferName(sale.FullName, requestName);
            var phone = SalesCustomerIdentity.PreferText(sale.Phone, requestPhone);
            var address = SalesCustomerIdentity.PreferText(sale.Address, requestAddress);
            var province = SalesCustomerIdentity.PreferText(sale.Province, requestProvince);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = sale.FullName;
            }

            sale.FullName = name;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                sale.Phone = phone;
            }

            if (!string.IsNullOrWhiteSpace(address))
            {
                sale.Address = address;
            }

            if (!string.IsNullOrWhiteSpace(province))
            {
                sale.Province = province;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.SalesDrafts
                  SET FullName = @FullName,
                      Phone = @Phone,
                      Address = @Address,
                      Province = @Province,
                      CustomerId = COALESCE(@CustomerId, CustomerId)
                  WHERE SaleId = @SaleId",
                new
                {
                    SaleId = sale.SaleId,
                    sale.FullName,
                    sale.Phone,
                    sale.Address,
                    sale.Province,
                    CustomerId = postedCustomerId is > 0 ? postedCustomerId : sale.CustomerId
                }, tx, cancellationToken: ct));

            var customerId = postedCustomerId is > 0 ? postedCustomerId : sale.CustomerId;
            if (customerId is not > 0 || !await TableExistsAsync(connection, tx, "Customers", ct))
            {
                return;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.Customers
                  SET CustomerName = @CustomerName,
                      PhoneNumber = COALESCE(@PhoneNumber, PhoneNumber),
                      Address = COALESCE(@Address, Address)
                  WHERE CustomerID = @CustomerID",
                new
                {
                    CustomerID = customerId,
                    CustomerName = Trunc(name, 255),
                    PhoneNumber = Trunc(phone, 255),
                    Address = Trunc(address, 255)
                }, tx, cancellationToken: ct));
        }

        private async Task RecordDownPaymentIfNeededAsync(
            SqlConnection connection,
            SqlTransaction tx,
            SalesDraftDTO sale,
            int employeeId,
            FinalSalePosting posting,
            DateTime paymentDate,
            CancellationToken ct)
        {
            if (!SalesDownPaymentReceipt.ShouldRecord(sale.DownPayment, sale.DownPaymentCustomerPaymentId))
            {
                return;
            }

            if (!posting.OfficialSalePosted)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status500InternalServerError,
                    "تعذر تسجيل دفعة المقدمة لأن عملية البيع المحاسبية لم تُسجَّل.");
            }

            if (!await ProcedureExistsAsync(connection, tx, "CustomersPayments_Create", ct))
            {
                throw new SalesCompleteException(
                    StatusCodes.Status500InternalServerError,
                    "تعذر تسجيل دفعة المقدمة لأن إجراء الوصولات غير متوفر.");
            }

            var customerId = posting.CustomerId is > 0 ? posting.CustomerId : sale.CustomerId;
            if (customerId is not > 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status409Conflict,
                    "تعذر تسجيل دفعة المقدمة لأن حساب الزبون غير مرتبط بهذه العملية.");
            }

            CustomersPaymentsGetDTO? created;
            try
            {
                created = await _payments.CustomersPayments_Create(
                    new CustomersPaymentsPostDTO
                    {
                        UserCreateID = employeeId,
                        CustomerID = customerId,
                        PaymentDate = paymentDate,
                        PaymentAmount = (double)sale.DownPayment
                    },
                    connection,
                    tx,
                    ct);
            }
            catch (SalesCompleteException)
            {
                throw;
            }
            catch
            {
                throw new SalesCompleteException(
                    StatusCodes.Status500InternalServerError,
                    "فشل تسجيل دفعة المقدمة.");
            }

            if (created?.CustomerPaymentID is not > 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status500InternalServerError,
                    "فشل تسجيل دفعة المقدمة.");
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.SalesDrafts
                  SET DownPaymentCustomerPaymentId = @PaymentId,
                      CustomerId = COALESCE(@CustomerId, CustomerId)
                  WHERE SaleId = @SaleId
                    AND DownPaymentCustomerPaymentId IS NULL",
                new
                {
                    SaleId = sale.SaleId,
                    PaymentId = created.CustomerPaymentID,
                    CustomerId = customerId
                }, tx, cancellationToken: ct));

            sale.DownPaymentCustomerPaymentId = created.CustomerPaymentID;
            if (sale.CustomerId is not > 0)
            {
                sale.CustomerId = customerId;
            }
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
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private static string? Trunc(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? value : (value.Length <= max ? value : value[..max]);

        private const string HeaderLockSql = @"
SELECT SaleId, EmployeeId, UserName, UserType, CityValue, CityName, Status, CustomerId, SourceCityValue,
       FullName, Phone, Province, NationalCardNumber, Address, NearestLandmark, MukhtarName, RationCenterNumber,
       EvaluationLevel, EvaluationNote, BaseSalePrice, FinalSalePrice, DailyInstallment,
       DefaultTotalSalePrice, DefaultDailyInstallment, DefaultDownPayment,
       OverrideTotalSalePrice, OverrideDailyInstallment, OverrideDownPayment, DownPayment,
       DownPaymentCustomerPaymentId, CreatedAt,
       CompletedAt, CompletedBy, DocumentsStatus, SalesRequestId, CustomerListId
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

        private sealed class FinalSalePosting
        {
            public bool Deducted { get; init; }
            public bool OfficialSalePosted { get; init; }
            public int? CustomerId { get; init; }
        }

        private sealed class PostedCustomerSaleRow
        {
            public int CustomerID { get; set; }
            public int CustomerSaleID { get; set; }
        }

        private sealed class LinkedRequestIdentity
        {
            public string? CustomerName { get; set; }
            public string? CustomerPhone { get; set; }
            public string? CustomerAddress { get; set; }
            public string? CustomerProvince { get; set; }
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
