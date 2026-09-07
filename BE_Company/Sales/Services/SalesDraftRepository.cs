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
            foreach (var sql in SalesDraftSchema.Commands)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(ct);
            }
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

        public async Task<SalesDraftDTO> ReplaceContentsAsync(SalesDraftDTO draft, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesDrafts SET
 UserName = @UserName, UserType = @UserType, CityValue = @CityValue, CityName = @CityName,
 Status = @Status, CustomerId = @CustomerId, SourceCityValue = @SourceCityValue,
 FullName = @FullName, Phone = @Phone, Province = @Province, NationalCardNumber = @NationalCardNumber,
 Address = @Address, NearestLandmark = @NearestLandmark, MukhtarName = @MukhtarName, RationCenterNumber = @RationCenterNumber,
 EvaluationLevel = @EvaluationLevel, EvaluationNote = @EvaluationNote,
 BaseSalePrice = @BaseSalePrice, FinalSalePrice = @FinalSalePrice, DailyInstallment = @DailyInstallment,
 DefaultTotalSalePrice = @DefaultTotalSalePrice, DefaultDailyInstallment = @DefaultDailyInstallment, DefaultDownPayment = @DefaultDownPayment,
 OverrideTotalSalePrice = @OverrideTotalSalePrice, OverrideDailyInstallment = @OverrideDailyInstallment, OverrideDownPayment = @OverrideDownPayment,
 DownPayment = @DownPayment, SalesRequestId = @SalesRequestId, CustomerListId = @CustomerListId
WHERE SaleId = @SaleId AND EmployeeId = @EmployeeId;",
                    draft, tx, cancellationToken: ct));

                await connection.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM dbo.SalesDraftItems WHERE SaleId = @SaleId",
                    new { draft.SaleId }, tx, cancellationToken: ct));

                foreach (var item in draft.Items)
                {
                    item.SaleItemId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesDraftItems (SaleId, ProductId, ProductName, Quantity, UnitSalePrice, LineSalePrice)
OUTPUT INSERTED.SaleItemId
VALUES (@SaleId, @ProductId, @ProductName, @Quantity, @UnitSalePrice, @LineSalePrice);",
                        new
                        {
                            draft.SaleId,
                            item.ProductId,
                            item.ProductName,
                            item.Quantity,
                            item.UnitSalePrice,
                            item.LineSalePrice
                        }, tx, cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
                var saved = await GetByIdAsync(draft.SaleId, draft.EmployeeId, ct);
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
                         DownPaymentCustomerPaymentId, CreatedAt,
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

            await SalesInventoryService.AttachListNamesAsync(connection, headers, ct);
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
                         DownPaymentCustomerPaymentId, CreatedAt,
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
            await SalesInventoryService.AttachListNamesAsync(connection, [header], ct);
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
    }
}
