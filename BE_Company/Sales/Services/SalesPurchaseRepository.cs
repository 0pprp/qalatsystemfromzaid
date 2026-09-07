using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Sales.Services
{
    public interface ISalesPurchaseRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        Task<bool> InvoiceExistsAsync(int supplierId, string invoiceNumber, CancellationToken ct);
        Task<SalesPurchaseLookupsDTO> GetLookupsAsync(CancellationToken ct);
        Task<IReadOnlyList<SalesPurchaseItemOptionDTO>> GetItemsAsync(int storeId, string? search, CancellationToken ct);
        Task<IReadOnlyList<SalesPurchaseListItemDTO>> ListAsync(DateTime? fromDate, DateTime? toDate, string? textSearch, CancellationToken ct);
        Task<double> GetBoxAmountDenarAsync(int boxId, CancellationToken ct);
        Task<double> GetSupplierAccountAmountAsync(int supplierId, CancellationToken ct);
        Task<SalesPurchaseListItemDTO> CreateOfficialBuyAsync(SalesPurchaseCreateCommand command, CancellationToken ct);
    }

    public sealed class SalesPurchaseRepository : ISalesPurchaseRepository
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly IBuysRepository _buys;

        public SalesPurchaseRepository(SalesDevelopmentGuard guard, IBuysRepository buys)
        {
            _guard = guard;
            _buys = buys;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(ColumnsSql, cancellationToken: ct));
            await connection.ExecuteAsync(new CommandDefinition(IndexSql, cancellationToken: ct));
        }

        public async Task<bool> InvoiceExistsAsync(int supplierId, string invoiceNumber, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var normalized = SalesPurchaseRules.NormalizeInvoiceNumber(invoiceNumber);
            if (supplierId <= 0 || string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var found = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.Buys
                    WHERE SupplierID = @SupplierId
                      AND SupplierInvoiceNumber IS NOT NULL
                      AND SupplierInvoiceNumber = @InvoiceNumber
                  ) THEN 1 ELSE 0 END",
                new { SupplierId = supplierId, InvoiceNumber = normalized },
                cancellationToken: ct));
            return found == 1;
        }

        public async Task<SalesPurchaseLookupsDTO> GetLookupsAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var suppliers = (await connection.QueryAsync<SuppliersGetDTO>(new CommandDefinition(
                "Suppliers_GetAll",
                new { TextSearch = (string?)null },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct))).ToList();
            var stores = (await connection.QueryAsync<StoresDataGetDTO>(new CommandDefinition(
                "StoresData_GetAll",
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct))).ToList();
            var boxes = (await connection.QueryAsync<BoxsGetDTO>(new CommandDefinition(
                "Boxs_GetAll",
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct))).ToList();
            return new SalesPurchaseLookupsDTO
            {
                Suppliers = suppliers,
                Stores = stores,
                Boxes = boxes
            };
        }

        public async Task<IReadOnlyList<SalesPurchaseItemOptionDTO>> GetItemsAsync(int storeId, string? search, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = (await connection.QueryAsync<ItemsBuyDataDTO>(new CommandDefinition(
                "Items_GetByItemBuy",
                new { StoreID = storeId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct))).ToList();
            var q = (search ?? string.Empty).Trim();
            return rows
                .Where(r => r.ItemID is > 0)
                .Select(MapItem)
                .Where(r => string.IsNullOrWhiteSpace(q)
                            || r.ItemName.Contains(q, StringComparison.OrdinalIgnoreCase)
                            || r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public async Task<IReadOnlyList<SalesPurchaseListItemDTO>> ListAsync(
            DateTime? fromDate,
            DateTime? toDate,
            string? textSearch,
            CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var text = string.IsNullOrWhiteSpace(textSearch) || textSearch == "null" ? null : textSearch.Trim();
            var rows = await connection.QueryAsync<SalesPurchaseListItemDTO>(new CommandDefinition(
                @"SELECT v.BuyID AS BuyId,
                         v.BoundNumber,
                         v.SupplierID AS SupplierId,
                         v.SupplierName,
                         v.StoreName,
                         v.BoxName,
                         v.ItemsNames,
                         v.DateCreate,
                         v.NumberOfItemsBuys,
                         v.TotalAmountDenar,
                         v.AmountSpentDenar,
                         b.SupplierInvoiceNumber,
                         b.Notes,
                         b.CreatedByUserName,
                         b.CreatedByDisplayName,
                         b.CreatedByUserType,
                         b.CreatedByBranchId,
                         b.CreatedByBranchName,
                         b.CreatedAtUtc
                  FROM View_Buys v
                  INNER JOIN dbo.Buys b ON b.BuyID = v.BuyID
                  WHERE (@FromDate IS NULL OR CONVERT(DATE, v.DateCreate) >= CONVERT(DATE, @FromDate))
                    AND (@ToDate IS NULL OR CONVERT(DATE, v.DateCreate) <= CONVERT(DATE, @ToDate))
                    AND (
                        @TextSearch IS NULL
                        OR CAST(v.BuyID AS NVARCHAR(50)) LIKE N'%' + @TextSearch + N'%'
                        OR v.SupplierName LIKE N'%' + @TextSearch + N'%'
                        OR v.ItemsNames LIKE N'%' + @TextSearch + N'%'
                        OR ISNULL(b.Notes, N'') LIKE N'%' + @TextSearch + N'%'
                        OR ISNULL(b.SupplierInvoiceNumber, N'') LIKE N'%' + @TextSearch + N'%'
                    )
                  ORDER BY v.DateCreate DESC, v.BuyID DESC",
                new { FromDate = fromDate, ToDate = toDate, TextSearch = text },
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<double> GetBoxAmountDenarAsync(int boxId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<double?>(new CommandDefinition(
                "SELECT TOP 1 AmountDenar FROM View_Box WHERE BoxID = @BoxId AND ISNULL(BoxState, 1) = 1",
                new { BoxId = boxId },
                cancellationToken: ct)) ?? 0;
        }

        public async Task<double> GetSupplierAccountAmountAsync(int supplierId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<double?>(new CommandDefinition(
                "SELECT TOP 1 AmountAccount FROM View_Suppliers WHERE SupplierID = @SupplierId",
                new { SupplierId = supplierId },
                cancellationToken: ct)) ?? 0;
        }

        public async Task<SalesPurchaseListItemDTO> CreateOfficialBuyAsync(SalesPurchaseCreateCommand command, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            try
            {
                var postingUserId = await ResolvePostingUserIdAsync(
                    connection, tx, command.PreferredUserId, command.CreatedByDisplayName, ct);
                command.Buy.UserCreateID = postingUserId;

                await connection.ExecuteAsync(new CommandDefinition(
                    "EXEC sp_getapplock @Resource = @Resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 30000",
                    new { Resource = "SalesBuyUser_" + postingUserId },
                    tx,
                    cancellationToken: ct));
                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM dbo.SelectItemBuyTemporary WHERE UserID = @UserId",
                    new { UserId = postingUserId },
                    tx,
                    cancellationToken: ct));

                var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.Buys WITH (UPDLOCK, HOLDLOCK)
                        WHERE SupplierID = @SupplierId
                          AND SupplierInvoiceNumber = @InvoiceNumber
                      ) THEN 1 ELSE 0 END",
                    new
                    {
                        SupplierId = command.Buy.SupplierID,
                        InvoiceNumber = command.SupplierInvoiceNumber
                    },
                    tx,
                    cancellationToken: ct));
                if (duplicate == 1)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status409Conflict,
                        "فاتورة الشراء هذه مسجّلة مسبقاً لنفس المورد.");
                }

                BuysGetDTO? created;
                try
                {
                    created = await _buys.Buys_Create(command.Buy, connection, tx, ct);
                }
                catch (Exception ex) when (IsDuplicateKey(ex))
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status409Conflict,
                        "فاتورة الشراء هذه مسجّلة مسبقاً لنفس المورد.");
                }

                var buyId = created?.BuyID;
                if (buyId is not > 0)
                {
                    buyId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                        "SELECT CAST(IDENT_CURRENT(N'dbo.Buys') AS INT)",
                        transaction: tx,
                        cancellationToken: ct));
                }

                if (buyId is not > 0)
                {
                    throw new SalesCompleteException(
                        StatusCodes.Status500InternalServerError,
                        "تعذر تسجيل فاتورة الشراء في النظام الرئيسي.");
                }

                var postedAt = DateTime.UtcNow;
                var notes = BuildAuditNotes(command);
                await connection.ExecuteAsync(new CommandDefinition(
                    @"UPDATE dbo.Buys
                      SET SupplierInvoiceNumber = @InvoiceNumber,
                          Recipient = @InvoiceNumber,
                          Notes = @Notes,
                          CreatedByUserName = @UserName,
                          CreatedByDisplayName = @DisplayName,
                          CreatedByUserType = @UserType,
                          CreatedByBranchId = @BranchId,
                          CreatedByBranchName = @BranchName,
                          CreatedAtUtc = @CreatedAtUtc
                      WHERE BuyID = @BuyId",
                    new
                    {
                        BuyId = buyId,
                        InvoiceNumber = command.SupplierInvoiceNumber,
                        Notes = notes,
                        UserName = Trunc(command.CreatedByUserName, 100),
                        DisplayName = Trunc(command.CreatedByDisplayName, 150),
                        UserType = Trunc(command.CreatedByUserType, 50),
                        BranchId = Trunc(command.BranchId, 100),
                        BranchName = Trunc(command.BranchName, 150),
                        CreatedAtUtc = postedAt
                    },
                    tx,
                    cancellationToken: ct));

                await tx.CommitAsync(ct);
                return new SalesPurchaseListItemDTO
                {
                    BuyId = buyId.Value,
                    BoundNumber = created?.BoundNumber,
                    SupplierId = command.Buy.SupplierID,
                    SupplierName = created?.SupplierName,
                    StoreName = created?.StoreName,
                    BoxName = created?.BoxName,
                    ItemsNames = created?.ItemsNames,
                    DateCreate = created?.DateCreate ?? command.Buy.Date,
                    NumberOfItemsBuys = created?.NumberOfItemsBuys,
                    TotalAmountDenar = created?.TotalAmountDenar,
                    AmountSpentDenar = created?.AmountSpentDenar,
                    SupplierInvoiceNumber = command.SupplierInvoiceNumber,
                    CreatedByUserName = command.CreatedByUserName,
                    CreatedByDisplayName = command.CreatedByDisplayName,
                    CreatedByUserType = command.CreatedByUserType,
                    CreatedByBranchId = command.BranchId,
                    CreatedByBranchName = command.BranchName,
                    CreatedAtUtc = postedAt,
                    Notes = notes
                };
            }
            catch
            {
                try { await tx.RollbackAsync(ct); } catch { /* already committed or rolled back */ }
                throw;
            }
        }

        private static async Task<int> ResolvePostingUserIdAsync(
            SqlConnection connection,
            SqlTransaction tx,
            int preferredUserId,
            string displayName,
            CancellationToken ct)
        {
            if (preferredUserId > 0)
            {
                var exists = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                    "SELECT TOP 1 UserID FROM dbo.Users WHERE UserID = @UserId",
                    new { UserId = preferredUserId },
                    tx,
                    cancellationToken: ct));
                if (exists is > 0)
                {
                    return exists.Value;
                }
            }

            var resolved = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                @"SELECT TOP 1 UserID
                  FROM dbo.Users
                  ORDER BY
                    CASE
                      WHEN UserType = N'مدير مبيعات' AND UserName = @Name THEN 0
                      WHEN UserType = N'مدير مبيعات' THEN 1
                      WHEN UserType = N'محاسب رئيسي' THEN 2
                      ELSE 3
                    END,
                    UserID",
                new { Name = displayName },
                tx,
                cancellationToken: ct));
            if (resolved is not > 0)
            {
                throw new SalesCompleteException(
                    StatusCodes.Status409Conflict,
                    "لا يوجد مستخدم في قاعدة الفرع يمكن ربط فاتورة الشراء به.");
            }

            return resolved.Value;
        }

        private static SalesPurchaseItemOptionDTO MapItem(ItemsBuyDataDTO row)
        {
            var cost = Math.Round(row.ItemCostDenar, 0, MidpointRounding.AwayFromZero);
            var price = Math.Round(row.ItemPriceDenar, 0, MidpointRounding.AwayFromZero);
            var name = row.ItemName ?? string.Empty;
            return new SalesPurchaseItemOptionDTO
            {
                ItemId = row.ItemID!.Value,
                ItemName = name,
                ItemCostDenar = cost,
                ItemPriceDenar = price,
                DisplayName = $"{name} - سعر الشراء  ({cost:N0} دع) - سعر البيع  ({price:N0} دع)"
            };
        }

        private static string BuildAuditNotes(SalesPurchaseCreateCommand command)
        {
            var extra = string.IsNullOrWhiteSpace(command.Notes) ? string.Empty : command.Notes.Trim();
            var audit =
                $"أُدخلت بواسطة مسؤول المبيعات {command.CreatedByDisplayName} ({command.CreatedByUserName}) - فرع {command.BranchName} - رقم فاتورة المورد {command.SupplierInvoiceNumber}";
            return string.IsNullOrWhiteSpace(extra) ? audit : extra + " | " + audit;
        }

        private static bool IsDuplicateKey(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                {
                    return true;
                }
            }

            return false;
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private static string Trunc(string? value, int max)
        {
            var text = value?.Trim() ?? string.Empty;
            return text.Length <= max ? text : text[..max];
        }

        private const string ColumnsSql = @"
IF COL_LENGTH(N'dbo.Buys', N'SupplierInvoiceNumber') IS NULL
    ALTER TABLE dbo.Buys ADD SupplierInvoiceNumber NVARCHAR(80) NULL;
IF COL_LENGTH(N'dbo.Buys', N'CreatedByUserName') IS NULL
    ALTER TABLE dbo.Buys ADD CreatedByUserName NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Buys', N'CreatedByDisplayName') IS NULL
    ALTER TABLE dbo.Buys ADD CreatedByDisplayName NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.Buys', N'CreatedByUserType') IS NULL
    ALTER TABLE dbo.Buys ADD CreatedByUserType NVARCHAR(50) NULL;
IF COL_LENGTH(N'dbo.Buys', N'CreatedByBranchId') IS NULL
    ALTER TABLE dbo.Buys ADD CreatedByBranchId NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.Buys', N'CreatedByBranchName') IS NULL
    ALTER TABLE dbo.Buys ADD CreatedByBranchName NVARCHAR(150) NULL;
IF COL_LENGTH(N'dbo.Buys', N'CreatedAtUtc') IS NULL
    ALTER TABLE dbo.Buys ADD CreatedAtUtc DATETIME NULL;";

        private const string IndexSql = @"
IF COL_LENGTH(N'dbo.Buys', N'SupplierInvoiceNumber') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Buys_SupplierInvoiceNumber' AND object_id = OBJECT_ID(N'dbo.Buys'))
BEGIN
    EXEC(N'CREATE UNIQUE INDEX UX_Buys_SupplierInvoiceNumber ON dbo.Buys (SupplierID, SupplierInvoiceNumber) WHERE SupplierInvoiceNumber IS NOT NULL AND SupplierInvoiceNumber <> N''''');
END;";
    }
}
